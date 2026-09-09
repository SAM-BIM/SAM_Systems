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
    /// What the materialisation produces from a design, and what it must never read to produce it.
    /// <para>
    /// Every test here calls the real production entry point <c>Create.MechanicalVentilation</c> against
    /// the real shipped <c>MV.json</c>. Nothing is asserted through an internal helper.
    /// </para>
    /// </summary>
    public class MechanicalVentilationMaterialisationTests
    {
        /// <summary>
        /// A supply-only room: one supply leg carrying its design duty, and <b>no extract leg at all</b>.
        /// An absent direction is not a zero-flow connection - a room with no extract terminal has no
        /// extract, which is a different statement from extracting nothing.
        /// </summary>
        [Fact]
        public void SupplyOnlyRoom_MaterialisesOneSupplyLegAndNoExtractLeg()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 30.0);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Single(MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation));

            List<SystemSpace> systemSpaces = MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation);

            Assert.Single(systemSpaces);
            Assert.Equal(30.0, systemSpaces[0].FlowRate.Value);
            Assert.Equal(MechanicalVentilationTestModel.Area, systemSpaces[0].Area);
            Assert.Equal(MechanicalVentilationTestModel.Volume, systemSpaces[0].Volume);

            Assert.Single(MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection));
            Assert.Empty(MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.ExtractConnection));

            Dictionary<Guid, double> dictionary = MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation);

            Assert.Single(dictionary);
            Assert.Equal(30.0, dictionary[MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection)[0].Guid_Systems]);
        }

        /// <summary>
        /// An extract-only room: an extract leg at its own magnitude, no supply leg, and a design flow of
        /// 0.0 on the room itself because there is no supply terminal to state one.
        /// </summary>
        [Fact]
        public void ExtractOnlyRoom_MaterialisesOneExtractLegAndNoSupplyLeg()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bathroom");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Extract, 25.0);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            List<SystemSpace> systemSpaces = MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation);

            Assert.Single(systemSpaces);
            Assert.Equal(0.0, systemSpaces[0].FlowRate.Value);

            Assert.Empty(MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection));

            List<MechanicalVentilationBinding> bindings = MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.ExtractConnection);

            Assert.Single(bindings);
            Assert.Equal(25.0, MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation)[bindings[0].Guid_Systems]);
        }

        /// <summary>
        /// A room with both directions: two legs, each carrying its own magnitude, and the room's design
        /// flow taken from the supply side.
        /// </summary>
        [Fact]
        public void SupplyAndExtractRoom_MaterialisesBothLegsAtTheirOwnMagnitudes()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Studio");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 30.0);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Extract, 25.0);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Equal(30.0, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation)[0].FlowRate.Value);

            Dictionary<Guid, double> dictionary = MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation);

            Assert.Equal(2, dictionary.Count);
            Assert.Equal(30.0, dictionary[MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection)[0].Guid_Systems]);
            Assert.Equal(25.0, dictionary[MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.ExtractConnection)[0].Guid_Systems]);
        }

        /// <summary>
        /// <b>A transfer-only room is materialised.</b> A hall with neither a supply nor an extract terminal
        /// is part of the system because the authored topology routes air through it - not because of its
        /// name, and not because of a zero flow value. Dropping it would break the transfer chain that
        /// carries the dwelling's air from its habitable rooms to its wet rooms.
        /// </summary>
        [Fact]
        public void TransferOnlyRoom_IsMaterialisedWithNeitherLeg()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space_Bedroom = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            Space space_Hall = MechanicalVentilationTestModel.Space(adjacencyCluster, "Hall");

            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space_Bedroom);
            MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space_Bedroom, FlowClassification.Supply, 30.0);
            MechanicalVentilationTestModel.Transfer(adjacencyCluster, space_Bedroom, space_Hall, 0.03);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Equal(2, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation).Count);

            SystemSpace systemSpace_Hall = MechanicalVentilationTestModel.SystemSpace(mechanicalVentilationMaterialisation, space_Hall);

            Assert.NotNull(systemSpace_Hall);

            //It has neither leg of its own, and its own design flow is a zero it never claimed to have a
            //supply terminal for.
            Assert.Equal(0.0, systemSpace_Hall.FlowRate.Value);

            Assert.Single(MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection));
            Assert.Empty(MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.ExtractConnection));
            Assert.Single(MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.TransferConnection));
        }

        /// <summary>
        /// An explicit transfer chain, and a hall that branches two in and two out - the topology a
        /// one-connection-per-connector model cannot express, and which is why the legs are constructed
        /// directly rather than through <c>Connect</c>.
        /// </summary>
        [Fact]
        public void BranchingTransferTopology_MaterialisesEveryAuthoredMovementAtItsOwnFlow()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            List<MechanicalVentilationBinding> bindings = MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.TransferConnection);

            //Four authored movements, four transfer legs - the hall carrying two in and two out.
            Assert.Equal(4, bindings.Count);

            Dictionary<Guid, double> dictionary = MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation);

            List<double> designFlowRates = [];

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in bindings)
            {
                designFlowRates.Add(dictionary[mechanicalVentilationBinding.Guid_Systems]);
            }

            designFlowRates.Sort();

            //m3/s at the source, litres per second on the connection - and no other conversion anywhere.
            Assert.Equal([5.0, 6.0, 6.5, 7.5], designFlowRates);
        }

        /// <summary>
        /// Several rooms on one unit: one air system, one room each, and every supply leg hanging off the
        /// one template supply attachment. That many-to-one is what the connector model cannot express and
        /// what the shipped template and <c>Query.Duplicate</c> already produce.
        /// </summary>
        [Fact]
        public void SeveralRoomsOnOneUnit_ShareOneAirSystemAndOneSupplyAttachment()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            for (int i = 0; i < 5; i++)
            {
                Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, string.Format("Room {0}", i));
                MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
                MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 10.0 + i);
            }

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Single(MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation));
            Assert.Equal(5, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation).Count);

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);

            //Every supply leg names the same component at its non-room end.
            HashSet<Guid> guids_Attachment = [];

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection))
            {
                ISystemConnection systemConnection = Connection(systemPlantRoom, mechanicalVentilationBinding.Guid_Systems);

                foreach (Core.ObjectReference objectReference in systemConnection.ObjectReferences)
                {
                    if (Guid.TryParse(objectReference.Reference?.ToString(), out Guid guid) && !IsSystemSpace(systemPlantRoom, guid))
                    {
                        guids_Attachment.Add(guid);
                    }
                }
            }

            Assert.Single(guids_Attachment);
        }

        /// <summary>
        /// <b>Two units are two air systems in ONE plant room, and neither can overwrite the other.</b>
        /// <para>
        /// The energy centre stores plant rooms in a guid-keyed dictionary, so a materialisation that added
        /// a plant room per unit would silently replace the previous one and lose an entire air system -
        /// with no error anywhere. Every count below would break before any timing did.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public void SeveralUnits_BecomeSeparateAirSystemsInOnePlantRoomAndNeitherOverwritesTheOther(int count)
        {
            AdjacencyCluster adjacencyCluster = new();

            List<AirHandlingUnit> airHandlingUnits = [];

            for (int i = 0; i < count; i++)
            {
                MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit, string.Format(" {0}", i), adjacencyCluster);
                airHandlingUnits.Add(airHandlingUnit);
            }

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.Template();

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            //One plant room, whatever the unit count.
            Assert.Single(mechanicalVentilationMaterialisation.SystemEnergyCentre.GetSystemPlantRooms());

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);

            List<AirSystem> airSystems = MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation);

            Assert.Equal(count, airSystems.Count);

            HashSet<Guid> guids_AirSystem = [];
            foreach (AirSystem airSystem in airSystems)
            {
                Assert.True(guids_AirSystem.Add(airSystem.Guid));
            }

            //Five rooms each, disjoint, and all of them still there - nothing was replaced.
            HashSet<Guid> guids_SystemSpace = [];

            foreach (AirSystem airSystem in airSystems)
            {
                List<SystemSpace> systemSpaces = systemPlantRoom.GetRelatedObjects<SystemSpace>(airSystem);

                Assert.Equal(5, systemSpaces.Count);

                foreach (SystemSpace systemSpace in systemSpaces)
                {
                    Assert.True(guids_SystemSpace.Add(systemSpace.Guid), "A room belongs to two air systems.");
                }
            }

            Assert.Equal(5 * count, guids_SystemSpace.Count);
            Assert.Equal(5 * count, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation).Count);

            //The re-keyed fan, damper and junction sets are pairwise different despite one template.
            Assert.Equal(count * FansPerAirSystem(systemPlantRoom, airSystems[0]), FanCount(systemPlantRoom));

            //The template's own air system and its prototype rooms are gone.
            List<SystemPlantRoom> systemPlantRooms_Template = systemEnergyCentre_Template.GetSystemPlantRooms();
            AirSystem airSystem_Template = systemPlantRooms_Template[0].GetSystems<AirSystem>()[0];

            Assert.DoesNotContain(airSystem_Template.Guid, guids_AirSystem);

            foreach (SystemSpace systemSpace in MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation))
            {
                Assert.DoesNotContain(systemSpace.Guid, TemplateSystemSpaceGuids(systemPlantRooms_Template[0]));
            }

            //The direct overwrite guard: every materialised object holds its own identity.
            HashSet<Guid> guids = [];
            int count_Object = 0;

            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents())
            {
                count_Object++;
                guids.Add(((Core.SAMObject)systemComponent).Guid);
            }

            Assert.Equal(count_Object, guids.Count);
        }

        /// <summary>
        /// The shared non-air plant is <b>not</b> duplicated per unit: the collection count is the
        /// template's, whether one unit or three are materialised. Copying it per unit would give a
        /// dwelling three electrical installations.
        /// </summary>
        [Fact]
        public void SharedNonAirPlant_IsNotDuplicatedPerUnit()
        {
            int count_One = CollectionCount(1);
            int count_Three = CollectionCount(3);

            //Non-zero first: removing every shared collection would satisfy 0 == 0 and look like a pass.
            Assert.True(count_One > 0, "The template's shared collections are missing from the result, so equality here would prove nothing.");

            Assert.Equal(count_One, count_Three);
        }

        /// <summary>
        /// <b>A subdivided room's duty is the SUM of its terminals, and all three keep their lineage.</b>
        /// <para>
        /// 7 + 8 + 15 is one 30 l/s supply leg - not 7 (the first), not 3 (the count), and not the largest.
        /// All three terminals are real objects with their own identities, so all three get a row naming
        /// that one connection: a caller has to be able to trace a duty back to the terminals that produced
        /// it.
        /// </para>
        /// <para>
        /// The negative control removes the middle terminal and re-materialises: two rows, 22 l/s, and
        /// <b>the same connection guid</b> - which derives from the space and the air system, not from the
        /// terminal set. Without it the test could pass by the rows being accidental.
        /// </para>
        /// </summary>
        [Fact]
        public void SubdividedTerminals_SumToOneLegAndAllThreeKeepTheirLineage()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");
            MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);

            VentilationTerminal ventilationTerminal_7 = MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 7.0, "Terminal 7");
            VentilationTerminal ventilationTerminal_8 = MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 8.0, "Terminal 8");
            VentilationTerminal ventilationTerminal_15 = MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 15.0, "Terminal 15");

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Equal(30.0, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation)[0].FlowRate.Value);

            List<MechanicalVentilationBinding> bindings = MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.SupplyConnection);

            //Exactly three rows, naming exactly the three terminals, all against ONE connection.
            Assert.Equal(3, bindings.Count);

            HashSet<Guid> guids_Analytical = [];
            HashSet<Guid> guids_Systems = [];

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in bindings)
            {
                guids_Analytical.Add(mechanicalVentilationBinding.Guid_Analytical);
                guids_Systems.Add(mechanicalVentilationBinding.Guid_Systems);
            }

            Assert.Equal([ventilationTerminal_7.Guid, ventilationTerminal_8.Guid, ventilationTerminal_15.Guid], guids_Analytical);
            Assert.Single(guids_Systems);

            Guid guid_Connection = bindings[0].Guid_Systems;

            Dictionary<Guid, double> dictionary = MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation);

            Assert.Single(dictionary);
            Assert.Equal(30.0, dictionary[guid_Connection]);

            //The negative control: remove the 8 l/s terminal and the answer changes, but the connection's
            //identity does not.
            adjacencyCluster.RemoveObject<VentilationTerminal>(ventilationTerminal_8.Guid);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Reduced = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation_Reduced);

            Assert.Equal(2, MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation_Reduced, MechanicalVentilationBindingType.SupplyConnection).Count);
            Assert.Equal(22.0, MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation_Reduced)[guid_Connection]);
        }

        /// <summary>
        /// <b>Lineage is complete both ways.</b> Every unit, space, terminal and movement the
        /// materialisation read appears in at least one row; every air system, room and leg it produced
        /// appears in at least one row; and no row names the plant room or a copied fan, damper or junction
        /// - those have no analytical source, so a row for them would state a lineage that does not exist.
        /// </summary>
        [Fact]
        public void Lineage_IsCompleteInBothDirectionsAndNamesNothingWithoutASource()
        {
            AdjacencyCluster adjacencyCluster = new();

            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_1, " 1", adjacencyCluster);
            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_2, " 2", adjacencyCluster);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            HashSet<Guid> guids_Analytical = [];
            HashSet<Guid> guids_Systems = [];
            HashSet<string> keys = [];

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in mechanicalVentilationMaterialisation.Bindings)
            {
                guids_Analytical.Add(mechanicalVentilationBinding.Guid_Analytical);
                guids_Systems.Add(mechanicalVentilationBinding.Guid_Systems);

                Assert.True(keys.Add(string.Format("{0}|{1}|{2}", mechanicalVentilationBinding.BindingType, mechanicalVentilationBinding.Guid_Analytical, mechanicalVentilationBinding.Guid_Analytical_Secondary)), "Two rows state the same source for the same kind of object.");

                Assert.NotEqual(MechanicalVentilationBindingType.Undefined, mechanicalVentilationBinding.BindingType);
            }

            //Analytical -> Systems: everything read as an authority is named.
            Assert.Contains(airHandlingUnit_1.Guid, guids_Analytical);
            Assert.Contains(airHandlingUnit_2.Guid, guids_Analytical);

            foreach (Space space in adjacencyCluster.GetSpaces())
            {
                Assert.Contains(space.Guid, guids_Analytical);
            }

            foreach (VentilationTerminal ventilationTerminal in adjacencyCluster.GetObjects<VentilationTerminal>())
            {
                Assert.Contains(ventilationTerminal.Guid, guids_Analytical);
            }

            foreach (SpaceAirMovement spaceAirMovement in adjacencyCluster.GetObjects<SpaceAirMovement>())
            {
                Assert.Contains(spaceAirMovement.Guid, guids_Analytical);
            }

            //Systems -> Analytical: everything with a source is named, and nothing else is.
            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);

            foreach (AirSystem airSystem in MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation))
            {
                Assert.Contains(airSystem.Guid, guids_Systems);
            }

            foreach (SystemSpace systemSpace in MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation))
            {
                Assert.Contains(systemSpace.Guid, guids_Systems);
            }

            foreach (Guid guid in MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation).Keys)
            {
                Assert.Contains(guid, guids_Systems);
            }

            //The plant room and the copied plant have no analytical source and are deliberately absent.
            Assert.DoesNotContain(systemPlantRoom.Guid, guids_Systems);

            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents())
            {
                if (systemComponent is SystemFan || systemComponent is SystemDamper || systemComponent is SystemAirJunction)
                {
                    Assert.DoesNotContain(((Core.SAMObject)systemComponent).Guid, guids_Systems);
                }
            }
        }

        /// <summary>
        /// <b>A regulatory requirement cannot leak into the design airflow.</b> The room carries Part F
        /// data stating a requirement well below its design duty; the materialised flow is the duty.
        /// <para>
        /// The structural control is what makes this more than a value check: a model carrying the Part F
        /// data and a model with it stripped produce <b>byte-identical</b> graphs, so the code cannot be
        /// reading it at all.
        /// </para>
        /// </summary>
        [Fact]
        public void PartFRequirement_CannotLeakIntoDesignAirflow()
        {
            //One unit shared by both models: the two runs must differ ONLY in the Part F data.
            AirHandlingUnit airHandlingUnit = Analytical.Create.AirHandlingUnit("AHU");

            AdjacencyCluster adjacencyCluster_With = Model(airHandlingUnit, out Space space_With, true);
            AdjacencyCluster adjacencyCluster_Without = Model(airHandlingUnit, out Space _, false);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_With = adjacencyCluster_With.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Without = adjacencyCluster_Without.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation_With);
            AssertMaterialised(mechanicalVentilationMaterialisation_Without);

            //The duty, never the requirement.
            Assert.Equal(30.0, MechanicalVentilationTestModel.SystemSpace(mechanicalVentilationMaterialisation_With, space_With).FlowRate.Value);

            //And the structural control.
            Assert.Equal(
                MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_Without.SystemEnergyCentre),
                MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_With.SystemEnergyCentre));

            static AdjacencyCluster Model(AirHandlingUnit airHandlingUnit, out Space space, bool partFSpaceData)
            {
                AdjacencyCluster result = new();

                VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(result, airHandlingUnit, out AirHandlingUnit _);

                space = new Space(new Guid("2f1b6a90-4c5d-4e21-9a37-8b0d5e6c1234"), "Bedroom", new Geometry.Spatial.Point3D(0, 0, 0));
                space.SetValue(SpaceParameter.Area, MechanicalVentilationTestModel.Area);
                space.SetValue(SpaceParameter.Volume, MechanicalVentilationTestModel.Volume);

                if (partFSpaceData)
                {
                    //A requirement well BELOW the design duty, so reading it would visibly change the answer.
                    space.SetValue(SpaceParameter.PartFSpaceData, new PartFSpaceData("Bedroom", Analytical.Enums.PartFType.Habitable, Analytical.Enums.PartFVentilationType.supply, true, 8.0, true, true, false, false, null, 8.0));
                }

                result.AddObject(space);

                MechanicalVentilationTestModel.Serve(result, ventilationSystem, space);
                MechanicalVentilationTestModel.Terminal(result, ventilationSystem, space, FlowClassification.Supply, 30.0);

                return result;
            }
        }

        /// <summary>
        /// <b>A selected product's capacity cannot leak into the design airflow.</b> The unit carries a
        /// selected product; the materialised flow is still the design terminal duty.
        /// <para>
        /// Capacity is what the chosen machine <i>can</i> move and duty is what the design asks of it - a
        /// unit that can move 150 l/s serving a dwelling designed at 50 has a design duty of 50, and
        /// writing 150 onto the graph would make it simulate air the design does not move.
        /// </para>
        /// <para>
        /// The structural control again: stripping the selection produces a <b>byte-identical</b> graph,
        /// so the code cannot be reading it.
        /// </para>
        /// </summary>
        [Fact]
        public void SelectedEquipmentCapacity_CannotLeakIntoDesignAirflow()
        {
            //One unit shared by both models, so the two runs differ only in the selection.
            AirHandlingUnit airHandlingUnit_With = Analytical.Create.AirHandlingUnit("AHU");
            AirHandlingUnit airHandlingUnit_Without = new(airHandlingUnit_With);

            //A real product, whose capacity is far above the 50 l/s this dwelling is designed at.
            airHandlingUnit_With.SetValue(AirHandlingUnitParameter.VentilationUnitReference, new VentilationUnitReference("Nuaire", "MRXBOX95B-WM1", "MRXBOX95B-WM1"));

            AdjacencyCluster adjacencyCluster_With = Model(airHandlingUnit_With, out Space space_With);
            AdjacencyCluster adjacencyCluster_Without = Model(airHandlingUnit_Without, out Space _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_With = adjacencyCluster_With.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Without = adjacencyCluster_Without.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation_With);
            AssertMaterialised(mechanicalVentilationMaterialisation_Without);

            //The duty, never the capacity.
            Assert.Equal(50.0, MechanicalVentilationTestModel.SystemSpace(mechanicalVentilationMaterialisation_With, space_With).FlowRate.Value);

            Assert.Equal(
                MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_Without.SystemEnergyCentre),
                MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_With.SystemEnergyCentre));

            static AdjacencyCluster Model(AirHandlingUnit airHandlingUnit, out Space space)
            {
                AdjacencyCluster result = new();

                VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(result, airHandlingUnit, out AirHandlingUnit _);

                space = new Space(new Guid("7c3e9a11-2b48-4d6f-8a05-9e1f2c3d4b56"), "Bedroom", new Geometry.Spatial.Point3D(0, 0, 0));
                space.SetValue(SpaceParameter.Area, MechanicalVentilationTestModel.Area);
                space.SetValue(SpaceParameter.Volume, MechanicalVentilationTestModel.Volume);

                result.AddObject(space);

                MechanicalVentilationTestModel.Serve(result, ventilationSystem, space);
                MechanicalVentilationTestModel.Terminal(result, ventilationSystem, space, FlowClassification.Supply, 50.0);

                return result;
            }
        }

        /// <summary>
        /// <b>Names are not identity.</b> A model in which every space, system and terminal name is empty
        /// materialises correctly and produces the <b>same guids</b> as the named model - the only name read
        /// anywhere is the air handling unit's, which is the model's own binding and not a membership
        /// decision. A name-scanning implementation cannot pass this.
        /// </summary>
        [Fact]
        public void Names_AreNotIdentity()
        {
            //One unit shared by both runs: it is the only object whose name the materialisation reads, and
            //its guid is what every derived identity below it depends on.
            AirHandlingUnit airHandlingUnit = Analytical.Create.AirHandlingUnit("AHU");

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Named = Materialise(airHandlingUnit, false);
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Unnamed = Materialise(airHandlingUnit, true);

            AssertMaterialised(mechanicalVentilationMaterialisation_Named);
            AssertMaterialised(mechanicalVentilationMaterialisation_Unnamed);

            Assert.Equal(
                Guids(mechanicalVentilationMaterialisation_Named),
                Guids(mechanicalVentilationMaterialisation_Unnamed));

            static MechanicalVentilationMaterialisation Materialise(AirHandlingUnit airHandlingUnit, bool blankNames)
            {
                AdjacencyCluster adjacencyCluster = new();

                //Fixed guids, so the two runs differ only in the names. The system's own name comes from its
                //type, so blanking the type blanks it - SAMObject.Name has no setter.
                adjacencyCluster.AddObject(airHandlingUnit);

                VentilationSystemType ventilationSystemType = Analytical.Create.VentilationSystemType(Guid.NewGuid(), blankNames ? string.Empty : "MVHR", string.Empty);

                VentilationSystem ventilationSystem = new(blankNames ? string.Empty : "1", ventilationSystemType);
                ventilationSystem.SetValue(VentilationSystemParameter.SupplyUnitName, "AHU");
                ventilationSystem.SetValue(VentilationSystemParameter.ExhaustUnitName, "AHU");

                adjacencyCluster.AddObject(ventilationSystem);

                Space space = new(new Guid("a1c0d3e5-6789-4bcd-8ef0-123456789abc"), blankNames ? string.Empty : "Bedroom", new Geometry.Spatial.Point3D(0, 0, 0));
                space.SetValue(SpaceParameter.Area, MechanicalVentilationTestModel.Area);
                space.SetValue(SpaceParameter.Volume, MechanicalVentilationTestModel.Volume);

                adjacencyCluster.AddObject(space);

                MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);

                VentilationTerminal ventilationTerminal = new(new Guid("b2d1e4f6-789a-4cde-9f01-23456789abcd"), blankNames ? string.Empty : "Supply", FlowClassification.Supply, 30.0);

                adjacencyCluster.AddObject(ventilationTerminal);
                adjacencyCluster.AddRelation(ventilationSystem, ventilationTerminal);
                adjacencyCluster.AddRelation(ventilationTerminal, space);

                return adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            }
        }

        // -------------------------------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------------------------------

        internal static void AssertMaterialised(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);
            Assert.Empty(mechanicalVentilationMaterialisation.Refusals);
            Assert.NotNull(mechanicalVentilationMaterialisation.SystemEnergyCentre);
            Assert.True(mechanicalVentilationMaterialisation.IsMaterialised);
        }

        /// <summary>Every materialised guid, sorted - the determinism comparison.</summary>
        internal static List<Guid> Guids(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            List<Guid> result = [];

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);

            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents() ?? [])
            {
                result.Add(((Core.SAMObject)systemComponent).Guid);
            }

            foreach (Core.Systems.ISystem system in systemPlantRoom.GetSystems() ?? [])
            {
                result.Add(((Core.SAMObject)system).Guid);
            }

            result.Sort();

            return result;
        }

        private static ISystemConnection Connection(SystemPlantRoom systemPlantRoom, Guid guid)
        {
            foreach (ISystemConnection systemConnection in systemPlantRoom.GetSystemConnections() ?? [])
            {
                if (((Core.SAMObject)systemConnection).Guid == guid)
                {
                    return systemConnection;
                }
            }

            return null;
        }

        private static bool IsSystemSpace(SystemPlantRoom systemPlantRoom, Guid guid)
        {
            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents() ?? [])
            {
                if (systemComponent is SystemSpace && ((Core.SAMObject)systemComponent).Guid == guid)
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<Guid> TemplateSystemSpaceGuids(SystemPlantRoom systemPlantRoom)
        {
            HashSet<Guid> result = [];

            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents() ?? [])
            {
                if (systemComponent is SystemSpace)
                {
                    result.Add(((Core.SAMObject)systemComponent).Guid);
                }
            }

            return result;
        }

        private static int FanCount(SystemPlantRoom systemPlantRoom)
        {
            int result = 0;

            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents() ?? [])
            {
                if (systemComponent is SystemFan)
                {
                    result++;
                }
            }

            return result;
        }

        private static int FansPerAirSystem(SystemPlantRoom systemPlantRoom, AirSystem airSystem)
        {
            int result = 0;

            foreach (ISystemJSAMObject systemJSAMObject in systemPlantRoom.GetRelatedObjects(airSystem) ?? [])
            {
                if (systemJSAMObject is SystemFan)
                {
                    result++;
                }
            }

            return result;
        }

        private static int CollectionCount(int count)
        {
            AdjacencyCluster adjacencyCluster = new();

            for (int i = 0; i < count; i++)
            {
                MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _, string.Format(" {0}", i), adjacencyCluster);
            }

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

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
