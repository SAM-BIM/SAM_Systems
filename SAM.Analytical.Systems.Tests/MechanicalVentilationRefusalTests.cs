// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Core.Systems;
using System;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// What the materialisation refuses, and that a refusal never leaves a graph behind.
    /// <para>
    /// <b>Fail closed means no partial-success graph.</b> Every case here asserts three things together:
    /// the energy centre is null, the refusals name the offending object, and the notes claim nothing.
    /// A refusal that still returned a half-built graph would let a caller simulate a dwelling whose
    /// ventilation the design does not actually state.
    /// </para>
    /// </summary>
    public class MechanicalVentilationRefusalTests
    {
        /// <summary>Nothing to read, or nothing to build on.</summary>
        [Fact]
        public void MissingInput_FailsClosed()
        {
            AssertRefused(((AdjacencyCluster)null).MechanicalVentilation(MechanicalVentilationTestModel.Template()), "model");
            AssertRefused(MechanicalVentilationTestModel.Model().MechanicalVentilation(null), "template");
        }

        /// <summary>
        /// A system that names no unit, and a system that names one the model does not contain. In both the
        /// physical machine is unknown, and guessing which one it is would decide a building's air
        /// distribution by chance.
        /// </summary>
        [Theory]
        [InlineData(null, "names no air handling unit")]
        [InlineData("Somewhere else", "which the model does not contain")]
        public void UnresolvableAirHandlingUnit_FailsClosed(string name_Supply, string expected)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", name_Supply, name_Supply, out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 30.0);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), expected);
        }

        /// <summary>
        /// A terminal related to no space, and a terminal related to two. The room it serves is missing or
        /// ambiguous, and its duty cannot be attributed to a room without inventing an attribution.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void TerminalRelatedToOtherThanOneSpace_FailsClosed(int count)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space_1 = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom 1");
            Space space_2 = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom 2");

            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space_1);

            VentilationTerminal ventilationTerminal = new("Supply", FlowClassification.Supply, 30.0);

            adjacencyCluster.AddObject(ventilationTerminal);
            adjacencyCluster.AddRelation(ventilationSystem, ventilationTerminal);

            if (count >= 1)
            {
                adjacencyCluster.AddRelation(ventilationTerminal, space_1);
            }

            if (count >= 2)
            {
                adjacencyCluster.AddRelation(ventilationTerminal, space_2);
            }

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), "is related to");
        }

        /// <summary>
        /// <b>Two units answering to one name refuse.</b> The legacy source binding is a name, and where
        /// two physical machines answer to it there is no way to tell which one a system serves from -
        /// SAM's own accessor silently takes the first, which would merge two machines into one air system.
        /// <para>
        /// This is the counterpart of the space case: identical SPACE names are fine and prove guid
        /// identity; an ambiguous UNIT name is a refusal. See
        /// <c>MechanicalVentilationDeterminismTests.DuplicateSpaceNames_MaterialiseAsDistinctRooms</c>.
        /// </para>
        /// </summary>
        [Fact]
        public void DuplicateAirHandlingUnitNames_FailClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _, string.Empty, adjacencyCluster);

            //A second, entirely different physical unit that answers to the same name.
            AirHandlingUnit airHandlingUnit = Analytical.Create.AirHandlingUnit("AHU");
            adjacencyCluster.AddObject(airHandlingUnit);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), "units in the model answer to");
        }

        /// <summary>Supply and exhaust naming different units: which physical machine it is has two answers.</summary>
        [Fact]
        public void SupplyAndExhaustNamingDifferentUnits_FailClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            AirHandlingUnit airHandlingUnit_Exhaust = Analytical.Create.AirHandlingUnit("AHU Exhaust");
            adjacencyCluster.AddObject(airHandlingUnit_Exhaust);

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", "AHU", "AHU Exhaust", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 30.0);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), "for supply and");
        }

        /// <summary>
        /// One space served by two units would be ventilated twice, and the two air systems would each
        /// claim it. <b>The units are never merged into one air system to resolve it.</b>
        /// </summary>
        [Fact]
        public void SpaceServedByTwoUnits_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem_1 = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU 1", out AirHandlingUnit _);
            VentilationSystem ventilationSystem_2 = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU 2", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");

            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem_1, space);
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem_2, space);

            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem_1, space, FlowClassification.Supply, 30.0);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem_2, space, FlowClassification.Supply, 20.0);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), "is served by air handling units");
        }

        /// <summary>
        /// An authored transfer movement between rooms served by two different units fails closed.
        /// <para>
        /// <b>It refuses at membership rather than at the transfer, and that is the earlier and more
        /// specific answer.</b> Transfer-connected rooms are members of the same system - that is what
        /// brings a hall into the system that ventilates the rooms either side of it - so a movement
        /// across two units makes a room reachable from both, and the ambiguous-ownership rule catches it
        /// before any leg is built. The cross-unit rule in the transfer pass remains as depth behind it.
        /// Either way the units are never merged and no graph is returned.
        /// </para>
        /// </summary>
        [Fact]
        public void TransferCrossingTwoUnits_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem_1 = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU 1", out AirHandlingUnit _);
            VentilationSystem ventilationSystem_2 = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU 2", out AirHandlingUnit _);

            Space space_1 = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom 1");
            Space space_2 = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom 2");

            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem_1, space_1);
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem_2, space_2);

            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem_1, space_1, FlowClassification.Supply, 30.0);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem_2, space_2, FlowClassification.Extract, 30.0);

            MechanicalVentilationTestModel.Transfer(adjacencyCluster, space_1, space_2, 0.03);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), "is served by air handling units");
        }

        /// <summary>
        /// A transfer endpoint that no materialised system serves - here because the caller's scope
        /// excluded it. Materialising the leg anyway would hang it off a room that is not in the graph.
        /// </summary>
        [Fact]
        public void TransferEndpointOutsideScope_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space_Bedroom = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            Space space_Hall = MechanicalVentilationTestModel.Space(adjacencyCluster, "Hall");

            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space_Bedroom);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space_Bedroom, FlowClassification.Supply, 30.0);
            MechanicalVentilationTestModel.Transfer(adjacencyCluster, space_Bedroom, space_Hall, 0.03);

            //The hall is deliberately left out of scope while the movement that crosses it is not.
            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, [space_Bedroom]), "no materialised system serves");
        }

        /// <summary>
        /// <b>Missing, invalid and valid-zero are three different answers.</b> A terminal that states no
        /// duty, one that states NaN, a negative one and an infinite one all refuse - none of them is a
        /// design airflow. The positive control at the end is the point of the whole rule: an established
        /// 0.0 is a real design statement and materialises as a 0.0 leg.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData(double.NaN)]
        [InlineData(-5.0)]
        [InlineData(double.PositiveInfinity)]
        public void InvalidDesignAirflow_FailsClosed(double? designFlowRate_Lps)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, designFlowRate_Lps);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), "design airflow");
        }

        /// <summary>The positive control: an established zero is valid and is materialised as zero.</summary>
        [Fact]
        public void EstablishedZeroDesignAirflow_IsMaterialisedAsZero()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 0.0);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            //A leg exists and carries zero - which is not the same as no leg at all.
            System.Collections.Generic.List<MechanicalVentilationBinding> bindings = MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection);

            Assert.Single(bindings);
            Assert.Equal(0.0, MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation)[bindings[0].Guid_Systems]);
            Assert.Equal(0.0, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation)[0].FlowRate.Value);
        }

        /// <summary>A transfer movement stating a non-finite airflow is not a design statement.</summary>
        [Fact]
        public void NonFiniteTransferAirflow_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space_Bedroom = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            Space space_Hall = MechanicalVentilationTestModel.Space(adjacencyCluster, "Hall");

            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space_Bedroom);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space_Bedroom, FlowClassification.Supply, 30.0);
            MechanicalVentilationTestModel.Transfer(adjacencyCluster, space_Bedroom, space_Hall, double.NaN);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template()), "states an airflow of");
        }

        /// <summary>An operating schedule with no name is unreachable: components reference schedules by name.</summary>
        [Fact]
        public void UnnamedSchedule_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationSettings mechanicalVentilationSettings = new() { Schedule = new YearlySchedule((string)null) };

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), mechanicalVentilationSettings), "states no name");
        }

        /// <summary>An operating schedule with non-finite hours does not cover the period being materialised.</summary>
        [Fact]
        public void NonFiniteSchedule_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            YearlySchedule yearlySchedule = new("Operating");

            double[] values = new double[8760];
            values[17] = double.NaN;

            yearlySchedule.Values = values;

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { Schedule = yearlySchedule }), "non-finite value");
        }

        /// <summary>
        /// A template that does not state one plant room, or does not state one air system in it. Which one
        /// the ventilation belongs in, or which one is the prototype, would be a guess.
        /// </summary>
        [Fact]
        public void AmbiguousTemplate_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            //No plant room at all.
            AssertRefused(adjacencyCluster.MechanicalVentilation(new SystemEnergyCentre("Empty")), "plant rooms");

            //Two, from the shipped one.
            SystemEnergyCentre systemEnergyCentre = MechanicalVentilationTestModel.Template();
            SystemPlantRoom systemPlantRoom = systemEnergyCentre.GetSystemPlantRooms()[0];

            systemEnergyCentre.Add(new SystemPlantRoom(Guid.NewGuid(), systemPlantRoom));

            AssertRefused(adjacencyCluster.MechanicalVentilation(systemEnergyCentre), "plant rooms");
        }

        private static void AssertRefused(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, string expected)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);

            //No partial-success graph, and nothing claimed.
            Assert.Null(mechanicalVentilationMaterialisation.SystemEnergyCentre);
            Assert.False(mechanicalVentilationMaterialisation.IsMaterialised);
            Assert.Empty(mechanicalVentilationMaterialisation.Notes);
            Assert.Empty(mechanicalVentilationMaterialisation.Bindings);

            Assert.NotEmpty(mechanicalVentilationMaterialisation.Refusals);

            Assert.Contains(mechanicalVentilationMaterialisation.Refusals, x => x.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
    }
}
