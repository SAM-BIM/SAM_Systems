// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System;
using System.Collections.Generic;

//SAM.Analytical.Systems.Query shadows SAM.Analytical.Query inside this namespace, and the transfer
//authority lives in the analytical one.
using DesignTransferAirMovement = SAM.Analytical.Query.DesignTransferAirMovement;

namespace SAM.Analytical.Systems
{
    public static partial class Create
    {
        /// <summary>
        /// Materialises the mechanical ventilation the analytical design already states onto a supplied
        /// <c>SystemEnergyCentre</c> topology template: one <c>AirSystem</c> per physical analytical air
        /// handling unit, one <c>SystemSpace</c> per served space, the design supply and extract legs, and
        /// the authored space-to-space transfer air.
        /// <para>
        /// <b>It reads the design and never rebalances, repairs or completes it.</b> The design terminal
        /// duties are the only airflow authority: a regulatory requirement, a selected product's capacity
        /// and an operating airflow are three different numbers and none of them is read here. Where the
        /// design does not say something, this refuses rather than inventing it - an unbalanced dwelling
        /// or a missing transfer route is a property of the design, and repairing it is nobody's business
        /// in this seam.
        /// </para>
        /// <para>
        /// <b>Generic.</b> Nothing here knows about Approved Document O, or about TAS, TPD, TBD or TSD. A
        /// caller decides which spaces are in scope - which is where dwelling policy lives - and this
        /// materialises what the model says about them.
        /// </para>
        /// <para>
        /// <b>Fail closed, with no partial-success graph.</b> Every refusal discards the whole
        /// materialisation: see <see cref="MechanicalVentilationMaterialisation"/>.
        /// </para>
        /// <para>
        /// <b>Isolated in both directions.</b> The analytical model is strictly read-only and the supplied
        /// template is never mutated - the result is built on a deep clone of it, with every flow value,
        /// connection, space and schedule a fresh instance. Mutating a returned graph cannot reach either
        /// input.
        /// </para>
        /// </summary>
        /// <param name="adjacencyCluster">
        /// The analytical design. An <c>AdjacencyCluster</c> rather than an <c>AnalyticalModel</c> on
        /// purpose: <c>AnalyticalModel.AdjacencyCluster</c> hands back a fresh shallow copy on every
        /// access, so reading one through it would read a different object each time.
        /// </param>
        /// <param name="systemEnergyCentre_Template">
        /// The topology template. It is a source and is never written to: the shipped templates are read
        /// as text and handed back as a new graph on every load, and this only ever clones what it is given.
        /// </param>
        /// <param name="mechanicalVentilationSettings">The generic options. Null takes the defaults.</param>
        /// <param name="spaces">
        /// The scope. <b>Null means every space of the cluster</b> - the convention
        /// <c>AddPartOBaseMVHRSystem</c> already uses.
        /// </param>
        public static MechanicalVentilationMaterialisation MechanicalVentilation(this AdjacencyCluster adjacencyCluster, SystemEnergyCentre systemEnergyCentre_Template, MechanicalVentilationSettings mechanicalVentilationSettings = null, IEnumerable<Space> spaces = null)
        {
            MechanicalVentilationContext context = new MechanicalVentilationContext
            {
                //Copied on the way in, so the caller cannot reach into the materialisation afterwards.
                Settings = new MechanicalVentilationSettings(mechanicalVentilationSettings)
            };

            //=============================================================================================
            //D0 - what has to be true before anything is read
            //=============================================================================================

            if (adjacencyCluster == null)
            {
                context.Refuse("No model was supplied, so there is no design to materialise.");
                return Result(context, null);
            }

            if (systemEnergyCentre_Template == null)
            {
                context.Refuse("No topology template was supplied, so there is nothing to materialise the ventilation onto.");
                return Result(context, null);
            }

            List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre_Template.GetSystemPlantRooms() ?? new List<SystemPlantRoom>();

            if (systemPlantRooms.Count != 1)
            {
                context.Refuse(string.Format("The topology template contains {0} plant rooms, so which one the ventilation is materialised into is ambiguous.", systemPlantRooms.Count));
                return Result(context, null);
            }

            //A deep clone with guids preserved, taken by GetSystemPlantRooms itself - so the supplied
            //template instance is untouched from here on. SystemEnergyCentre.Duplicate is not used: it
            //mutates its source. SystemPlantRoom.Duplicate is not used: it assigns random guids and
            //downgrades a DisplaySystemPlantRoom to a plain one.
            SystemPlantRoom systemPlantRoom = systemPlantRooms[0];

            List<AirSystem> airSystems_Template = systemPlantRoom.GetSystems<AirSystem>() ?? new List<AirSystem>();

            if (airSystems_Template.Count != 1)
            {
                context.Refuse(string.Format("The topology template's plant room contains {0} air systems, so which one is the prototype is ambiguous.", airSystems_Template.Count));
                return Result(context, null);
            }

            AirSystem airSystem_Template = airSystems_Template[0];

            if (context.Settings.Schedule != null)
            {
                if (string.IsNullOrWhiteSpace(context.Settings.Schedule.Name))
                {
                    context.Refuse("The operating schedule states no name, so no component can reference it.");
                    return Result(context, null);
                }

                if (context.Settings.Schedule is YearlySchedule yearlySchedule)
                {
                    int count = 0;

                    foreach (double value in yearlySchedule.Values ?? new double[0])
                    {
                        if (double.IsNaN(value) || double.IsInfinity(value))
                        {
                            count++;
                        }
                    }

                    if (count != 0)
                    {
                        context.Refuse(string.Format("The operating schedule holds {0} non-finite value(s), so it does not cover the period being materialised.", count));
                        return Result(context, null);
                    }
                }
            }

            //=============================================================================================
            //D1 - one pass each. Nothing below re-enumerates the model.
            //=============================================================================================

            foreach (Space space in adjacencyCluster.GetSpaces() ?? new List<Space>())
            {
                if (space == null)
                {
                    continue;
                }

                if (space.Guid == System.Guid.Empty)
                {
                    context.Refuse("A space carries no identity, so a deterministic binding could not be derived.");
                    return Result(context, null);
                }

                context.Dictionary_Space[space.Guid] = space;
            }

            Dictionary<string, List<AirHandlingUnit>> dictionary_AirHandlingUnitName = new Dictionary<string, List<AirHandlingUnit>>(StringComparer.Ordinal);
            Dictionary<Guid, AirHandlingUnit> dictionary_AirHandlingUnit = new Dictionary<Guid, AirHandlingUnit>();

            foreach (AirHandlingUnit airHandlingUnit in adjacencyCluster.GetObjects<AirHandlingUnit>() ?? new List<AirHandlingUnit>())
            {
                if (airHandlingUnit == null)
                {
                    continue;
                }

                if (airHandlingUnit.Guid == System.Guid.Empty)
                {
                    context.Refuse("An air handling unit carries no identity, so a deterministic binding could not be derived.");
                    return Result(context, null);
                }

                dictionary_AirHandlingUnit[airHandlingUnit.Guid] = airHandlingUnit;

                string name = airHandlingUnit.Name;
                if (name == null)
                {
                    continue;
                }

                if (!dictionary_AirHandlingUnitName.TryGetValue(name, out List<AirHandlingUnit> airHandlingUnits))
                {
                    airHandlingUnits = new List<AirHandlingUnit>();
                    dictionary_AirHandlingUnitName[name] = airHandlingUnits;
                }

                airHandlingUnits.Add(airHandlingUnit);
            }

            Dictionary<(Guid, Guid), DesignTransferAirMovement> dictionary_Transfer = adjacencyCluster.DesignTransferSpaceAirMovements() ?? new Dictionary<(Guid, Guid), DesignTransferAirMovement>();

            Dictionary<Guid, List<(Guid, Guid)>> dictionary_TransferAdjacency = new Dictionary<Guid, List<(Guid, Guid)>>();

            foreach (KeyValuePair<(Guid, Guid), DesignTransferAirMovement> keyValuePair in dictionary_Transfer)
            {
                if (keyValuePair.Value.SpaceAirMovement != null && keyValuePair.Value.SpaceAirMovement.Guid == System.Guid.Empty)
                {
                    context.Refuse("A design transfer air movement carries no identity, so a deterministic binding could not be derived.");
                    return Result(context, null);
                }

                Adjacency(dictionary_TransferAdjacency, keyValuePair.Value.FromGuid, keyValuePair.Key);
                Adjacency(dictionary_TransferAdjacency, keyValuePair.Value.ToGuid, keyValuePair.Key);
            }

            HashSet<Guid> guids_Scope = new HashSet<Guid>();

            if (spaces == null)
            {
                foreach (Guid guid in context.Dictionary_Space.Keys)
                {
                    guids_Scope.Add(guid);
                }
            }
            else
            {
                foreach (Space space in spaces)
                {
                    if (space != null && context.Dictionary_Space.ContainsKey(space.Guid))
                    {
                        guids_Scope.Add(space.Guid);
                    }
                }
            }

            //=============================================================================================
            //D2 - group the ventilation systems by the physical unit they serve from
            //=============================================================================================

            List<VentilationSystem> ventilationSystems = adjacencyCluster.GetObjects<VentilationSystem>() ?? new List<VentilationSystem>();

            ventilationSystems.Sort((x, y) => x.Guid.CompareTo(y.Guid));

            Dictionary<Guid, List<VentilationSystem>> dictionary_Group = new Dictionary<Guid, List<VentilationSystem>>();

            foreach (VentilationSystem ventilationSystem in ventilationSystems)
            {
                if (ventilationSystem == null)
                {
                    continue;
                }

                //The one place a name is read, and it is not a membership decision: it locates the
                //AirHandlingUnit object that already exists in the model. AddPartOBaseMVHRSystem adds the
                //unit with no relation at all and binds it through SupplyUnitName, and SAM's own accessor
                //records that as pre-existing debt - migrating it would be a SAM production change. What
                //happens here is stricter than that accessor, which silently takes the first of several
                //matches: every ambiguity below refuses. After this, AirHandlingUnit.Guid is the sole
                //authority and no name is read again.
                if (!TryGetAirHandlingUnit(context, dictionary_AirHandlingUnitName, ventilationSystem, VentilationSystemParameter.SupplyUnitName, true, out AirHandlingUnit airHandlingUnit_Supply))
                {
                    return Result(context, null);
                }

                if (!TryGetAirHandlingUnit(context, dictionary_AirHandlingUnitName, ventilationSystem, VentilationSystemParameter.ExhaustUnitName, false, out AirHandlingUnit airHandlingUnit_Exhaust))
                {
                    return Result(context, null);
                }

                if (airHandlingUnit_Exhaust != null && airHandlingUnit_Exhaust.Guid != airHandlingUnit_Supply.Guid)
                {
                    context.Refuse(string.Format("Ventilation system '{0}' names '{1}' for supply and '{2}' for exhaust, so which physical unit it is could not be resolved.", ventilationSystem.Name, airHandlingUnit_Supply.Name, airHandlingUnit_Exhaust.Name));
                    return Result(context, null);
                }

                if (!dictionary_Group.TryGetValue(airHandlingUnit_Supply.Guid, out List<VentilationSystem> ventilationSystems_Group))
                {
                    ventilationSystems_Group = new List<VentilationSystem>();
                    dictionary_Group[airHandlingUnit_Supply.Guid] = ventilationSystems_Group;
                }

                ventilationSystems_Group.Add(ventilationSystem);
            }

            List<Guid> guids_AirHandlingUnit = new List<Guid>(dictionary_Group.Keys);
            guids_AirHandlingUnit.Sort();

            //=============================================================================================
            //D3 / D4 - membership and design duties, per group
            //=============================================================================================

            Dictionary<Guid, List<Guid>> dictionary_Member = new Dictionary<Guid, List<Guid>>();

            foreach (Guid guid_AirHandlingUnit in guids_AirHandlingUnit)
            {
                if (!TryGetMembers(context, adjacencyCluster, dictionary_Group[guid_AirHandlingUnit], guid_AirHandlingUnit, dictionary_AirHandlingUnit, dictionary_TransferAdjacency, guids_Scope, out List<Guid> guids_Member))
                {
                    return Result(context, null);
                }

                dictionary_Member[guid_AirHandlingUnit] = guids_Member;
            }

            foreach (Guid guid_AirHandlingUnit in guids_AirHandlingUnit)
            {
                foreach (Guid guid_Space in dictionary_Member[guid_AirHandlingUnit])
                {
                    if (!TryGetDuties(context, guid_Space))
                    {
                        return Result(context, null);
                    }
                }
            }

            //=============================================================================================
            //D5 - the result, and the one plant room every unit is materialised into
            //=============================================================================================

            List<string> components_EnergyCentre = new List<string>
            {
                Query.MechanicalVentilationGuidComponent(systemEnergyCentre_Template.Guid),
                guids_AirHandlingUnit.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            foreach (Guid guid in guids_AirHandlingUnit)
            {
                components_EnergyCentre.Add(Query.MechanicalVentilationGuidComponent(guid));
            }

            //The settings' identity-affecting fields, in a fixed order, so the same design against the same
            //template with different settings does not silently reuse identities.
            components_EnergyCentre.Add(context.Settings.Name);
            components_EnergyCentre.Add(context.Settings.MaterialiseSystemSpaceComponents.ToString());
            components_EnergyCentre.Add(context.Settings.Schedule?.GetType()?.FullName);
            components_EnergyCentre.Add(context.Settings.Schedule?.Name);

            context.Key_EnergyCentre = string.Join("|", components_EnergyCentre);

            Guid guid_EnergyCentre = context.Guid_Derived("SystemEnergyCentre", components_EnergyCentre.ToArray());

            if (context.HasRefusals)
            {
                return Result(context, null);
            }

            SystemEnergyCentre result = new SystemEnergyCentre(guid_EnergyCentre, new SystemEnergyCentre(context.Settings.Name ?? systemEnergyCentre_Template.Name))
            {
                Name = context.Settings.Name ?? systemEnergyCentre_Template.Name
            };

            //Once, not once per unit.
            systemEnergyCentre_Template.CopyProperties(result);

            if (systemEnergyCentre_Template.TryGetValue(SystemEnergyCentreParameter.SystemTemplate, out SystemTemplate systemTemplate) && systemTemplate != null)
            {
                result.SetValue(SystemEnergyCentreParameter.SystemTemplate, systemTemplate);
            }

            //=============================================================================================
            //D6 - one AirSystem per physical unit, all inside that one plant room
            //=============================================================================================

            foreach (Guid guid_AirHandlingUnit in guids_AirHandlingUnit)
            {
                if (!MechanicalVentilationAirSystem(context, systemPlantRoom, airSystem_Template, dictionary_AirHandlingUnit[guid_AirHandlingUnit], dictionary_Member[guid_AirHandlingUnit]))
                {
                    return Result(context, null);
                }
            }

            //=============================================================================================
            //D7 - the authored transfer air. Never invented, never repaired, never inferred from a name.
            //=============================================================================================

            List<(Guid, Guid)> keys_Transfer = new List<(Guid, Guid)>(dictionary_Transfer.Keys);
            keys_Transfer.Sort((x, y) => x.Item1.CompareTo(y.Item1) != 0 ? x.Item1.CompareTo(y.Item1) : x.Item2.CompareTo(y.Item2));

            foreach ((Guid, Guid) key in keys_Transfer)
            {
                if (!TryMaterialiseTransfer(context, adjacencyCluster, systemPlantRoom, dictionary_Transfer, key, dictionary_AirHandlingUnit))
                {
                    return Result(context, null);
                }
            }

            //=============================================================================================
            //D8 - the operating schedule
            //=============================================================================================

            if (context.Settings.Schedule != null)
            {
                if (!result.TryGetValue(SystemEnergyCentreParameter.AnalyticalSystemsProperties, out AnalyticalSystemsProperties analyticalSystemsProperties) || analyticalSystemsProperties == null)
                {
                    analyticalSystemsProperties = new AnalyticalSystemsProperties();
                }

                //A copy, so the output never shares the caller's mutable instance.
                analyticalSystemsProperties.Add(Core.Query.Clone(context.Settings.Schedule));

                result.SetValue(SystemEnergyCentreParameter.AnalyticalSystemsProperties, analyticalSystemsProperties);
            }

            //=============================================================================================
            //D9 - the template's own air plant is now superseded by the N copies. Removed by the exact set
            //     the walk identified, so the shared non-air plant cannot be caught by a relation walk.
            //=============================================================================================

            MechanicalVentilationTemplateSubgraph(systemPlantRoom, airSystem_Template, true, out Dictionary<Guid, ISystemJSAMObject> dictionary_Template, out Dictionary<Guid, ISystemJSAMObject> _);

            foreach (ISystemJSAMObject systemJSAMObject in dictionary_Template.Values)
            {
                if (systemJSAMObject is ISystemSpace systemSpace_Template)
                {
                    foreach (ISystemSpaceComponent systemSpaceComponent in systemPlantRoom.GetRelatedObjects<ISystemSpaceComponent>(systemSpace_Template) ?? new List<ISystemSpaceComponent>())
                    {
                        systemPlantRoom.Remove(systemSpaceComponent);
                    }
                }
            }

            foreach (ISystemJSAMObject systemJSAMObject in dictionary_Template.Values)
            {
                if (systemJSAMObject is Core.Systems.ISystem system)
                {
                    systemPlantRoom.Remove(system, false);
                }
                else if (systemJSAMObject is ISystemGroup systemGroup)
                {
                    systemPlantRoom.Remove(systemGroup);
                }
                else if (systemJSAMObject is ISystemConnection systemConnection)
                {
                    systemPlantRoom.Remove(systemConnection);
                }
                else if (systemJSAMObject is ISystemComponent systemComponent)
                {
                    systemPlantRoom.Remove(systemComponent);
                }
            }

            //=============================================================================================
            //D11 - exactly one Add for the whole materialisation. The energy centre stores plant rooms in a
            //      guid-keyed dictionary and Add deep-clones, so calling it per unit would both re-clone the
            //      whole graph and replace the previous entry - which is precisely how a two-unit run loses
            //      an AirSystem.
            //=============================================================================================

            result.Add(systemPlantRoom);

            //=============================================================================================
            //D10 - reconciliation. Read off the finished graph, and never a modification of it.
            //=============================================================================================

            MechanicalVentilationReconcile(context, adjacencyCluster, result, dictionary_Member, dictionary_AirHandlingUnit);

            context.Bindings.Sort((x, y) => x.CompareTo(y));

            return Result(context, result);
        }

        private static MechanicalVentilationMaterialisation Result(MechanicalVentilationContext context, SystemEnergyCentre systemEnergyCentre)
        {
            return new MechanicalVentilationMaterialisation(systemEnergyCentre, context.Refusals, context.Notes, context.Bindings);
        }

        private static void Adjacency(Dictionary<Guid, List<(Guid, Guid)>> dictionary, Guid guid, (Guid, Guid) key)
        {
            if (!dictionary.TryGetValue(guid, out List<(Guid, Guid)> keys))
            {
                keys = new List<(Guid, Guid)>();
                dictionary[guid] = keys;
            }

            keys.Add(key);
        }

        private static bool TryGetAirHandlingUnit(MechanicalVentilationContext context, Dictionary<string, List<AirHandlingUnit>> dictionary_AirHandlingUnitName, VentilationSystem ventilationSystem, VentilationSystemParameter ventilationSystemParameter, bool required, out AirHandlingUnit airHandlingUnit)
        {
            airHandlingUnit = null;

            string name = ventilationSystem.GetValue<string>(ventilationSystemParameter);

            if (string.IsNullOrWhiteSpace(name))
            {
                if (!required)
                {
                    return true;
                }

                context.Refuse(string.Format("Ventilation system '{0}' names no air handling unit, so the physical unit it serves from could not be resolved.", ventilationSystem.Name));
                return false;
            }

            if (!dictionary_AirHandlingUnitName.TryGetValue(name, out List<AirHandlingUnit> airHandlingUnits) || airHandlingUnits.Count == 0)
            {
                context.Refuse(string.Format("Ventilation system '{0}' names air handling unit '{1}', which the model does not contain.", ventilationSystem.Name, name));
                return false;
            }

            if (airHandlingUnits.Count > 1)
            {
                context.Refuse(string.Format("Ventilation system '{0}' names air handling unit '{1}', which {2} units in the model answer to.", ventilationSystem.Name, name, airHandlingUnits.Count));
                return false;
            }

            airHandlingUnit = airHandlingUnits[0];

            return true;
        }

        /// <summary>
        /// The spaces one physical unit serves: those its systems are related to, those its design
        /// terminals serve, and those the authored transfer topology routes air through - which is what
        /// brings in a hall or landing whose own supply and extract are both absent. Membership falls out
        /// of relations and guids; no name is read.
        /// </summary>
        private static bool TryGetMembers(MechanicalVentilationContext context, AdjacencyCluster adjacencyCluster, List<VentilationSystem> ventilationSystems, Guid guid_AirHandlingUnit, Dictionary<Guid, AirHandlingUnit> dictionary_AirHandlingUnit, Dictionary<Guid, List<(Guid, Guid)>> dictionary_TransferAdjacency, HashSet<Guid> guids_Scope, out List<Guid> guids_Member)
        {
            guids_Member = new List<Guid>();

            HashSet<Guid> guids_Seed = new HashSet<Guid>();

            Dictionary<Guid, List<VentilationTerminal>> dictionary_Terminal = new Dictionary<Guid, List<VentilationTerminal>>();

            foreach (VentilationSystem ventilationSystem in ventilationSystems)
            {
                foreach (Space space in adjacencyCluster.GetRelatedObjects<Space>(ventilationSystem) ?? new List<Space>())
                {
                    if (space != null && context.Dictionary_Space.ContainsKey(space.Guid))
                    {
                        guids_Seed.Add(space.Guid);
                    }
                }

                foreach (VentilationTerminal ventilationTerminal in adjacencyCluster.VentilationTerminals(ventilationSystem) ?? new List<VentilationTerminal>())
                {
                    if (ventilationTerminal == null)
                    {
                        continue;
                    }

                    if (ventilationTerminal.Guid == System.Guid.Empty)
                    {
                        context.Refuse("A design terminal carries no identity, so a deterministic binding could not be derived.");
                        return false;
                    }

                    List<Space> spaces_Terminal = adjacencyCluster.GetRelatedObjects<Space>(ventilationTerminal) ?? new List<Space>();

                    if (spaces_Terminal.Count != 1)
                    {
                        context.Refuse(string.Format("Design terminal '{0}' is related to {1} spaces, so the room it serves could not be resolved.", ventilationTerminal.Name, spaces_Terminal.Count));
                        return false;
                    }

                    Guid guid_Space = spaces_Terminal[0].Guid;

                    guids_Seed.Add(guid_Space);

                    if (!dictionary_Terminal.TryGetValue(guid_Space, out List<VentilationTerminal> ventilationTerminals))
                    {
                        ventilationTerminals = new List<VentilationTerminal>();
                        dictionary_Terminal[guid_Space] = ventilationTerminals;
                    }

                    ventilationTerminals.Add(ventilationTerminal);
                }
            }

            //Transfer-connected spaces, breadth-first over the authored movements.
            HashSet<Guid> guids_Reached = new HashSet<Guid>(guids_Seed);

            Queue<Guid> queue = new Queue<Guid>(guids_Seed);

            while (queue.Count != 0)
            {
                Guid guid = queue.Dequeue();

                if (!dictionary_TransferAdjacency.TryGetValue(guid, out List<(Guid, Guid)> keys))
                {
                    continue;
                }

                foreach ((Guid, Guid) key in keys)
                {
                    foreach (Guid guid_Endpoint in new[] { key.Item1, key.Item2 })
                    {
                        if (context.Dictionary_Space.ContainsKey(guid_Endpoint) && guids_Reached.Add(guid_Endpoint))
                        {
                            queue.Enqueue(guid_Endpoint);
                        }
                    }
                }
            }

            foreach (Guid guid in guids_Reached)
            {
                if (!guids_Scope.Contains(guid))
                {
                    continue;
                }

                if (context.Dictionary_SpaceOwner.TryGetValue(guid, out Guid guid_Owner))
                {
                    context.Refuse(string.Format("Space '{0}' is served by air handling units '{1}' and '{2}'.", context.Dictionary_Space[guid].Name, dictionary_AirHandlingUnit[guid_Owner].Name, dictionary_AirHandlingUnit[guid_AirHandlingUnit].Name));
                    return false;
                }

                context.Dictionary_SpaceOwner[guid] = guid_AirHandlingUnit;

                if (dictionary_Terminal.TryGetValue(guid, out List<VentilationTerminal> ventilationTerminals))
                {
                    ventilationTerminals.Sort((x, y) => x.Guid.CompareTo(y.Guid));
                    context.Dictionary_Terminal[guid] = ventilationTerminals;
                }

                guids_Member.Add(guid);
            }

            guids_Member.Sort();

            int count_Terminal = 0;
            foreach (Guid guid in guids_Member)
            {
                if (context.Dictionary_Terminal.TryGetValue(guid, out List<VentilationTerminal> ventilationTerminals))
                {
                    count_Terminal += ventilationTerminals.Count;
                }
            }

            int count_Terminal_System = 0;
            foreach (List<VentilationTerminal> ventilationTerminals in dictionary_Terminal.Values)
            {
                count_Terminal_System += ventilationTerminals.Count;
            }

            context.Dictionary_ScopeComplete[guid_AirHandlingUnit] = count_Terminal == count_Terminal_System;

            return true;
        }

        /// <summary>
        /// A space's design supply and extract duties - the sum of its terminals of that direction, which
        /// is what a subdivided room means, and never the first of them or the count of them.
        /// <para>
        /// <b>Absent, zero and invalid are three different answers.</b> No terminal of a direction means
        /// that leg is simply not materialised, which is the only legal route to a transfer-only room. A
        /// terminal that exists but states no duty is a refusal - "not established" is not "zero". An
        /// established 0.0 is a valid zero and is materialised as one.
        /// </para>
        /// </summary>
        private static bool TryGetDuties(MechanicalVentilationContext context, Guid guid_Space)
        {
            if (!context.Dictionary_Terminal.TryGetValue(guid_Space, out List<VentilationTerminal> ventilationTerminals))
            {
                return true;
            }

            foreach (FlowClassification flowClassification in new[] { FlowClassification.Supply, FlowClassification.Extract })
            {
                List<VentilationTerminal> ventilationTerminals_Direction = ventilationTerminals.VentilationTerminals(flowClassification) ?? new List<VentilationTerminal>();

                if (ventilationTerminals_Direction.Count == 0)
                {
                    continue;
                }

                foreach (VentilationTerminal ventilationTerminal in ventilationTerminals_Direction)
                {
                    double? designFlowRate_Lps = ventilationTerminal.DesignFlowRate_Lps;

                    if (!designFlowRate_Lps.HasValue || double.IsNaN(designFlowRate_Lps.Value))
                    {
                        context.Refuse(string.Format("Design terminal '{0}' of space '{1}' states no design airflow.", ventilationTerminal.Name, context.Dictionary_Space[guid_Space].Name));
                        return false;
                    }

                    if (designFlowRate_Lps.Value < 0 || double.IsInfinity(designFlowRate_Lps.Value))
                    {
                        context.Refuse(string.Format("Design terminal '{0}' of space '{1}' states a design airflow of {2} l/s.", ventilationTerminal.Name, context.Dictionary_Space[guid_Space].Name, designFlowRate_Lps.Value));
                        return false;
                    }
                }

                //Summed by the analytical authority itself, over the same ordered list, so the reconciliation
                //below compares the same additions in the same order.
                double? duty_Lps = ventilationTerminals_Direction.VentilationTerminalDesignDuty_Lps(flowClassification);

                if (!duty_Lps.HasValue)
                {
                    continue;
                }

                if (flowClassification == FlowClassification.Supply)
                {
                    context.Dictionary_SupplyDuty_Lps[guid_Space] = duty_Lps.Value;
                }
                else
                {
                    context.Dictionary_ExtractDuty_Lps[guid_Space] = duty_Lps.Value;
                }
            }

            return true;
        }

        private static bool TryMaterialiseTransfer(MechanicalVentilationContext context, AdjacencyCluster adjacencyCluster, SystemPlantRoom systemPlantRoom, Dictionary<(Guid, Guid), DesignTransferAirMovement> dictionary_Transfer, (Guid, Guid) key, Dictionary<Guid, AirHandlingUnit> dictionary_AirHandlingUnit)
        {
            bool member_1 = context.Dictionary_SpaceOwner.ContainsKey(key.Item1);
            bool member_2 = context.Dictionary_SpaceOwner.ContainsKey(key.Item2);

            //Neither end is materialised: this movement is somewhere else in the model and is not this
            //materialisation's business.
            if (!member_1 && !member_2)
            {
                return true;
            }

            if (!member_1 || !member_2)
            {
                context.Refuse(string.Format("The design transfer air movement between '{0}' and '{1}' has an endpoint that no materialised system serves.", Name(context, key.Item1), Name(context, key.Item2)));
                return false;
            }

            Guid guid_Owner_1 = context.Dictionary_SpaceOwner[key.Item1];
            Guid guid_Owner_2 = context.Dictionary_SpaceOwner[key.Item2];

            if (guid_Owner_1 != guid_Owner_2)
            {
                context.Refuse(string.Format("The design transfer air movement between '{0}' and '{1}' crosses air handling units '{2}' and '{3}', so which system owns it is ambiguous.", Name(context, key.Item1), Name(context, key.Item2), dictionary_AirHandlingUnit[guid_Owner_1].Name, dictionary_AirHandlingUnit[guid_Owner_2].Name));
                return false;
            }

            double? designFlowRate_Lps = adjacencyCluster.DesignTransferFlowRate_Lps(key.Item1, key.Item2, out Guid guid_From, out Guid guid_To, dictionary_Transfer);

            if (!designFlowRate_Lps.HasValue)
            {
                return true;
            }

            SpaceAirMovement spaceAirMovement = dictionary_Transfer[key].SpaceAirMovement;

            if (guid_From == guid_To)
            {
                context.Refuse(string.Format("The design transfer air movement at '{0}' states the same space at both ends.", Name(context, guid_From)));
                return false;
            }

            if (double.IsNaN(spaceAirMovement.AirFlow) || double.IsInfinity(spaceAirMovement.AirFlow) || spaceAirMovement.AirFlow < 0)
            {
                context.Refuse(string.Format("The design transfer air movement between '{0}' and '{1}' states an airflow of {2} m3/s.", Name(context, key.Item1), Name(context, key.Item2), spaceAirMovement.AirFlow));
                return false;
            }

            AirSystem airSystem = context.Dictionary_AirSystem[guid_Owner_1];

            Guid guid_Connection = context.Guid_Derived(
                "TransferConnection",
                Query.MechanicalVentilationGuidComponent(guid_From),
                Query.MechanicalVentilationGuidComponent(guid_To),
                string.Format("{0}#{1}", Query.MechanicalVentilationGuidComponent(guid_Owner_1), context.Key_EnergyCentre));

            if (context.HasRefusals)
            {
                return false;
            }

            SystemConnection systemConnection = Connection(systemPlantRoom, airSystem, context.Dictionary_SystemSpace[guid_From], 1, context.Dictionary_SystemSpace[guid_To], 0, guid_Connection, designFlowRate_Lps.Value);

            context.Bindings.Add(new MechanicalVentilationBinding(MechanicalVentilationBindingType.TransferConnection, spaceAirMovement.Guid, guid_To, systemConnection.Guid));

            return true;
        }

        private static string Name(MechanicalVentilationContext context, Guid guid_Space)
        {
            return context.Dictionary_Space.TryGetValue(guid_Space, out Space space) ? space.Name : guid_Space.ToString();
        }
    }
}
