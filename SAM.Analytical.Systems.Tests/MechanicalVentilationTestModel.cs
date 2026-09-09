// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Core;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.IO;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// The generated analytical models the materialisation tests work from, and the walk-up to the shipped
    /// <c>MV.json</c>.
    /// <para>
    /// <b>Generated, not fixtures.</b> Every model here is built from <c>SAM.Analytical</c>'s own public
    /// constructors, so a test states exactly the design it is asserting about - a room with three
    /// subdivided supply terminals, a hall with none, two units that must not merge - rather than
    /// depending on a checked-in file whose contents have to be read to know what a test means.
    /// </para>
    /// <para>
    /// <b>Shared deliberately.</b> The repository has no fixture infrastructure only because nothing has
    /// needed one; five test classes need the same builders, and duplicating them five times would let
    /// them drift.
    /// </para>
    /// </summary>
    internal static class MechanicalVentilationTestModel
    {
        internal const double Area = 12.0;
        internal const double Volume = 30.0;

        /// <summary>
        /// The shipped <c>MV.json</c>, read from the repository's own resources by walking up from the test
        /// output - the mechanism <c>SystemEnergyCentreCapabilityTests</c> already uses for it, and the only
        /// way to read what is SHIPPED rather than what happened to be copied.
        /// </summary>
        internal static SystemEnergyCentre Template()
        {
            return Analytical.Systems.Query.SystemEnergyCentre(PathTemplate());
        }

        internal static string PathTemplate()
        {
            return Path.Combine(DirectoryResources(), "MV.json");
        }

        internal static string DirectoryResources()
        {
            DirectoryInfo directoryInfo = new(AppContext.BaseDirectory);

            while (directoryInfo != null)
            {
                string result = Path.Combine(directoryInfo.FullName, "files", "resources", "Analytical", "Systems", "SystemEnergyCentre");

                if (Directory.Exists(result) && Directory.Exists(Path.Combine(directoryInfo.FullName, "SAM_Systems", "SAM.Analytical.Systems")))
                {
                    return result;
                }

                directoryInfo = directoryInfo.Parent;
            }

            throw new DirectoryNotFoundException("The SAM_Systems repository root was not found above " + AppContext.BaseDirectory);
        }

        /// <summary>An empty model with no unit and no system.</summary>
        internal static AdjacencyCluster Model()
        {
            return new AdjacencyCluster();
        }

        /// <summary>One space, carrying an area and a volume so a materialised room can state them.</summary>
        internal static Space Space(AdjacencyCluster adjacencyCluster, string name)
        {
            Space result = new(name, new Geometry.Spatial.Point3D(0, 0, 0));

            result.SetValue(SpaceParameter.Area, Area);
            result.SetValue(SpaceParameter.Volume, Volume);

            adjacencyCluster.AddObject(result);

            return result;
        }

        /// <summary>
        /// One physical air handling unit and the ventilation system that serves from it, bound the way
        /// <c>AddPartOBaseMVHRSystem</c> binds them - through <c>SupplyUnitName</c>/<c>ExhaustUnitName</c>,
        /// which is the model's own pre-existing binding and the one the materialisation resolves once.
        /// </summary>
        internal static VentilationSystem VentilationSystem(AdjacencyCluster adjacencyCluster, string name_AirHandlingUnit, out AirHandlingUnit airHandlingUnit)
        {
            return VentilationSystem(adjacencyCluster, name_AirHandlingUnit, name_AirHandlingUnit, name_AirHandlingUnit, out airHandlingUnit);
        }

        /// <summary>
        /// The same overload, given a unit that already exists - so two models a test compares can share
        /// one unit identity, which every derived identity below it depends on.
        /// </summary>
        internal static VentilationSystem VentilationSystem(AdjacencyCluster adjacencyCluster, AirHandlingUnit airHandlingUnit_Existing, out AirHandlingUnit airHandlingUnit)
        {
            return VentilationSystem(adjacencyCluster, airHandlingUnit_Existing.Name, airHandlingUnit_Existing.Name, airHandlingUnit_Existing.Name, out airHandlingUnit, airHandlingUnit_Existing);
        }

        internal static VentilationSystem VentilationSystem(AdjacencyCluster adjacencyCluster, string name_AirHandlingUnit, string name_Supply, string name_Exhaust, out AirHandlingUnit airHandlingUnit, AirHandlingUnit airHandlingUnit_Existing = null)
        {
            airHandlingUnit = airHandlingUnit_Existing ?? (name_AirHandlingUnit == null ? null : Analytical.Create.AirHandlingUnit(name_AirHandlingUnit));

            if (airHandlingUnit != null)
            {
                airHandlingUnit.SummerSupplyTemperature = double.NaN;
                airHandlingUnit.WinterSupplyTemperature = double.NaN;

                adjacencyCluster.AddObject(airHandlingUnit);
            }

            VentilationSystemType ventilationSystemType = Analytical.Create.VentilationSystemType(Guid.NewGuid(), "MVHR", "Continuous mechanical supply and extract with heat recovery.");

            VentilationSystem result = Analytical.Create.MechanicalSystem(ventilationSystemType, null, 1) as VentilationSystem;

            if (name_Supply != null)
            {
                result.SetValue(VentilationSystemParameter.SupplyUnitName, name_Supply);
            }

            if (name_Exhaust != null)
            {
                result.SetValue(VentilationSystemParameter.ExhaustUnitName, name_Exhaust);
            }

            adjacencyCluster.AddObject(result);

            return result;
        }

        /// <summary>Relates a space to the system that serves it, exactly as the Part O path does.</summary>
        internal static void Serve(AdjacencyCluster adjacencyCluster, VentilationSystem ventilationSystem, Space space)
        {
            adjacencyCluster.AddRelation(ventilationSystem, space);
        }

        /// <summary>
        /// One design terminal of one direction, related to both the system that owns it and the space it
        /// serves. A space may carry any number of these in either direction - the design duty is their sum.
        /// </summary>
        internal static VentilationTerminal Terminal(AdjacencyCluster adjacencyCluster, VentilationSystem ventilationSystem, Space space, FlowClassification flowClassification, double? designFlowRate_Lps, string name = null)
        {
            VentilationTerminal result = new(name ?? string.Format("{0} {1}", space.Name, flowClassification), flowClassification, designFlowRate_Lps);

            adjacencyCluster.AddObject(result);
            adjacencyCluster.AddRelation(ventilationSystem, result);
            adjacencyCluster.AddRelation(result, space);

            return result;
        }

        /// <summary>
        /// One authored space-to-space transfer air movement, written in the direction the air travels and
        /// related to the downstream space only - exactly as <c>AddPartFTransferAirMovements</c> writes it.
        /// </summary>
        internal static SpaceAirMovement Transfer(AdjacencyCluster adjacencyCluster, Space space_From, Space space_To, double airFlow_M3S)
        {
            SpaceAirMovement result = new(
                string.Format("{0} -> {1}", space_From.Name, space_To.Name),
                airFlow_M3S,
                new ObjectReference(space_From).ToString(),
                new ObjectReference(space_To).ToString());

            adjacencyCluster.AddObject(result);
            adjacencyCluster.AddRelation(result, space_To);

            return result;
        }

        /// <summary>
        /// The canonical single-unit dwelling: two habitable rooms with supply, a hall with no terminals at
        /// all, two wet rooms with extract, and the four movements that route the air
        /// <c>habitable -&gt; hall -&gt; wet room</c> - the branching hall that a one-connection-per-connector
        /// model cannot express and that direct connection construction handles.
        /// </summary>
        internal static AdjacencyCluster Dwelling(out AirHandlingUnit airHandlingUnit, string suffix = "", AdjacencyCluster adjacencyCluster = null)
        {
            adjacencyCluster ??= new AdjacencyCluster();

            VentilationSystem ventilationSystem = VentilationSystem(adjacencyCluster, "AHU" + suffix, out airHandlingUnit);

            Space space_Bedroom = Space(adjacencyCluster, "Bedroom" + suffix);
            Space space_Living = Space(adjacencyCluster, "Living" + suffix);
            Space space_Hall = Space(adjacencyCluster, "Hall" + suffix);
            Space space_Bathroom = Space(adjacencyCluster, "Bathroom" + suffix);
            Space space_Kitchen = Space(adjacencyCluster, "Kitchen" + suffix);

            Serve(adjacencyCluster, ventilationSystem, space_Bedroom);
            Serve(adjacencyCluster, ventilationSystem, space_Living);
            Serve(adjacencyCluster, ventilationSystem, space_Bathroom);
            Serve(adjacencyCluster, ventilationSystem, space_Kitchen);

            Terminal(adjacencyCluster, ventilationSystem, space_Bedroom, FlowClassification.Supply, 13.0);
            Terminal(adjacencyCluster, ventilationSystem, space_Living, FlowClassification.Supply, 12.0);
            Terminal(adjacencyCluster, ventilationSystem, space_Bathroom, FlowClassification.Extract, 15.0);
            Terminal(adjacencyCluster, ventilationSystem, space_Kitchen, FlowClassification.Extract, 10.0);

            Transfer(adjacencyCluster, space_Bedroom, space_Hall, 0.0065);
            Transfer(adjacencyCluster, space_Living, space_Hall, 0.006);
            Transfer(adjacencyCluster, space_Hall, space_Bathroom, 0.0075);
            Transfer(adjacencyCluster, space_Hall, space_Kitchen, 0.005);

            return adjacencyCluster;
        }

        /// <summary>The shape of a generated scaling model, so the assertions are closed-form.</summary>
        internal readonly struct Scale
        {
            internal int Spaces { get; }
            internal int AirHandlingUnits { get; }
            internal int Terminals { get; }
            internal int Movements { get; }
            internal int SpacesWithSupply { get; }
            internal int SpacesWithExtract { get; }

            internal Scale(int dwellings, int dwellingsPerUnit)
            {
                Spaces = dwellings * 5;
                AirHandlingUnits = (dwellings + dwellingsPerUnit - 1) / dwellingsPerUnit;
                Terminals = dwellings * 4;
                Movements = dwellings * 4;
                SpacesWithSupply = dwellings * 2;
                SpacesWithExtract = dwellings * 2;
            }
        }

        /// <summary>
        /// A realistic multi-dwelling model at a given size: per dwelling two habitable rooms with supply,
        /// a hall with no terminals at all, two wet rooms with extract, and the four movements that route
        /// <c>habitable -&gt; hall -&gt; wet room</c> - the hall branching two in and two out.
        /// <para>
        /// Several dwellings share one physical unit, so a large model exercises many air systems rather
        /// than one enormous one - which is what a real scheme looks like and what the per-unit re-key has
        /// to stay linear in.
        /// </para>
        /// </summary>
        internal static AdjacencyCluster Scaled(int dwellings, int dwellingsPerUnit, out Scale scale, bool blankNames = false)
        {
            scale = new Scale(dwellings, dwellingsPerUnit);

            AdjacencyCluster result = new();

            VentilationSystem ventilationSystem = null;

            for (int i = 0; i < dwellings; i++)
            {
                if (i % dwellingsPerUnit == 0)
                {
                    ventilationSystem = VentilationSystem(result, string.Format("AHU {0}", i / dwellingsPerUnit), out AirHandlingUnit _);
                }

                Space space_Bedroom = Scaled(result, blankNames, "Bedroom", i);
                Space space_Living = Scaled(result, blankNames, "Living", i);
                Space space_Hall = Scaled(result, blankNames, "Hall", i);
                Space space_Bathroom = Scaled(result, blankNames, "Bathroom", i);
                Space space_Kitchen = Scaled(result, blankNames, "Kitchen", i);

                Serve(result, ventilationSystem, space_Bedroom);
                Serve(result, ventilationSystem, space_Living);
                Serve(result, ventilationSystem, space_Bathroom);
                Serve(result, ventilationSystem, space_Kitchen);

                Terminal(result, ventilationSystem, space_Bedroom, FlowClassification.Supply, 13.0, blankNames ? string.Empty : null);
                Terminal(result, ventilationSystem, space_Living, FlowClassification.Supply, 12.0, blankNames ? string.Empty : null);
                Terminal(result, ventilationSystem, space_Bathroom, FlowClassification.Extract, 15.0, blankNames ? string.Empty : null);
                Terminal(result, ventilationSystem, space_Kitchen, FlowClassification.Extract, 10.0, blankNames ? string.Empty : null);

                Transfer(result, space_Bedroom, space_Hall, 0.0065);
                Transfer(result, space_Living, space_Hall, 0.006);
                Transfer(result, space_Hall, space_Bathroom, 0.0075);
                Transfer(result, space_Hall, space_Kitchen, 0.005);
            }

            return result;
        }

        private static Space Scaled(AdjacencyCluster adjacencyCluster, bool blankNames, string name, int index)
        {
            return Space(adjacencyCluster, blankNames ? string.Empty : string.Format("{0} {1}", name, index));
        }

        /// <summary>The materialised rooms of a result, by their <c>SystemSpace</c> guid.</summary>
        internal static List<SystemSpace> SystemSpaces(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            List<SystemSpace> result = [];

            foreach (ISystemComponent systemComponent in PlantRoom(mechanicalVentilationMaterialisation).GetSystemComponents() ?? [])
            {
                if (systemComponent is SystemSpace systemSpace)
                {
                    result.Add(systemSpace);
                }
            }

            return result;
        }

        internal static SystemPlantRoom PlantRoom(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            List<SystemPlantRoom> systemPlantRooms = mechanicalVentilationMaterialisation.SystemEnergyCentre.GetSystemPlantRooms();

            return systemPlantRooms.Count == 1 ? systemPlantRooms[0] : throw new InvalidOperationException(string.Format("{0} plant rooms.", systemPlantRooms.Count));
        }

        internal static List<AirSystem> AirSystems(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            return PlantRoom(mechanicalVentilationMaterialisation).GetSystems<AirSystem>() ?? [];
        }

        /// <summary>Every connection of a result that carries a design flow rate, by guid.</summary>
        internal static Dictionary<Guid, double> DesignFlowRates(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            Dictionary<Guid, double> result = [];

            foreach (ISystemConnection systemConnection in PlantRoom(mechanicalVentilationMaterialisation).GetSystemConnections() ?? [])
            {
                if (systemConnection is SAMObject sAMObject && sAMObject.TryGetValue(SystemConnectionParameter.DesignFlowRate, out double designFlowRate_Lps))
                {
                    result[sAMObject.Guid] = designFlowRate_Lps;
                }
            }

            return result;
        }

        /// <summary>The bindings of one kind, in the order the result states them.</summary>
        internal static List<MechanicalVentilationBinding> Bindings(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, MechanicalVentilationBindingType mechanicalVentilationBindingType)
        {
            List<MechanicalVentilationBinding> result = [];

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in mechanicalVentilationMaterialisation.Bindings)
            {
                if (mechanicalVentilationBinding.BindingType == mechanicalVentilationBindingType)
                {
                    result.Add(mechanicalVentilationBinding);
                }
            }

            return result;
        }

        /// <summary>The materialised room of one analytical space, found through the lineage and never by name.</summary>
        internal static SystemSpace SystemSpace(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, Space space)
        {
            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SystemSpace))
            {
                if (mechanicalVentilationBinding.Guid_Analytical != space.Guid)
                {
                    continue;
                }

                foreach (SystemSpace systemSpace in SystemSpaces(mechanicalVentilationMaterialisation))
                {
                    if (systemSpace.Guid == mechanicalVentilationBinding.Guid_Systems)
                    {
                        return systemSpace;
                    }
                }
            }

            return null;
        }

        /// <summary>The whole result as JSON, for the byte-level isolation comparisons.</summary>
        internal static string Json(IJSAMObject jSAMObject)
        {
            return jSAMObject?.ToJsonObject()?.ToJsonString();
        }
    }
}
