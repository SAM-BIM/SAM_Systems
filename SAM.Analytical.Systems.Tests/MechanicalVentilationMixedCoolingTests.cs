// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Core;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// Mixed Part O strategies PR3B-2: cooled and uncooled units in ONE materialisation. With
    /// <see cref="MechanicalVentilationSettings.GuidanceTemplate"/> stated, a unit named in the (now partial)
    /// guidance settings is the product's own arrangement on the MVRE topology - exchanger and supply DX coil - and
    /// every other unit is ordinary uncooled ventilation on the call's MV topology, all in one plant room sharing one
    /// set of plant collections. Fixture values only.
    /// </summary>
    public class MechanicalVentilationMixedCoolingTests
    {
        private const double Tolerance = 1e-9;

        [Fact]
        public void CooledAndUncooledUnits_MaterialiseInOneCall_AndOnlyTheCooledUnitIsCooled()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit airHandlingUnit_Cooled, out AirHandlingUnit airHandlingUnit_Uncooled);

            MechanicalVentilationMaterialisation mixed = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(airHandlingUnit_Cooled, adjacencyCluster));
            AssertMaterialised(mixed);

            //One guidance record, for the cooled unit only.
            MechanicalVentilationGuidanceCooling guidanceCooling = Assert.Single(mixed.GuidanceCoolings);
            Assert.Equal(airHandlingUnit_Cooled.Guid, guidanceCooling.Guid_AirHandlingUnit);

            //One plant room, one air system per unit.
            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mixed);
            Assert.Equal(2, MechanicalVentilationTestModel.AirSystems(mixed).Count);

            //The cooled unit: the product's arrangement. The uncooled unit: ordinary MV - no exchanger, no coil.
            Assert.Equal((1, 1), Kinds(systemPlantRoom, AirSystemOf(mixed, airHandlingUnit_Cooled)));
            Assert.Equal((0, 0), Kinds(systemPlantRoom, AirSystemOf(mixed, airHandlingUnit_Uncooled)));

            //The coil is the cooled unit's own.
            Assert.Contains(systemPlantRoom.GetSystemComponents<ISystemComponent>(AirSystemOf(mixed, airHandlingUnit_Cooled)) ?? [], x => x is SAMObject sAMObject && sAMObject.Guid == guidanceCooling.Guid_DXCoil);
        }

        [Fact]
        public void TheUncooledUnit_IsExactlyTheOrdinaryMvUnit()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit airHandlingUnit_Cooled, out AirHandlingUnit airHandlingUnit_Uncooled);

            MechanicalVentilationMaterialisation mixed = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(airHandlingUnit_Cooled, adjacencyCluster));
            MechanicalVentilationMaterialisation plain = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            AssertMaterialised(mixed);
            AssertMaterialised(plain);

            Assert.Equal(Signature(plain, airHandlingUnit_Uncooled), Signature(mixed, airHandlingUnit_Uncooled));
        }

        [Fact]
        public void TheCooledUnit_IsExactlyTheLegacyManufacturerGuidanceUnit()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationGuidanceSettings selected = GuidanceSettings();
            selected.CoolingStatSpaceGuid = adjacencyCluster.GetSpaces().Single(x => x.Name == "Living").Guid;
            MechanicalVentilationMaterialisation legacy = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), new MechanicalVentilationSettings { GuidanceSettings = new Dictionary<Guid, MechanicalVentilationGuidanceSettings> { { airHandlingUnit.Guid, selected } } });
            MechanicalVentilationMaterialisation mixed = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(airHandlingUnit, adjacencyCluster));
            AssertMaterialised(legacy);
            AssertMaterialised(mixed);

            Assert.Equal(Signature(legacy, airHandlingUnit), Signature(mixed, airHandlingUnit));

            MechanicalVentilationGuidanceCooling guidanceCooling_Legacy = Assert.Single(legacy.GuidanceCoolings);
            MechanicalVentilationGuidanceCooling guidanceCooling_Mixed = Assert.Single(mixed.GuidanceCoolings);
            Assert.Equal(guidanceCooling_Legacy.ElevatedAirFlow_Lps, guidanceCooling_Mixed.ElevatedAirFlow_Lps, Tolerance);
            Assert.Equal(guidanceCooling_Legacy.Rooms.Select(x => (x.DesignSupply_Lps, x.ElevatedSupply_Lps)), guidanceCooling_Mixed.Rooms.Select(x => (x.DesignSupply_Lps, x.ElevatedSupply_Lps)));
        }

        [Fact]
        public void ThePlantCollections_AreOneInstallation_NotDuplicatedByTheImport()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit airHandlingUnit_Cooled, out AirHandlingUnit _);

            int count_Template = MechanicalVentilationTestModel.PlantRoom(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template())).GetSystemComponents<ISystemCollection>().Count;
            int count_Mixed = MechanicalVentilationTestModel.PlantRoom(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(airHandlingUnit_Cooled, adjacencyCluster))).GetSystemComponents<ISystemCollection>().Count;

            Assert.Equal(count_Template, count_Mixed);
        }

        [Fact]
        public void EveryZone_IsMixingVentilation_OnBothTopologies()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit airHandlingUnit_Cooled, out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mixed = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(airHandlingUnit_Cooled, adjacencyCluster));
            AssertMaterialised(mixed);

            List<SystemSpace> systemSpaces = MechanicalVentilationTestModel.SystemSpaces(mixed);
            Assert.NotEmpty(systemSpaces);
            Assert.All(systemSpaces, x => Assert.False(x.DisplacementVentilation));
        }

        [Fact]
        public void APartialGuidanceSet_WithoutAGuidanceTopology_StillRefuses()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit airHandlingUnit_Cooled, out AirHandlingUnit _);

            MechanicalVentilationSettings settings = Mixed(airHandlingUnit_Cooled, adjacencyCluster);
            settings.GuidanceTemplate = null;

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), settings), "only partly configured");
        }

        [Fact]
        public void GuidanceForAUnitTheCallDoesNotMaterialise_StillRefuses()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit _, out AirHandlingUnit _);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(Guid.NewGuid())), "does not materialise");
        }

        [Fact]
        public void TheMixedMaterialisation_IsDeterministic_AndDiffersFromTheUncooledOne()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit airHandlingUnit_Cooled, out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mixed_1 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(airHandlingUnit_Cooled, adjacencyCluster));
            MechanicalVentilationMaterialisation mixed_2 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Mixed(airHandlingUnit_Cooled, adjacencyCluster));
            MechanicalVentilationMaterialisation plain = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            Assert.Equal(MechanicalVentilationTestModel.Json(mixed_1.SystemEnergyCentre), MechanicalVentilationTestModel.Json(mixed_2.SystemEnergyCentre));
            Assert.NotEqual(mixed_1.SystemEnergyCentre.Guid, plain.SystemEnergyCentre.Guid);
        }

        [Fact]
        public void TheSettings_CarryTheGuidanceTopology_ThroughJson()
        {
            AdjacencyCluster adjacencyCluster = TwoDwellings(out AirHandlingUnit airHandlingUnit_Cooled, out AirHandlingUnit _);

            MechanicalVentilationSettings roundTrip = new(Mixed(airHandlingUnit_Cooled, adjacencyCluster).ToJsonObject());

            Assert.NotNull(roundTrip.GuidanceTemplate);
            AssertMaterialised(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), roundTrip));
            Assert.False(new MechanicalVentilationSettings().ToJsonObject().ContainsKey("GuidanceTemplate"));
        }

        // =====================================================================================================
        // The cooling operating airflow, resolved through SAM's rule
        // =====================================================================================================

        [Theory]
        [InlineData(63.0, 80.0)]
        [InlineData(100.0, 100.0)]
        public void TheGuidanceSettings_OperateAtSamsCoolingAirflow(double design_Lps, double expected_Lps)
        {
            MechanicalVentilationGuidanceSettings settings = Product().MechanicalVentilationGuidanceSettings(design_Lps, design_Lps, out string refusal);

            Assert.Null(refusal);
            Assert.Equal(expected_Lps, settings.OperatingStrategy.ElevatedAirFlow_Lps, Tolerance);
        }

        [Fact]
        public void TheGuidanceSettings_RefuseADesignBeyondThePublishedCoolingRange()
        {
            Assert.Null(Product().MechanicalVentilationGuidanceSettings(143.0, 143.0, out string refusal));
            Assert.Contains("published cooling airflow range", refusal);
        }

        // =====================================================================================================
        // Fixtures
        // =====================================================================================================

        private static AdjacencyCluster TwoDwellings(out AirHandlingUnit airHandlingUnit_1, out AirHandlingUnit airHandlingUnit_2)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out airHandlingUnit_1, " A");
            return MechanicalVentilationTestModel.Dwelling(out airHandlingUnit_2, " B", adjacencyCluster);
        }

        private static MechanicalVentilationSettings Mixed(AirHandlingUnit airHandlingUnit_Cooled, AdjacencyCluster adjacencyCluster) =>
            Mixed(airHandlingUnit_Cooled.Guid, adjacencyCluster.GetSpaces().Single(x => x.Name == (airHandlingUnit_Cooled.Name == "AHU A" ? "Living A" : "Living")).Guid);

        internal static MechanicalVentilationSettings Mixed(Guid guid_AirHandlingUnit_Cooled, Guid guid_CoolingStatSpace = default)
        {
            MechanicalVentilationGuidanceSettings guidance = GuidanceSettings();
            guidance.CoolingStatSpaceGuid = guid_CoolingStatSpace;
            return new MechanicalVentilationSettings
            {
                GuidanceTemplate = MechanicalVentilationTestModel.TemplateMVRE(),
                GuidanceSettings = new Dictionary<Guid, MechanicalVentilationGuidanceSettings> { { guid_AirHandlingUnit_Cooled, guidance } },
            };
        }

        private static VentilationUnitOperatingStrategy Strategy(double elevated_Lps = 80.0, double minimum_Lps = 70.0, double maximum_Lps = 90.0)
        {
            return new VentilationUnitOperatingStrategy
            {
                Source = "Test Fixture, manufacturer modelling guidance, v.1 - not a real product",
                CoolingActivationTemperature_C = 22.0,
                CoolingActivationSignal = CoolingActivationSignal.RoomTemperature,
                MinimumCoolingActivationTemperature_C = 22.0,
                MaximumCoolingActivationTemperature_C = 25.0,
                BypassMinimumIntakeTemperature_C = 12.0,
                BypassMinimumExtractTemperature_C = 18.0,
                ElevatedAirFlow_Lps = elevated_Lps,
                MinimumElevatedAirFlow_Lps = minimum_Lps,
                MaximumElevatedAirFlow_Lps = maximum_Lps,
                SummerBypassSupplyTemperatureRule = SupplyTemperatureRule.OutdoorAir(),
                HeatCoolthRecoverySupplyTemperatureRule = SupplyTemperatureRule.LinearBlend(0.8),
                CoolingSupplyTemperatureRule = SupplyTemperatureRule.IntakeOffset([60.0, 80.0, 100.0, 120.0], [15.0, 14.0, 13.5, 13.0]),
            };
        }

        private static MechanicalVentilationGuidanceSettings GuidanceSettings()
        {
            return new MechanicalVentilationGuidanceSettings
            {
                SourceIdentifier = "Test Fixture, manufacturer modelling guidance, v.1 - not a real product",
                OperatingStrategy = Strategy(),
            };
        }

        //A catalogue product: default 80, published 60-120 l/s, capacity 150 l/s - the Nuaire-shaped envelope.
        private static VentilationUnitTemplate Product()
        {
            VentilationUnitOperatingStrategy strategy = Strategy(double.NaN, 60.0, 120.0);
            strategy.DefaultElevatedAirFlow_Lps = 80.0;

            return new VentilationUnitTemplate(new VentilationUnitReference("Fixture", "Unit", "U-1"), "Test fixture")
            {
                MaximumSupplyFlowRate_Lps = 150.0,
                MaximumExtractFlowRate_Lps = 150.0,
                OperatingStrategy = strategy,
            };
        }

        private static AirSystem AirSystemOf(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, AirHandlingUnit airHandlingUnit)
        {
            MechanicalVentilationBinding binding = MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.AirSystem).Single(x => x.Guid_Analytical == airHandlingUnit.Guid);
            return MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation).Single(x => x.Guid == binding.Guid_Systems);
        }

        private static (int Exchangers, int Coils) Kinds(SystemPlantRoom systemPlantRoom, AirSystem airSystem)
        {
            List<ISystemComponent> systemComponents = systemPlantRoom.GetSystemComponents<ISystemComponent>(airSystem) ?? [];
            return (systemComponents.Count(x => x is SystemExchanger), systemComponents.Count(x => x is SystemDXCoil));
        }

        //A unit's air system, by component kinds and the design flows of its connections - guid-insensitive.
        private static List<string> Signature(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, AirHandlingUnit airHandlingUnit)
        {
            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);
            AirSystem airSystem = AirSystemOf(mechanicalVentilationMaterialisation, airHandlingUnit);

            List<string> result = (systemPlantRoom.GetSystemComponents<ISystemComponent>(airSystem) ?? []).Select(x => x.GetType().Name + (x is SystemSpace systemSpace ? ":" + systemSpace.Name + ":" + systemSpace.DisplacementVentilation : string.Empty)).ToList();
            result.Sort(StringComparer.Ordinal);

            return result;
        }

        private static void AssertMaterialised(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);
            Assert.True(mechanicalVentilationMaterialisation.IsMaterialised, string.Join("; ", mechanicalVentilationMaterialisation.Refusals));
        }

        private static void AssertRefused(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, string expected)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);
            Assert.False(mechanicalVentilationMaterialisation.IsMaterialised);
            Assert.Empty(mechanicalVentilationMaterialisation.GuidanceCoolings);
            Assert.Contains(mechanicalVentilationMaterialisation.Refusals, x => x.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
    }
}
