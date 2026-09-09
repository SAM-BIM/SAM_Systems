// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// That the materialisation stays linear on a real scheme, proved structurally rather than by a clock.
    /// <para>
    /// <b>No absolute wall-clock threshold anywhere.</b> This repository deleted a stopwatch test once
    /// already and recorded why - it was machine-dependent, and a single long pause on a loaded runner
    /// could flip it. What is asserted instead is what a quadratic or duplicating implementation actually
    /// gets wrong: the object counts, the allocation ratio, and the identities. An implementation that
    /// cloned the plant room per unit, or let one unit replace another, or scanned the model per space,
    /// breaks these long before it breaks any timing.
    /// </para>
    /// <para>
    /// Every case calls the real production entry point against the real shipped <c>MV.json</c>.
    /// </para>
    /// </summary>
    public class MechanicalVentilationScalingTests
    {
        private const int DwellingsPerUnit = 20;

        /// <summary>
        /// <b>Structural exactness at 100, 1 000 and 5 000 spaces.</b> Every count is a closed-form
        /// function of the input, asserted with no tolerance.
        /// </summary>
        [Theory]
        [InlineData(20)]
        [InlineData(200)]
        [InlineData(1000)]
        public void MaterialisationAtScale_ProducesExactlyTheGraphTheDesignImplies(int dwellings)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Scaled(dwellings, DwellingsPerUnit, out MechanicalVentilationTestModel.Scale scale);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            AssertStructure(mechanicalVentilationMaterialisation, scale);
        }

        /// <summary>
        /// <b>The shared non-air plant is not duplicated per unit, at any unit count.</b> One unit, ten and
        /// fifty give the same collection count - an implementation that copied the whole plant room per
        /// unit would multiply it by fifty at the 5 000-space size.
        /// </summary>
        [Fact]
        public void SharedPlant_DoesNotGrowWithUnitCount()
        {
            int count_1 = CollectionCount(20, 20);
            int count_10 = CollectionCount(200, 20);
            int count_50 = CollectionCount(1000, 20);

            //Non-zero first: removing every shared collection would satisfy 0 == 0 == 0 and look like a pass.
            Assert.True(count_1 > 0, "The template's shared collections are missing from the result, so equality here would prove nothing.");

            Assert.Equal(count_1, count_10);
            Assert.Equal(count_1, count_50);
        }

        /// <summary>
        /// <b>Allocation grows linearly, not quadratically.</b> Five times the model allocates on the order
        /// of five times as much; a quadratic path would be nearer twenty-five.
        /// <para>
        /// A ratio, and allocation rather than time: allocation is a property of the code path taken, not
        /// of what else the machine was doing - which is what makes it safe in continuous integration
        /// where a wall-clock budget is not. The warm-up run pays the just-in-time compilation and the
        /// uncached reflection that resolving a type name costs the first time.
        /// </para>
        /// </summary>
        [Fact]
        public void Allocation_GrowsLinearlyWithModelSize()
        {
            //Warm-up: JIT and the uncached assembly walk behind Core.Query.Type.
            Materialise(20);

            long allocated_1000 = Allocated(200);
            long allocated_5000 = Allocated(1000);

            Assert.True(allocated_1000 > 0);

            double ratio = allocated_5000 / (double)allocated_1000;

            Assert.True(ratio < 8.0, string.Format("Allocation ratio between the 1 000 and 5 000 space models is {0:0.00}; linear is about 5 and quadratic about 25.", ratio));
        }

        /// <summary>
        /// <b>The index-usage negative control: a name-scanning implementation cannot pass this.</b>
        /// <para>
        /// The 5 000-space model is materialised twice - once named, once with every space and terminal
        /// name blank - and both runs produce the same counts and <b>the same identities</b>. Every
        /// materialised room is reachable from the lineage by its analytical space guid in one lookup, and
        /// none of them has to be found by name.
        /// </para>
        /// </summary>
        [Fact]
        public void AtFiveThousandSpaces_NothingIsFoundByName()
        {
            AdjacencyCluster adjacencyCluster_Named = MechanicalVentilationTestModel.Scaled(1000, DwellingsPerUnit, out MechanicalVentilationTestModel.Scale scale);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Named = adjacencyCluster_Named.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_Named);
            AssertStructure(mechanicalVentilationMaterialisation_Named, scale);

            //Every room is reachable from the lineage by guid, in one lookup.
            Dictionary<Guid, Guid> dictionary = [];

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation_Named, MechanicalVentilationBindingType.SystemSpace))
            {
                dictionary[mechanicalVentilationBinding.Guid_Analytical] = mechanicalVentilationBinding.Guid_Systems;
            }

            Assert.Equal(scale.Spaces, dictionary.Count);

            foreach (Space space in adjacencyCluster_Named.GetSpaces())
            {
                Assert.True(dictionary.ContainsKey(space.Guid));
            }

            //The same model with every name blank materialises identically.
            AdjacencyCluster adjacencyCluster_Unnamed = Rename(adjacencyCluster_Named);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Unnamed = adjacencyCluster_Unnamed.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_Unnamed);
            AssertStructure(mechanicalVentilationMaterialisation_Unnamed, scale);

            Assert.Equal(
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_Named),
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_Unnamed));
        }

        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// The same model with every space and terminal name replaced by an empty string, and every guid
        /// kept - so the two runs differ only in the names. The unit keeps its name, which is the model's
        /// own binding and the only name read anywhere.
        /// </summary>
        private static AdjacencyCluster Rename(AdjacencyCluster adjacencyCluster)
        {
            AdjacencyCluster result = new();

            foreach (AirHandlingUnit airHandlingUnit in adjacencyCluster.GetObjects<AirHandlingUnit>())
            {
                result.AddObject(airHandlingUnit);
            }

            Dictionary<Guid, Space> dictionary_Space = [];

            foreach (Space space in adjacencyCluster.GetSpaces())
            {
                Space space_Renamed = new(space.Guid, string.Empty, space.Location);

                space_Renamed.SetValue(SpaceParameter.Area, MechanicalVentilationTestModel.Area);
                space_Renamed.SetValue(SpaceParameter.Volume, MechanicalVentilationTestModel.Volume);

                result.AddObject(space_Renamed);

                dictionary_Space[space.Guid] = space_Renamed;
            }

            foreach (VentilationSystem ventilationSystem in adjacencyCluster.GetObjects<VentilationSystem>())
            {
                result.AddObject(ventilationSystem);

                foreach (Space space in adjacencyCluster.GetRelatedObjects<Space>(ventilationSystem) ?? [])
                {
                    result.AddRelation(ventilationSystem, dictionary_Space[space.Guid]);
                }

                foreach (VentilationTerminal ventilationTerminal in adjacencyCluster.VentilationTerminals(ventilationSystem) ?? [])
                {
                    VentilationTerminal ventilationTerminal_Renamed = new(ventilationTerminal.Guid, string.Empty, ventilationTerminal.FlowClassification, ventilationTerminal.DesignFlowRate_Lps);

                    result.AddObject(ventilationTerminal_Renamed);
                    result.AddRelation(ventilationSystem, ventilationTerminal_Renamed);

                    foreach (Space space in adjacencyCluster.GetRelatedObjects<Space>(ventilationTerminal) ?? [])
                    {
                        result.AddRelation(ventilationTerminal_Renamed, dictionary_Space[space.Guid]);
                    }
                }
            }

            foreach (SpaceAirMovement spaceAirMovement in adjacencyCluster.GetObjects<SpaceAirMovement>())
            {
                result.AddObject(spaceAirMovement);

                foreach (Space space in adjacencyCluster.GetRelatedObjects<Space>(spaceAirMovement) ?? [])
                {
                    result.AddRelation(spaceAirMovement, dictionary_Space[space.Guid]);
                }
            }

            return result;
        }

        private static void AssertStructure(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, MechanicalVentilationTestModel.Scale scale)
        {
            //One plant room, whatever the unit count.
            Assert.Single(mechanicalVentilationMaterialisation.SystemEnergyCentre.GetSystemPlantRooms());

            Assert.Equal(scale.AirHandlingUnits, MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation).Count);
            Assert.Equal(scale.Spaces, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation).Count);

            Assert.Equal(scale.SpacesWithSupply, MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection).Count);
            Assert.Equal(scale.SpacesWithExtract, MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.ExtractConnection).Count);
            Assert.Equal(scale.Movements, MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.TransferConnection).Count);
            Assert.Equal(scale.AirHandlingUnits, MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.AirSystem).Count);
            Assert.Equal(scale.Spaces, MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SystemSpace).Count);

            //One row per CONTRIBUTING TERMINAL, not one per connection - the N:1 lineage.
            Assert.Equal(
                scale.Spaces + scale.AirHandlingUnits + scale.Terminals + scale.Movements,
                mechanicalVentilationMaterialisation.Bindings.Count);

            //Every leg carries a design flow rate, and there are exactly as many as the design implies.
            Assert.Equal(
                scale.SpacesWithSupply + scale.SpacesWithExtract + scale.Movements,
                MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation).Count);
        }

        private static MechanicalVentilationMaterialisation Materialise(int dwellings)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Scaled(dwellings, DwellingsPerUnit, out MechanicalVentilationTestModel.Scale _);

            return adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
        }

        /// <summary>
        /// What one materialisation allocates, with the model and the template built outside the
        /// measurement so only the materialisation itself is counted.
        /// </summary>
        private static long Allocated(int dwellings)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Scaled(dwellings, DwellingsPerUnit, out MechanicalVentilationTestModel.Scale _);

            SystemEnergyCentre systemEnergyCentre = MechanicalVentilationTestModel.Template();

            long allocated = GC.GetAllocatedBytesForCurrentThread();

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(systemEnergyCentre);

            long result = GC.GetAllocatedBytesForCurrentThread() - allocated;

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            return result;
        }

        private static int CollectionCount(int dwellings, int dwellingsPerUnit)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Scaled(dwellings, dwellingsPerUnit, out MechanicalVentilationTestModel.Scale _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            int result = 0;

            foreach (ISystemComponent systemComponent in MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation).GetSystemComponents() ?? [])
            {
                if (systemComponent is ISystemCollection)
                {
                    result++;
                }
            }

            return result;
        }
    }
}
