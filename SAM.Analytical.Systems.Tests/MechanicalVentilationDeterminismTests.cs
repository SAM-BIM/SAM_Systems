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
    /// That the identities the materialisation derives depend on what the objects <i>are</i>, and on
    /// nothing else.
    /// <para>
    /// <b>Why this matters beyond tidiness.</b> The next stage binds simulation results back to rooms by
    /// guid. If the same design materialised twice produced two sets of identities, every one of those
    /// bindings would be to a graph that no longer exists - and the failure would look like missing
    /// results rather than like a re-keying bug.
    /// </para>
    /// </summary>
    public class MechanicalVentilationDeterminismTests
    {
        /// <summary>
        /// <b>The identity schema is pinned.</b> It is hashed first in every derivation, so changing it
        /// re-keys every materialised object in existence. That is exactly what it is for - but it must be
        /// a deliberate act, and this is what makes an accidental one fail.
        /// </summary>
        [Fact]
        public void IdentitySchema_IsPinned()
        {
            Assert.Equal("MechanicalVentilationMaterialisation:v1", Analytical.Systems.Query.MechanicalVentilationIdentitySchema);
        }

        /// <summary>
        /// <b>Identical display names with different guids materialise as distinct rooms.</b> Three rooms
        /// all called "Bedroom" are three rooms - nothing keys on a name, so nothing collapses them.
        /// <para>
        /// This is the counterpart of the ambiguous-unit-name refusal: an ambiguous UNIT name is the one
        /// place a name is read at all, and it refuses. See
        /// <c>MechanicalVentilationRefusalTests.DuplicateAirHandlingUnitNames_FailClosed</c>.
        /// </para>
        /// </summary>
        [Fact]
        public void DuplicateSpaceNames_MaterialiseAsDistinctRooms()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Model();

            VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, "AHU", out AirHandlingUnit _);

            List<Space> spaces = [];

            for (int i = 0; i < 3; i++)
            {
                Space space = MechanicalVentilationTestModel.Space(adjacencyCluster, "Bedroom");

                MechanicalVentilationTestModel.Serve(adjacencyCluster, ventilationSystem, space);
                MechanicalVentilationTestModel.Terminal(adjacencyCluster, ventilationSystem, space, FlowClassification.Supply, 10.0 + i);

                spaces.Add(space);
            }

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Equal(3, MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation).Count);

            HashSet<Guid> guids = [];

            for (int i = 0; i < spaces.Count; i++)
            {
                SystemSpace systemSpace = MechanicalVentilationTestModel.SystemSpace(mechanicalVentilationMaterialisation, spaces[i]);

                Assert.NotNull(systemSpace);
                Assert.True(guids.Add(systemSpace.Guid), "Two same-named rooms collapsed into one identity.");

                //And each keeps its own duty, so they were not merged in value either.
                Assert.Equal(10.0 + i, systemSpace.FlowRate.Value);
            }
        }

        /// <summary>
        /// <b>Enumeration order does not reach the result.</b> The same design assembled in the reverse
        /// order produces the same identities and the same lineage. Nothing in a derivation is a position.
        /// </summary>
        [Fact]
        public void EnumerationOrder_DoesNotReachTheResult()
        {
            AirHandlingUnit airHandlingUnit = Analytical.Create.AirHandlingUnit("AHU");

            List<Guid> guids_Space = [
                new Guid("11111111-1111-4111-8111-111111111111"),
                new Guid("22222222-2222-4222-8222-222222222222"),
                new Guid("33333333-3333-4333-8333-333333333333")];

            List<Guid> guids_Terminal = [
                new Guid("aaaaaaaa-1111-4111-8111-111111111111"),
                new Guid("bbbbbbbb-2222-4222-8222-222222222222"),
                new Guid("cccccccc-3333-4333-8333-333333333333")];

            //The unit and the movements are shared instances, so the two models differ ONLY in the order
            //their objects were added - SpaceAirMovement mints its own guid, and two separately built
            //movements would be two different analytical objects rather than one seen twice.
            List<SpaceAirMovement> spaceAirMovements = [];

            for (int i = 1; i < guids_Space.Count; i++)
            {
                spaceAirMovements.Add(new SpaceAirMovement(
                    string.Format("Transfer {0}", i),
                    0.005 * i,
                    new Core.ObjectReference(typeof(Space), guids_Space[i - 1]).ToString(),
                    new Core.ObjectReference(typeof(Space), guids_Space[i]).ToString()));
            }

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Forward = Materialise(airHandlingUnit, guids_Space, guids_Terminal, spaceAirMovements, false);
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Reverse = Materialise(airHandlingUnit, guids_Space, guids_Terminal, spaceAirMovements, true);

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_Forward);
            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_Reverse);

            Assert.Equal(
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_Forward),
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_Reverse));

            AssertSameBindings(mechanicalVentilationMaterialisation_Forward, mechanicalVentilationMaterialisation_Reverse);

            static MechanicalVentilationMaterialisation Materialise(AirHandlingUnit airHandlingUnit, List<Guid> guids_Space, List<Guid> guids_Terminal, List<SpaceAirMovement> spaceAirMovements, bool reverse)
            {
                AdjacencyCluster adjacencyCluster = new();

                VentilationSystem ventilationSystem = MechanicalVentilationTestModel.VentilationSystem(adjacencyCluster, airHandlingUnit, out AirHandlingUnit _);

                List<int> indexes = [];
                for (int i = 0; i < guids_Space.Count; i++)
                {
                    indexes.Add(reverse ? guids_Space.Count - 1 - i : i);
                }

                List<Space> spaces = [];

                foreach (int index in indexes)
                {
                    Space space = new(guids_Space[index], string.Format("Room {0}", index), new Geometry.Spatial.Point3D(0, 0, 0));
                    space.SetValue(SpaceParameter.Area, MechanicalVentilationTestModel.Area);
                    space.SetValue(SpaceParameter.Volume, MechanicalVentilationTestModel.Volume);

                    adjacencyCluster.AddObject(space);
                    adjacencyCluster.AddRelation(ventilationSystem, space);

                    spaces.Add(space);
                }

                foreach (int index in indexes)
                {
                    Space space = spaces.Find(x => x.Guid == guids_Space[index]);

                    VentilationTerminal ventilationTerminal = new(guids_Terminal[index], string.Format("Supply {0}", index), FlowClassification.Supply, 10.0 + index);

                    adjacencyCluster.AddObject(ventilationTerminal);
                    adjacencyCluster.AddRelation(ventilationSystem, ventilationTerminal);
                    adjacencyCluster.AddRelation(ventilationTerminal, space);
                }

                //The transfer chain, the same movement objects added in the two orders.
                foreach (int index in indexes)
                {
                    if (index == 0)
                    {
                        continue;
                    }

                    SpaceAirMovement spaceAirMovement = spaceAirMovements[index - 1];

                    adjacencyCluster.AddObject(spaceAirMovement);
                    adjacencyCluster.AddRelation(spaceAirMovement, spaces.Find(x => x.Guid == guids_Space[index]));
                }

                return adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            }
        }

        /// <summary>
        /// <b>Materialising the same design twice gives the same graph.</b> Every derived guid is
        /// identical and the lineage is sequence-equal.
        /// <para>
        /// The template's own air system, group, fan, damper, junction and prototype rooms are <b>absent</b>
        /// from the output - they were re-keyed and the originals removed. The plant room's guid <b>is</b>
        /// the template's: it is a container with no analytical source, and re-keying it would downgrade a
        /// display plant room to a plain one and lose its schematic routing. That is the one stated
        /// inherited identity, and this pins it so it cannot change silently.
        /// </para>
        /// </summary>
        [Fact]
        public void RepeatedMaterialisation_IsStable()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.Template();

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_1 = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template);
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_2 = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template);

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_1);
            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_2);

            Assert.Equal(mechanicalVentilationMaterialisation_1.SystemEnergyCentre.Guid, mechanicalVentilationMaterialisation_2.SystemEnergyCentre.Guid);

            Assert.Equal(
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_1),
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_2));

            AssertSameBindings(mechanicalVentilationMaterialisation_1, mechanicalVentilationMaterialisation_2);

            //The template's air plant was re-keyed, so none of its identities survive into the result.
            SystemPlantRoom systemPlantRoom_Template = systemEnergyCentre_Template.GetSystemPlantRooms()[0];

            HashSet<Guid> guids_Result = new(MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_1));

            foreach (AirSystem airSystem in systemPlantRoom_Template.GetSystems<AirSystem>())
            {
                Assert.DoesNotContain(airSystem.Guid, guids_Result);
            }

            foreach (ISystemComponent systemComponent in systemPlantRoom_Template.GetSystemComponents())
            {
                if (systemComponent is SystemSpace || systemComponent is SystemFan || systemComponent is SystemDamper || systemComponent is SystemAirJunction)
                {
                    Assert.DoesNotContain(((Core.SAMObject)systemComponent).Guid, guids_Result);
                }
            }

            //The one stated exception: the plant room keeps the template's identity.
            Assert.Equal(systemPlantRoom_Template.Guid, MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation_1).Guid);
        }

        /// <summary>
        /// <b>Different settings derive different identities.</b> The settings digest is part of the
        /// energy-centre key, so a materialisation under one operating schedule cannot silently reuse the
        /// identities of one under another.
        /// </summary>
        [Fact]
        public void DifferentSettings_DeriveDifferentIdentities()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_1 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_2 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { Schedule = Schedule("Operating") });

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_1);
            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_2);

            Assert.NotEqual(mechanicalVentilationMaterialisation_1.SystemEnergyCentre.Guid, mechanicalVentilationMaterialisation_2.SystemEnergyCentre.Guid);

            Assert.NotEqual(
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_1),
                MechanicalVentilationMaterialisationTests.Guids(mechanicalVentilationMaterialisation_2));
        }

        internal static YearlySchedule Schedule(string name)
        {
            YearlySchedule result = new(name);

            double[] values = new double[8760];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 1.0;
            }

            result.Values = values;

            return result;
        }

        private static void AssertSameBindings(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_1, MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_2)
        {
            List<MechanicalVentilationBinding> bindings_1 = mechanicalVentilationMaterialisation_1.Bindings;
            List<MechanicalVentilationBinding> bindings_2 = mechanicalVentilationMaterialisation_2.Bindings;

            Assert.Equal(bindings_1.Count, bindings_2.Count);
            Assert.NotEmpty(bindings_1);

            for (int i = 0; i < bindings_1.Count; i++)
            {
                Assert.Equal(bindings_1[i].BindingType, bindings_2[i].BindingType);
                Assert.Equal(bindings_1[i].Guid_Analytical, bindings_2[i].Guid_Analytical);
                Assert.Equal(bindings_1[i].Guid_Analytical_Secondary, bindings_2[i].Guid_Analytical_Secondary);
                Assert.Equal(bindings_1[i].Guid_Systems, bindings_2[i].Guid_Systems);
            }
        }
    }
}
