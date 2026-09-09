// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    public static partial class Create
    {
        /// <summary>
        /// Checks the finished graph against the design it was built from, and <b>never modifies it</b>.
        /// Any disagreement is a refusal and the whole materialisation is discarded.
        /// <para>
        /// <b>It reads the result back rather than trusting the assembly.</b> A lost air system, a plant
        /// room that replaced another, a room that quietly did not arrive - each of those is invisible to
        /// the code that built the graph and obvious to code that counts what is in it.
        /// </para>
        /// <para>
        /// <b>What is deliberately not checked:</b> whether supply balances extract, or whether a node's
        /// flows sum to zero. An unbalanced dwelling is a property of the authored design; reporting it is
        /// the caller's business and repairing it is nobody's here.
        /// </para>
        /// </summary>
        internal static void MechanicalVentilationReconcile(MechanicalVentilationContext context, AdjacencyCluster adjacencyCluster, SystemEnergyCentre systemEnergyCentre, Dictionary<Guid, List<Guid>> dictionary_Member, Dictionary<Guid, AirHandlingUnit> dictionary_AirHandlingUnit)
        {
            List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre.GetSystemPlantRooms() ?? new List<SystemPlantRoom>();

            if (systemPlantRooms.Count != 1)
            {
                context.Refuse(string.Format("The materialised energy centre does not reconcile with the design: it holds {0} plant rooms rather than one.", systemPlantRooms.Count));
                return;
            }

            SystemPlantRoom systemPlantRoom = systemPlantRooms[0];

            List<AirSystem> airSystems = systemPlantRoom.GetSystems<AirSystem>() ?? new List<AirSystem>();

            if (airSystems.Count != dictionary_Member.Count)
            {
                context.Refuse(string.Format("The materialised plant room does not reconcile with the design: it holds {0} air systems for {1} air handling unit(s).", airSystems.Count, dictionary_Member.Count));
                return;
            }

            //---------------------------------------------------------------------------------------------
            //Identity. RelationCluster.TryAddObject silently replaces on a guid collision, so two objects
            //that collided would simply be one and the graph would be short an object with nothing said.
            //---------------------------------------------------------------------------------------------

            HashSet<Guid> guids = new HashSet<Guid>();

            int count_Object = 0;

            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents() ?? new List<ISystemComponent>())
            {
                count_Object++;
                guids.Add(((Core.SAMObject)systemComponent).Guid);
            }

            foreach (Core.Systems.ISystem system in systemPlantRoom.GetSystems() ?? new List<Core.Systems.ISystem>())
            {
                count_Object++;
                guids.Add(((Core.SAMObject)system).Guid);
            }

            if (guids.Count != count_Object)
            {
                context.Refuse(string.Format("The materialised plant room does not reconcile with the design: {0} objects hold only {1} distinct identities.", count_Object, guids.Count));
                return;
            }

            //---------------------------------------------------------------------------------------------
            //Rooms. Exactly one SystemSpace per member space, each on its own unit, and no unit holding
            //another unit's room.
            //---------------------------------------------------------------------------------------------

            Dictionary<Guid, AirSystem> dictionary_AirSystem_ByGuid = new Dictionary<Guid, AirSystem>();

            foreach (AirSystem airSystem in airSystems)
            {
                dictionary_AirSystem_ByGuid[airSystem.Guid] = airSystem;
            }

            HashSet<Guid> guids_SystemSpace = new HashSet<Guid>();

            int count_Member = 0;

            foreach (KeyValuePair<Guid, List<Guid>> keyValuePair in dictionary_Member)
            {
                count_Member += keyValuePair.Value.Count;

                if (!context.Dictionary_AirSystem.TryGetValue(keyValuePair.Key, out AirSystem airSystem_Expected) || !dictionary_AirSystem_ByGuid.TryGetValue(airSystem_Expected.Guid, out AirSystem airSystem))
                {
                    context.Refuse(string.Format("The materialised plant room does not reconcile with the design: the air system of '{0}' is not in the result.", dictionary_AirHandlingUnit[keyValuePair.Key].Name));
                    return;
                }

                HashSet<Guid> guids_Expected = new HashSet<Guid>();

                foreach (Guid guid_Space in keyValuePair.Value)
                {
                    guids_Expected.Add(context.Dictionary_SystemSpaceGuid[guid_Space]);
                }

                HashSet<Guid> guids_Actual = new HashSet<Guid>();

                foreach (SystemSpace systemSpace in systemPlantRoom.GetRelatedObjects<SystemSpace>(airSystem) ?? new List<SystemSpace>())
                {
                    guids_Actual.Add(systemSpace.Guid);

                    if (systemSpace.FlowRate == null || double.IsNaN(systemSpace.FlowRate.Value) || double.IsInfinity(systemSpace.FlowRate.Value) || systemSpace.FlowRate.Value < 0)
                    {
                        context.Refuse(string.Format("The materialised space '{0}' does not reconcile with the design: it states a design flow of {1} l/s.", systemSpace.Name, systemSpace.FlowRate?.Value));
                        return;
                    }

                    if (!guids_SystemSpace.Add(systemSpace.Guid))
                    {
                        context.Refuse(string.Format("The materialised space '{0}' does not reconcile with the design: it belongs to more than one air system.", systemSpace.Name));
                        return;
                    }
                }

                if (!guids_Actual.SetEquals(guids_Expected))
                {
                    context.Refuse(string.Format("The materialised air system of '{0}' does not reconcile with the design: it serves {1} space(s) where the design gives it {2}.", dictionary_AirHandlingUnit[keyValuePair.Key].Name, guids_Actual.Count, guids_Expected.Count));
                    return;
                }

                //---------------------------------------------------------------------------------------------
                //Duties. The same summation in the same order, so exact equality is the right comparison.
                //---------------------------------------------------------------------------------------------

                double supply_Expected_Lps = 0;
                double extract_Expected_Lps = 0;

                foreach (Guid guid_Space in keyValuePair.Value)
                {
                    if (context.Dictionary_SupplyDuty_Lps.TryGetValue(guid_Space, out double supply_Lps))
                    {
                        supply_Expected_Lps += supply_Lps;
                    }

                    if (context.Dictionary_ExtractDuty_Lps.TryGetValue(guid_Space, out double extract_Lps))
                    {
                        extract_Expected_Lps += extract_Lps;
                    }
                }

                if (context.Dictionary_ScopeComplete.TryGetValue(keyValuePair.Key, out bool scopeComplete) && scopeComplete)
                {
                    //Cross-check against the analytical roll-up, which derives the same duties independently
                    //and catches a whole class of grouping mistake for one model pass.
                    if (adjacencyCluster.AirHandlingUnitDesignDuty(dictionary_AirHandlingUnit[keyValuePair.Key], out double supply_Analytical_Lps, out double extract_Analytical_Lps))
                    {
                        if (Math.Abs(supply_Analytical_Lps - supply_Expected_Lps) > Tolerance_Lps || Math.Abs(extract_Analytical_Lps - extract_Expected_Lps) > Tolerance_Lps)
                        {
                            context.Refuse(string.Format("The materialised air system of '{0}' does not reconcile with the design: it carries {1}/{2} l/s supply/extract where the design gives the unit {3}/{4} l/s.", dictionary_AirHandlingUnit[keyValuePair.Key].Name, supply_Expected_Lps, extract_Expected_Lps, supply_Analytical_Lps, extract_Analytical_Lps));
                            return;
                        }
                    }
                }
            }

            if (guids_SystemSpace.Count != count_Member)
            {
                context.Refuse(string.Format("The materialised plant room does not reconcile with the design: it holds {0} rooms where the design gives {1}.", guids_SystemSpace.Count, count_Member));
                return;
            }

            //---------------------------------------------------------------------------------------------
            //Legs. Every design flow rate on the graph is the one the design states.
            //---------------------------------------------------------------------------------------------

            Dictionary<Guid, double> dictionary_DesignFlowRate = new Dictionary<Guid, double>();

            foreach (ISystemConnection systemConnection in systemPlantRoom.GetSystemConnections() ?? new List<ISystemConnection>())
            {
                if (systemConnection is Core.SAMObject sAMObject && sAMObject.TryGetValue(SystemConnectionParameter.DesignFlowRate, out double designFlowRate_Lps))
                {
                    dictionary_DesignFlowRate[sAMObject.Guid] = designFlowRate_Lps;
                }
            }

            double supply_Materialised_Lps = 0;
            double extract_Materialised_Lps = 0;
            double supply_Design_Lps = 0;
            double extract_Design_Lps = 0;

            HashSet<Guid> guids_Connection_Supply = new HashSet<Guid>();
            HashSet<Guid> guids_Connection_Extract = new HashSet<Guid>();

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in context.Bindings)
            {
                if (mechanicalVentilationBinding.BindingType == MechanicalVentilationBindingType.SupplyConnection)
                {
                    guids_Connection_Supply.Add(mechanicalVentilationBinding.Guid_Systems);
                }
                else if (mechanicalVentilationBinding.BindingType == MechanicalVentilationBindingType.ExtractConnection)
                {
                    guids_Connection_Extract.Add(mechanicalVentilationBinding.Guid_Systems);
                }
            }

            foreach (Guid guid in guids_Connection_Supply)
            {
                if (!dictionary_DesignFlowRate.TryGetValue(guid, out double designFlowRate_Lps))
                {
                    context.Refuse("The materialised supply legs do not reconcile with the design: a leg the lineage names is not in the graph.");
                    return;
                }

                supply_Materialised_Lps += designFlowRate_Lps;
            }

            foreach (Guid guid in guids_Connection_Extract)
            {
                if (!dictionary_DesignFlowRate.TryGetValue(guid, out double designFlowRate_Lps))
                {
                    context.Refuse("The materialised extract legs do not reconcile with the design: a leg the lineage names is not in the graph.");
                    return;
                }

                extract_Materialised_Lps += designFlowRate_Lps;
            }

            foreach (double value in context.Dictionary_SupplyDuty_Lps.Values)
            {
                supply_Design_Lps += value;
            }

            foreach (double value in context.Dictionary_ExtractDuty_Lps.Values)
            {
                extract_Design_Lps += value;
            }

            if (Math.Abs(supply_Materialised_Lps - supply_Design_Lps) > Tolerance_Lps || Math.Abs(extract_Materialised_Lps - extract_Design_Lps) > Tolerance_Lps)
            {
                context.Refuse(string.Format("The materialised legs do not reconcile with the design: {0}/{1} l/s supply/extract where the design terminals give {2}/{3} l/s.", supply_Materialised_Lps, extract_Materialised_Lps, supply_Design_Lps, extract_Design_Lps));
                return;
            }

            //---------------------------------------------------------------------------------------------
            //Lineage, both ways.
            //---------------------------------------------------------------------------------------------

            HashSet<Guid> guids_Bound_Analytical = new HashSet<Guid>();
            HashSet<Guid> guids_Bound_Systems = new HashSet<Guid>();
            HashSet<string> keys_Binding = new HashSet<string>();

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in context.Bindings)
            {
                guids_Bound_Analytical.Add(mechanicalVentilationBinding.Guid_Analytical);
                guids_Bound_Systems.Add(mechanicalVentilationBinding.Guid_Systems);

                if (!keys_Binding.Add(string.Format("{0}|{1}|{2}", mechanicalVentilationBinding.BindingType, mechanicalVentilationBinding.Guid_Analytical, mechanicalVentilationBinding.Guid_Analytical_Secondary)))
                {
                    context.Refuse("The materialised lineage does not reconcile with the design: two rows state the same source for the same kind of object.");
                    return;
                }
            }

            foreach (KeyValuePair<Guid, List<Guid>> keyValuePair in dictionary_Member)
            {
                if (!guids_Bound_Analytical.Contains(keyValuePair.Key))
                {
                    context.Refuse(string.Format("The materialised lineage does not reconcile with the design: air handling unit '{0}' has no binding.", dictionary_AirHandlingUnit[keyValuePair.Key].Name));
                    return;
                }

                foreach (Guid guid_Space in keyValuePair.Value)
                {
                    if (!guids_Bound_Analytical.Contains(guid_Space))
                    {
                        context.Refuse(string.Format("The materialised lineage does not reconcile with the design: space '{0}' has no binding.", context.Dictionary_Space[guid_Space].Name));
                        return;
                    }

                    foreach (VentilationTerminal ventilationTerminal in context.Dictionary_Terminal.TryGetValue(guid_Space, out List<VentilationTerminal> ventilationTerminals) ? ventilationTerminals : new List<VentilationTerminal>())
                    {
                        if (!guids_Bound_Analytical.Contains(ventilationTerminal.Guid))
                        {
                            context.Refuse(string.Format("The materialised lineage does not reconcile with the design: design terminal '{0}' has no binding.", ventilationTerminal.Name));
                            return;
                        }
                    }
                }
            }

            foreach (AirSystem airSystem in airSystems)
            {
                if (!guids_Bound_Systems.Contains(airSystem.Guid))
                {
                    context.Refuse(string.Format("The materialised lineage does not reconcile with the design: air system '{0}' has no analytical source.", airSystem.Name));
                    return;
                }
            }

            foreach (Guid guid in guids_SystemSpace)
            {
                if (!guids_Bound_Systems.Contains(guid))
                {
                    context.Refuse("The materialised lineage does not reconcile with the design: a materialised room has no analytical source.");
                    return;
                }
            }

            foreach (Guid guid in dictionary_DesignFlowRate.Keys)
            {
                if (!guids_Bound_Systems.Contains(guid))
                {
                    context.Refuse("The materialised lineage does not reconcile with the design: a materialised leg has no analytical source.");
                    return;
                }
            }

            context.Note(string.Format("{0} air system(s), {1} room(s) and {2} lineage binding(s) materialised and reconciled against the design.", airSystems.Count, guids_SystemSpace.Count, context.Bindings.Count));
        }

        /// <summary>
        /// The margin in l/s within which two independently derived design duties count as agreeing - the
        /// same order as <c>Query.AirMovementResidualTolerance</c> and <c>PartFAirflowNetwork.Tolerance_Lps</c>.
        /// </summary>
        private const double Tolerance_Lps = 1e-9;
    }
}
