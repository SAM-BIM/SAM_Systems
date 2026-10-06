// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// Part O PR-3: <c>Create.MechanicalVentilation</c> with a stated ventilation system scope processes only the
    /// systems it is given. SAM decides what participates (<c>Query.PartOSystemsMaterialisationScope</c>); this seam
    /// must not re-enumerate the wider model and refuse, or absorb, a system SAM left out.
    /// <para>
    /// <b>The model is the owner's real model's shape.</b> <c>Modify.AddMechanicalSystems</c> builds the scaffolding a
    /// real project reaches Part O with - <c>NV 1</c> over Flat 1, <c>UV 1</c> over the corridor, <c>MV 1 → AHU1</c> over
    /// Flats 2 and 3 - and Part O's own MVHR system and unit sit beside it in Flats 2 and 3, with design terminals and
    /// transfer air. Everything else is fixture values.
    /// </para>
    /// </summary>
    public class MechanicalVentilationSystemScopeTests
    {
        // =====================================================================================================
        // The boundary on the real model's shape
        // =====================================================================================================

        [Fact]
        public void Unscoped_TheRealModelShape_Refuses_BecauseEverySystemIsRead()
        {
            Scaffold scaffold = new();

            MechanicalVentilationMaterialisation unscoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold));

            AssertRefused(unscoped, "names no air handling unit");
        }

        [Fact]
        public void Scoped_TheRealModelShape_MaterialisesOnlyTheTwoDwellingSystems()
        {
            Scaffold scaffold = new();
            string json_Before = MechanicalVentilationTestModel.Json(scaffold.AdjacencyCluster);

            MechanicalVentilationMaterialisation scoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, scaffold.Scope);
            AssertMaterialised(scoped);

            //Both dwelling units, and nothing else - AHU1 is on the model and is not pulled in.
            Assert.Equal(2, MechanicalVentilationTestModel.AirSystems(scoped).Count);
            Assert.Equal(Sorted(scaffold.Unit_Flat2.Guid, scaffold.Unit_Flat3.Guid), Bound(scoped, MechanicalVentilationBindingType.AirSystem));

            //Exactly the six dwelling rooms: no Flat 1 room, no corridor.
            Assert.Equal(Sorted([.. scaffold.Spaces_Mvhr.Select(x => x.Guid)]), Bound(scoped, MechanicalVentilationBindingType.SystemSpace));

            //Each dwelling's own legs and transfer air.
            Assert.Equal(2, MechanicalVentilationTestModel.Bindings(scoped, MechanicalVentilationBindingType.SupplyConnection).Count);
            Assert.Equal(4, MechanicalVentilationTestModel.Bindings(scoped, MechanicalVentilationBindingType.ExtractConnection).Count);
            Assert.Equal(4, MechanicalVentilationTestModel.Bindings(scoped, MechanicalVentilationBindingType.TransferConnection).Count);

            //Flat 2 uncooled, Flat 3 cooled - as the owner selected.
            Assert.Equal(scaffold.Unit_Flat3.Guid, Assert.Single(scoped.GuidanceCoolings).Guid_AirHandlingUnit);

            //The excluded systems stay on the model, untouched.
            Assert.Equal(json_Before, MechanicalVentilationTestModel.Json(scaffold.AdjacencyCluster));
            foreach (Guid guid in new[] { scaffold.NV.Guid, scaffold.UV.Guid, scaffold.MV.Guid })
            {
                Assert.NotNull(scaffold.AdjacencyCluster.GetObject<VentilationSystem>(guid));
            }

            Assert.NotNull(scaffold.AdjacencyCluster.GetObject<AirHandlingUnit>(scaffold.AHU1.Guid));
        }

        /// <summary>
        /// The scope is exactly "as if the other systems had been removed" - the same graph, the same derived guids and
        /// the same bindings as the unscoped call on SAM's working copy (which is what Iteration 3 and Mixed Design hand
        /// over). So stating the scope changes no identity.
        /// </summary>
        [Fact]
        public void Scoped_IsExactlyTheUnscopedCall_OnTheModelWithTheOtherSystemsRemoved()
        {
            Scaffold scaffold = new();

            MechanicalVentilationMaterialisation scoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, scaffold.Scope);
            MechanicalVentilationMaterialisation workingCopy = scaffold.Without(scaffold.NV, scaffold.UV, scaffold.MV).MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr);

            AssertMaterialised(scoped);
            AssertMaterialised(workingCopy);
            AssertSame(workingCopy, scoped);
        }

        // =====================================================================================================
        // 1-3: each unrelated scaffold system outside the scope
        // =====================================================================================================

        [Theory]
        [InlineData("NV 1", "names no air handling unit")]
        [InlineData("UV 1", "names no air handling unit")]
        [InlineData("MV 1", "is served by air handling units")]
        public void AnUnrelatedScaffoldSystemOutsideTheScope_IsNeverRead(string fullName, string refusal_Unscoped)
        {
            Scaffold scaffold = new();
            VentilationSystem ventilationSystem_Kept = new[] { scaffold.NV, scaffold.UV, scaffold.MV }.Single(x => x.FullName == fullName);
            AdjacencyCluster adjacencyCluster = scaffold.Without([.. new[] { scaffold.NV, scaffold.UV, scaffold.MV }.Where(x => x != ventilationSystem_Kept)]);

            //Proof the system is a real hazard: read, it refuses a correct design.
            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr), refusal_Unscoped);

            MechanicalVentilationMaterialisation scoped = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, scaffold.Scope);
            AssertMaterialised(scoped);
            Assert.Equal(Sorted(scaffold.Unit_Flat2.Guid, scaffold.Unit_Flat3.Guid), Bound(scoped, MechanicalVentilationBindingType.AirSystem));
            Assert.NotNull(adjacencyCluster.GetObject<VentilationSystem>(ventilationSystem_Kept.Guid));
        }

        [Fact]
        public void AUnitOnlyAnExcludedSystemNames_IsNotPartOfTheCall()
        {
            Scaffold scaffold = new();

            //Settings for AHU1 (MV 1's unit) are for a unit this call does not materialise.
            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { scaffold.Unit_Flat2.Guid, new MechanicalVentilationUnitSettings() },
                    { scaffold.Unit_Flat3.Guid, new MechanicalVentilationUnitSettings() },
                    { scaffold.AHU1.Guid, new MechanicalVentilationUnitSettings() },
                },
            };

            AssertRefused(scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), mechanicalVentilationSettings, scaffold.Spaces_Mvhr, scaffold.Scope), "which this call does not materialise");
        }

        // =====================================================================================================
        // 4-5: a malformed system outside the scope is never read; inside it, it refuses exactly as before
        // =====================================================================================================

        public enum Malformation
        {
            NoUnit,
            MissingUnit,
            AmbiguousUnit,
            SupplyAndExhaustDiffer,
            TerminalWithNoSpace,
            TerminalWithTwoSpaces,
            TerminalWithNoDuty,
            TerminalWithNegativeDuty,
        }

        public static TheoryData<Malformation, string> Malformations => new()
        {
            { Malformation.NoUnit, "names no air handling unit" },
            { Malformation.MissingUnit, "which the model does not contain" },
            { Malformation.AmbiguousUnit, "units in the model answer to" },
            { Malformation.SupplyAndExhaustDiffer, "for supply and" },
            { Malformation.TerminalWithNoSpace, "is related to 0 spaces" },
            { Malformation.TerminalWithTwoSpaces, "is related to 2 spaces" },
            { Malformation.TerminalWithNoDuty, "states no design airflow" },
            { Malformation.TerminalWithNegativeDuty, "states a design airflow of -" },
        };

        [Theory]
        [MemberData(nameof(Malformations))]
        public void AMalformedSystemOutsideTheScope_DoesNotRefuse(Malformation malformation, string refusal)
        {
            Scaffold scaffold = new();
            VentilationSystem ventilationSystem_Stray = scaffold.Stray(malformation);

            //Proof the malformation is real: handed to the unscoped call on a working copy, it refuses.
            AssertRefused(scaffold.Without(scaffold.NV, scaffold.UV, scaffold.MV).MechanicalVentilation(MechanicalVentilationTestModel.Template()), refusal);

            //Scoped over the whole model - every space in scope, so nothing hides the stray but the system scope.
            MechanicalVentilationMaterialisation scoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), null, scaffold.Scope);
            AssertMaterialised(scoped);
            Assert.Equal(Sorted(scaffold.Unit_Flat2.Guid, scaffold.Unit_Flat3.Guid), Bound(scoped, MechanicalVentilationBindingType.AirSystem));
            Assert.Equal(Sorted([.. scaffold.Spaces_Mvhr.Select(x => x.Guid)]), Bound(scoped, MechanicalVentilationBindingType.SystemSpace));
            Assert.NotNull(scaffold.AdjacencyCluster.GetObject<VentilationSystem>(ventilationSystem_Stray.Guid));
        }

        [Theory]
        [MemberData(nameof(Malformations))]
        public void AMalformedSystemInsideTheScope_StillRefuses_WithTheSameMessage(Malformation malformation, string refusal)
        {
            Scaffold scaffold = new();
            VentilationSystem ventilationSystem_Stray = scaffold.Stray(malformation);

            MechanicalVentilationMaterialisation unscoped = scaffold.Without(scaffold.NV, scaffold.UV, scaffold.MV).MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation scoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, null, [.. scaffold.Scope, ventilationSystem_Stray.Guid]);

            AssertRefused(scoped, refusal);
            Assert.Equal(unscoped.Refusals, scoped.Refusals);
        }

        /// <summary>
        /// The reconciliation's duty cross-check finds a unit's systems by the unit's name, over the whole model. A
        /// system outside the scope that names a scoped unit is SAM's decision to leave out, so its duty must not be
        /// added back to that unit.
        /// </summary>
        [Fact]
        public void AnExcludedSystemNamingAScopedUnit_AddsNothingToIt()
        {
            Scaffold scaffold = new();

            VentilationSystem ventilationSystem_Stray = MechanicalVentilationTestModel.VentilationSystem(scaffold.AdjacencyCluster, scaffold.Unit_Flat2, out AirHandlingUnit _);
            MechanicalVentilationTestModel.Serve(scaffold.AdjacencyCluster, ventilationSystem_Stray, scaffold.Spaces_Flat1[0]);
            MechanicalVentilationTestModel.Terminal(scaffold.AdjacencyCluster, ventilationSystem_Stray, scaffold.Spaces_Flat1[0], FlowClassification.Supply, 10.0);

            MechanicalVentilationMaterialisation scoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), null, scaffold.Scope);
            AssertMaterialised(scoped);

            //Flat 2's unit carries Flat 2's rooms and Flat 2's duty only.
            MechanicalVentilationMaterialisation workingCopy = scaffold.Without(scaffold.NV, scaffold.UV, scaffold.MV, ventilationSystem_Stray).MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold));
            AssertMaterialised(workingCopy);
            AssertSame(workingCopy, scoped);
        }

        // =====================================================================================================
        // 6-8: what an included system brings with it, and what it does not
        // =====================================================================================================

        [Fact]
        public void TwoRequestedDwellingSystems_BothMaterialise_EachOnItsOwnUnit()
        {
            Scaffold scaffold = new();

            MechanicalVentilationMaterialisation scoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, scaffold.Scope);
            AssertMaterialised(scoped);

            foreach ((AirHandlingUnit airHandlingUnit, List<Space> spaces) in new[] { (scaffold.Unit_Flat2, scaffold.Spaces_Flat2), (scaffold.Unit_Flat3, scaffold.Spaces_Flat3) })
            {
                Guid guid_AirSystem = MechanicalVentilationTestModel.Bindings(scoped, MechanicalVentilationBindingType.AirSystem).Single(x => x.Guid_Analytical == airHandlingUnit.Guid).Guid_Systems;
                AirSystem airSystem = MechanicalVentilationTestModel.AirSystems(scoped).Single(x => x.Guid == guid_AirSystem);

                foreach (Space space in spaces)
                {
                    SystemSpace systemSpace = MechanicalVentilationTestModel.SystemSpace(scoped, space);
                    Assert.NotNull(systemSpace);
                    Assert.Contains(MechanicalVentilationTestModel.PlantRoom(scoped).GetRelatedObjects<SystemSpace>(airSystem) ?? [], x => x.Guid == systemSpace.Guid);
                }
            }
        }

        /// <summary>
        /// Stating a system brings everything it depends on: the unit it names (found by name, though only the system was
        /// stated), its terminals' rooms, a hall reached only through transfer air, and that transfer air.
        /// </summary>
        [Fact]
        public void TheRequiredDependenciesOfAnIncludedSystem_AreRetained()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            VentilationSystem ventilationSystem = Assert.Single(adjacencyCluster.GetObjects<VentilationSystem>());

            MechanicalVentilationMaterialisation plain = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            AssertMaterialised(plain);

            //Unrelated plant beside it: a unit-less natural system and a legacy MV on its own unit, both over the dwelling.
            Scaffold.AddScaffold(adjacencyCluster, adjacencyCluster.GetSpaces(), "NV", null);
            Scaffold.AddScaffold(adjacencyCluster, adjacencyCluster.GetSpaces(), "MV", "AHU Legacy");

            MechanicalVentilationMaterialisation scoped = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, null, [ventilationSystem.Guid]);
            AssertMaterialised(scoped);

            Assert.Equal([airHandlingUnit.Guid], Bound(scoped, MechanicalVentilationBindingType.AirSystem));
            Assert.NotNull(MechanicalVentilationTestModel.SystemSpace(scoped, SpaceNamed(adjacencyCluster, "Hall")));
            Assert.Equal(5, MechanicalVentilationTestModel.SystemSpaces(scoped).Count);
            Assert.Equal(4, MechanicalVentilationTestModel.Bindings(scoped, MechanicalVentilationBindingType.TransferConnection).Count);

            //And it is the same graph the dwelling alone materialises.
            AssertSame(plain, scoped);
        }

        // =====================================================================================================
        // 9: the scope's own identity rules
        // =====================================================================================================

        [Fact]
        public void AnEmptyScope_Refuses()
        {
            Scaffold scaffold = new();

            AssertRefused(scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, null, []), "names no system");
        }

        [Fact]
        public void AScopeNamingNoIdentity_Refuses()
        {
            Scaffold scaffold = new();

            AssertRefused(scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, null, [.. scaffold.Scope, Guid.Empty]), "names a system with no identity");
        }

        [Fact]
        public void AScopeNamingAGuidTheModelDoesNotHold_Refuses()
        {
            Scaffold scaffold = new();
            Guid guid = Guid.NewGuid();

            MechanicalVentilationMaterialisation scoped = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, null, [.. scaffold.Scope, guid]);

            AssertRefused(scoped, guid.ToString());
            AssertRefused(scoped, "is not a ventilation system of the model");
        }

        [Fact]
        public void AScopeNamingAnObjectThatIsNotAVentilationSystem_Refuses()
        {
            Scaffold scaffold = new();

            //The unit and a room are on the model, but they are not ventilation systems.
            foreach (Guid guid in new[] { scaffold.Unit_Flat2.Guid, scaffold.Spaces_Flat2[0].Guid })
            {
                AssertRefused(scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, null, [.. scaffold.Scope, guid]), "is not a ventilation system of the model");
            }
        }

        [Fact]
        public void ARepeatedGuid_CountsOnce()
        {
            Scaffold scaffold = new();

            MechanicalVentilationMaterialisation once = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, scaffold.Scope);
            MechanicalVentilationMaterialisation repeated = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, [.. scaffold.Scope, .. scaffold.Scope, scaffold.Scope[0]]);

            AssertMaterialised(once);
            AssertSame(once, repeated);
        }

        [Fact]
        public void ANullScope_IsTheLegacyCall()
        {
            //A correct design: identical results.
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);
            MechanicalVentilationMaterialisation legacy = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation unscoped = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), null, null, null);
            AssertMaterialised(legacy);
            AssertSame(legacy, unscoped);

            //The real model's shape: the same refusal.
            Scaffold scaffold = new();
            MechanicalVentilationMaterialisation legacy_Scaffold = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr);
            MechanicalVentilationMaterialisation unscoped_Scaffold = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, null);
            Assert.False(legacy_Scaffold.IsMaterialised);
            Assert.Equal(legacy_Scaffold.Refusals, unscoped_Scaffold.Refusals);
        }

        [Fact]
        public void ARefusedScope_CarriesNoGraph()
        {
            Scaffold scaffold = new();

            MechanicalVentilationMaterialisation refused = scaffold.AdjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(scaffold), scaffold.Spaces_Mvhr, [Guid.NewGuid()]);

            Assert.False(refused.IsMaterialised);
            Assert.Null(refused.SystemEnergyCentre);
            Assert.Empty(refused.Bindings);
        }

        // =====================================================================================================
        // 10: Iteration 3 - its working copy holds only the systems it states, so stating them changes nothing
        // =====================================================================================================

        [Fact]
        public void StatingEverySystemOfTheWorkingCopy_IsTheLegacyCall_ForEveryIteration3Variant()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_A, " A");
            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_B, " B", adjacencyCluster);
            List<Guid> guids_All = [.. adjacencyCluster.GetObjects<VentilationSystem>().Select(x => x.Guid)];

            List<(SystemEnergyCentre, MechanicalVentilationSettings)> variants =
            [
                //B0: MV.json, the parity schedule's shape.
                (MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { Name = "Part O Iteration 3 mechanical ventilation" }),
                //B1+: MVRE.json with manufacturer-aware unit settings.
                (MechanicalVentilationTestModel.TemplateMVRE(), new MechanicalVentilationSettings { UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings> { { airHandlingUnit_A.Guid, new MechanicalVentilationUnitSettings() }, { airHandlingUnit_B.Guid, new MechanicalVentilationUnitSettings() } } }),
                //Mixed: one cooled unit.
                (MechanicalVentilationTestModel.Template(), MechanicalVentilationMixedCoolingTests.Mixed(airHandlingUnit_A.Guid, adjacencyCluster.GetSpaces().Single(x => x.Name == "Living A").Guid)),
            ];

            foreach ((SystemEnergyCentre systemEnergyCentre, MechanicalVentilationSettings mechanicalVentilationSettings) in variants)
            {
                MechanicalVentilationMaterialisation legacy = adjacencyCluster.MechanicalVentilation(systemEnergyCentre, mechanicalVentilationSettings);
                MechanicalVentilationMaterialisation scoped = adjacencyCluster.MechanicalVentilation(systemEnergyCentre, mechanicalVentilationSettings, null, guids_All);

                AssertMaterialised(legacy);
                AssertSame(legacy, scoped);
            }
        }

        // =====================================================================================================
        // Fixture
        // =====================================================================================================

        /// <summary>
        /// The owner's real model's shape: Flat 1 natural, the corridor uncontrolled, Flats 2 and 3 under the
        /// <c>AddMechanicalSystems</c> legacy <c>MV 1 → AHU1</c> (no terminal, no product) - and Part O's own MVHR system and
        /// unit in each of Flats 2 and 3, with a bedroom supply, kitchen and ensuite extract, and the transfer air between.
        /// </summary>
        private sealed class Scaffold
        {
            internal AdjacencyCluster AdjacencyCluster { get; } = new();

            internal List<Space> Spaces_Flat1 { get; } = [];
            internal List<Space> Spaces_Corridor { get; } = [];
            internal List<Space> Spaces_Flat2 { get; } = [];
            internal List<Space> Spaces_Flat3 { get; } = [];
            internal List<Space> Spaces_Mvhr => [.. Spaces_Flat2, .. Spaces_Flat3];

            internal VentilationSystem NV { get; }
            internal VentilationSystem UV { get; }
            internal VentilationSystem MV { get; }
            internal AirHandlingUnit AHU1 { get; }

            internal VentilationSystem System_Flat2 { get; }
            internal VentilationSystem System_Flat3 { get; }
            internal AirHandlingUnit Unit_Flat2 { get; }
            internal AirHandlingUnit Unit_Flat3 { get; }

            /// <summary>What SAM's Part O scope retains: the two systems Part O built.</summary>
            internal List<Guid> Scope => [System_Flat2.Guid, System_Flat3.Guid];

            internal Scaffold()
            {
                Spaces_Flat1.Add(MechanicalVentilationTestModel.Space(AdjacencyCluster, "Bedroom 1_1"));
                Spaces_Flat1.Add(MechanicalVentilationTestModel.Space(AdjacencyCluster, "Kitchen_2"));
                Spaces_Corridor.Add(MechanicalVentilationTestModel.Space(AdjacencyCluster, "Corridor"));

                foreach ((List<Space> spaces, string[] names) in new[] { (Spaces_Flat2, new[] { "Bedroom 2_3", "Kitchen_4", "Ensuite_5" }), (Spaces_Flat3, new[] { "Bedroom 2_6", "Kitchen_7", "Ensuite_8" }) })
                {
                    foreach (string name in names)
                    {
                        spaces.Add(MechanicalVentilationTestModel.Space(AdjacencyCluster, name));
                    }
                }

                NV = AddScaffold(AdjacencyCluster, Spaces_Flat1, "NV", null);
                UV = AddScaffold(AdjacencyCluster, Spaces_Corridor, "UV", null);
                MV = AddScaffold(AdjacencyCluster, Spaces_Mvhr, "MV", "AHU1");
                AHU1 = AdjacencyCluster.GetObjects<AirHandlingUnit>().Single(x => x.Name == "AHU1");

                System_Flat2 = Mvhr(Spaces_Flat2, "MVHR Flat 2", out AirHandlingUnit unit_Flat2);
                System_Flat3 = Mvhr(Spaces_Flat3, "MVHR Flat 3", out AirHandlingUnit unit_Flat3);
                Unit_Flat2 = unit_Flat2;
                Unit_Flat3 = unit_Flat3;
            }

            /// <summary>
            /// What <c>Modify.AddMechanicalSystems</c> builds for rooms whose internal condition names
            /// <paramref name="type"/>: one system related to them, named <c>&lt;type&gt; 1</c>, naming
            /// <paramref name="name_Unit"/> - except natural and uncontrolled, which never name a unit.
            /// </summary>
            internal static VentilationSystem AddScaffold(AdjacencyCluster adjacencyCluster, IEnumerable<Space> spaces, string type, string name_Unit)
            {
                SystemTypeLibrary systemTypeLibrary = new("Scaffold");
                systemTypeLibrary.Add(Analytical.Create.VentilationSystemType(Guid.NewGuid(), type, type));

                List<Space> spaces_Scaffold = [];
                foreach (Space space in spaces)
                {
                    InternalCondition internalCondition = new(space.Name + " IC");
                    internalCondition.SetValue(InternalConditionParameter.VentilationSystemTypeName, type);

                    Space space_Scaffold = adjacencyCluster.GetObject<Space>(space.Guid);
                    space_Scaffold.InternalCondition = internalCondition;
                    adjacencyCluster.AddObject(space_Scaffold);

                    spaces_Scaffold.Add(space_Scaffold);
                }

                List<MechanicalSystem> mechanicalSystems = Analytical.Modify.AddMechanicalSystems(adjacencyCluster, systemTypeLibrary, spaces_Scaffold, name_Unit, name_Unit);

                return Assert.IsType<VentilationSystem>(Assert.Single(mechanicalSystems));
            }

            private VentilationSystem Mvhr(List<Space> spaces, string name_Unit, out AirHandlingUnit airHandlingUnit)
            {
                VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, name_Unit, out airHandlingUnit);

                foreach (Space space in spaces)
                {
                    MechanicalVentilationTestModel.Serve(AdjacencyCluster, result, space);
                }

                MechanicalVentilationTestModel.Terminal(AdjacencyCluster, result, spaces[0], FlowClassification.Supply, 63.0);
                MechanicalVentilationTestModel.Terminal(AdjacencyCluster, result, spaces[1], FlowClassification.Extract, 55.0);
                MechanicalVentilationTestModel.Terminal(AdjacencyCluster, result, spaces[2], FlowClassification.Extract, 8.0);

                MechanicalVentilationTestModel.Transfer(AdjacencyCluster, spaces[0], spaces[1], 0.055);
                MechanicalVentilationTestModel.Transfer(AdjacencyCluster, spaces[0], spaces[2], 0.008);

                return result;
            }

            /// <summary>One more system outside the Part O dwellings, malformed in exactly one way, over Flat 1.</summary>
            internal VentilationSystem Stray(Malformation malformation)
            {
                Space space = Spaces_Flat1[0];

                switch (malformation)
                {
                    case Malformation.NoUnit:
                        {
                            VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, null, null, null, out AirHandlingUnit _);
                            MechanicalVentilationTestModel.Serve(AdjacencyCluster, result, space);
                            return result;
                        }

                    case Malformation.MissingUnit:
                        {
                            VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, null, "Ghost", "Ghost", out AirHandlingUnit _);
                            MechanicalVentilationTestModel.Serve(AdjacencyCluster, result, space);
                            return result;
                        }

                    case Malformation.AmbiguousUnit:
                        {
                            MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, "Twin", out AirHandlingUnit _);
                            AdjacencyCluster.RemoveObject(AdjacencyCluster.GetObjects<VentilationSystem>().Single(x => x.GetValue<string>(VentilationSystemParameter.SupplyUnitName) == "Twin"));
                            VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, "Twin", out AirHandlingUnit _);
                            MechanicalVentilationTestModel.Serve(AdjacencyCluster, result, space);
                            return result;
                        }

                    case Malformation.SupplyAndExhaustDiffer:
                        {
                            VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, "Stray Supply", "Stray Supply", "Stray Exhaust", out AirHandlingUnit _);
                            AdjacencyCluster.AddObject(Analytical.Create.AirHandlingUnit("Stray Exhaust"));
                            MechanicalVentilationTestModel.Serve(AdjacencyCluster, result, space);
                            return result;
                        }

                    case Malformation.TerminalWithNoSpace:
                        {
                            VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, "Stray", out AirHandlingUnit _);
                            VentilationTerminal ventilationTerminal = new("Orphan terminal", FlowClassification.Supply, 10.0);
                            AdjacencyCluster.AddObject(ventilationTerminal);
                            AdjacencyCluster.AddRelation(result, ventilationTerminal);
                            return result;
                        }

                    case Malformation.TerminalWithTwoSpaces:
                        {
                            VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, "Stray", out AirHandlingUnit _);
                            VentilationTerminal ventilationTerminal = MechanicalVentilationTestModel.Terminal(AdjacencyCluster, result, space, FlowClassification.Supply, 10.0);
                            AdjacencyCluster.AddRelation(ventilationTerminal, Spaces_Flat1[1]);
                            return result;
                        }

                    case Malformation.TerminalWithNoDuty:
                    case Malformation.TerminalWithNegativeDuty:
                        {
                            VentilationSystem result = MechanicalVentilationTestModel.VentilationSystem(AdjacencyCluster, "Stray", out AirHandlingUnit _);
                            MechanicalVentilationTestModel.Serve(AdjacencyCluster, result, space);
                            MechanicalVentilationTestModel.Terminal(AdjacencyCluster, result, space, FlowClassification.Supply, malformation == Malformation.TerminalWithNoDuty ? null : -5.0);
                            return result;
                        }

                    default:
                        throw new ArgumentOutOfRangeException(nameof(malformation));
                }
            }

            /// <summary>SAM's working copy: a shallow copy with the given systems removed, as the Part O scope builds it.</summary>
            internal AdjacencyCluster Without(params VentilationSystem[] ventilationSystems)
            {
                AdjacencyCluster result = new(AdjacencyCluster);

                foreach (VentilationSystem ventilationSystem in ventilationSystems)
                {
                    Assert.True(result.RemoveObject(result.GetObject<VentilationSystem>(ventilationSystem.Guid)));
                }

                return result;
            }
        }

        private static MechanicalVentilationSettings Mixed(Scaffold scaffold) => MechanicalVentilationMixedCoolingTests.Mixed(scaffold.Unit_Flat3.Guid, scaffold.Spaces_Flat3.Single(x => x.Name == "Bedroom 2_6").Guid);

        private static Space SpaceNamed(AdjacencyCluster adjacencyCluster, string name) => adjacencyCluster.GetSpaces().Single(x => x.Name == name);

        private static List<Guid> Sorted(params Guid[] guids)
        {
            List<Guid> result = [.. guids];
            result.Sort();
            return result;
        }

        private static List<Guid> Bound(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, MechanicalVentilationBindingType mechanicalVentilationBindingType)
        {
            return Sorted([.. MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, mechanicalVentilationBindingType).Select(x => x.Guid_Analytical)]);
        }

        /// <summary>The same graph, byte for byte, with the same bindings, coolings and notes.</summary>
        private static void AssertSame(MechanicalVentilationMaterialisation expected, MechanicalVentilationMaterialisation actual)
        {
            AssertMaterialised(actual);
            Assert.Equal(MechanicalVentilationTestModel.Json(expected.SystemEnergyCentre), MechanicalVentilationTestModel.Json(actual.SystemEnergyCentre));
            Assert.Equal(expected.Bindings.Select(x => x.ToString()), actual.Bindings.Select(x => x.ToString()));
            Assert.Equal(expected.GuidanceCoolings.Select(x => (x.Guid_AirHandlingUnit, x.Guid_AirSystem, x.ElevatedAirFlow_Lps)), actual.GuidanceCoolings.Select(x => (x.Guid_AirHandlingUnit, x.Guid_AirSystem, x.ElevatedAirFlow_Lps)));
            Assert.Equal(expected.Notes, actual.Notes);
        }

        private static void AssertMaterialised(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);
            Assert.True(mechanicalVentilationMaterialisation.IsMaterialised, string.Join("; ", mechanicalVentilationMaterialisation.Refusals));
        }

        private static void AssertRefused(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, string expected)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);
            Assert.False(mechanicalVentilationMaterialisation.IsMaterialised, "materialised where a refusal containing '" + expected + "' was expected");
            Assert.Contains(mechanicalVentilationMaterialisation.Refusals, x => x.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
    }
}
