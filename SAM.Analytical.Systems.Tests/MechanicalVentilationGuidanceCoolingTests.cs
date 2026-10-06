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
    /// SAM#123: a selected product operated to its manufacturer's guidance, materialised as the product's own
    /// arrangement - the MVRE exchanger with a supply DX coil inserted after it - and recorded for the TAS
    /// grounding. Every value is a fixture value; no manufacturer figure appears in this file.
    /// </summary>
    public class MechanicalVentilationGuidanceCoolingTests
    {
        private const double Elevated_Lps = 80.0;
        private const double Tolerance = 1e-9;

        [Fact]
        public void EmptyGuidanceSettings_MaterialisesByteIdenticalToB0()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation b0 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation empty = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { GuidanceSettings = new Dictionary<Guid, MechanicalVentilationGuidanceSettings>() });

            AssertMaterialised(b0);
            Assert.Equal(MechanicalVentilationTestModel.Json(b0.SystemEnergyCentre), MechanicalVentilationTestModel.Json(empty.SystemEnergyCentre));
            Assert.Empty(empty.GuidanceCoolings);
        }

        /// <summary>
        /// Presentation, pinned because it misled (2026-09-29 real project): the coil was drawn 4 units left of the
        /// supply fan - on the MVRE template that is left of the fresh-air inlet, so the TPD read as "DX, then heat
        /// recovery" while its ducts were exchanger -> DX -> fan. It is drawn where it is connected.
        /// </summary>
        [Fact]
        public void TheCoil_IsDrawnBetweenTheExchangerAndTheSupplyFan()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation guidance = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, GuidanceSettings(adjacencyCluster)));

            AssertMaterialised(guidance);
            MechanicalVentilationGuidanceCooling guidanceCooling = Assert.Single(guidance.GuidanceCoolings);

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(guidance);

            Geometry.Planar.Point2D exchanger = Origin(Component(systemPlantRoom, guidanceCooling.Guid_Exchanger));
            Geometry.Planar.Point2D coil = Origin(Component(systemPlantRoom, guidanceCooling.Guid_DXCoil));
            Geometry.Planar.Point2D fan = Origin(Component(systemPlantRoom, guidanceCooling.Guid_Fan_Supply));

            Assert.True(exchanger.X < fan.X, "the MVRE template draws the exchanger before the supply fan");
            Assert.InRange(coil.X, exchanger.X + Tolerance, fan.X - Tolerance);
            Assert.Equal(fan.Y, coil.Y, 9);
        }

        private static Geometry.Planar.Point2D Origin(ISystemComponent systemComponent)
        {
            Geometry.Planar.Point2D result = (systemComponent as Geometry.Systems.IDisplaySystemObject<Geometry.Systems.SystemGeometryInstance>)?.SystemGeometry?.CoordinateSystem2D?.Origin;
            Assert.NotNull(result);
            return result;
        }

        [Fact]
        public void TheCoil_SitsBetweenTheExchangerAndTheSupplyFan()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation guidance = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, GuidanceSettings(adjacencyCluster)));

            AssertMaterialised(guidance);
            MechanicalVentilationGuidanceCooling guidanceCooling = Assert.Single(guidance.GuidanceCoolings);

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(guidance);
            ISystemComponent exchanger = Component(systemPlantRoom, guidanceCooling.Guid_Exchanger);
            ISystemComponent coil = Component(systemPlantRoom, guidanceCooling.Guid_DXCoil);
            ISystemComponent fan = Component(systemPlantRoom, guidanceCooling.Guid_Fan_Supply);

            Assert.IsAssignableFrom<SystemExchanger>(exchanger);
            SystemDXCoil systemDXCoil = Assert.IsAssignableFrom<SystemDXCoil>(coil);
            Assert.IsAssignableFrom<SystemFan>(fan);
            Assert.NotNull(systemDXCoil.HeatingDuty);

            Assert.Single(systemPlantRoom.GetSystemConnections(exchanger, coil) ?? []);
            Assert.Single(systemPlantRoom.GetSystemConnections(coil, fan) ?? []);
            Assert.Empty(systemPlantRoom.GetSystemConnections(exchanger, fan) ?? []);

            //The exchanger's supply outlet (connector 1) now feeds the coil's inlet (connector 0).
            ISystemConnection systemConnection_ExchangerToCoil = Assert.Single(systemPlantRoom.GetSystemConnections(exchanger, coil));
            Assert.True(systemConnection_ExchangerToCoil.TryGetIndex(systemConnection_ExchangerToCoil.ObjectReferences.First(x => Guid.TryParse(x.Reference?.ToString(), out Guid guid_Reference) && guid_Reference == ((SAMObject)exchanger).Guid), out int index_Exchanger));
            Assert.Equal(1, index_Exchanger);
        }

        [Fact]
        public void TheRecord_KeepsDesignAndElevatedAirflowsDistinct_InDesignProportions()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation guidance = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, GuidanceSettings(adjacencyCluster)));

            AssertMaterialised(guidance);
            MechanicalVentilationGuidanceCooling guidanceCooling = Assert.Single(guidance.GuidanceCoolings);

            Assert.Equal(25.0, guidanceCooling.DesignSupply_Lps, Tolerance);
            Assert.Equal(25.0, guidanceCooling.DesignExtract_Lps, Tolerance);
            Assert.Equal(Elevated_Lps, guidanceCooling.ElevatedAirFlow_Lps, Tolerance);
            Assert.Equal(Elevated_Lps, guidanceCooling.Rooms.Sum(x => x.ElevatedSupply_Lps), Tolerance);
            Assert.Equal(Elevated_Lps, guidanceCooling.Rooms.Sum(x => x.ElevatedExtract_Lps), Tolerance);

            foreach (MechanicalVentilationGuidanceRoom room in guidanceCooling.Rooms)
            {
                Assert.Equal(room.DesignSupply_Lps * Elevated_Lps / 25.0, room.ElevatedSupply_Lps, Tolerance);
                Assert.Equal(room.DesignExtract_Lps * Elevated_Lps / 25.0, room.ElevatedExtract_Lps, Tolerance);
            }

            //The selected stat room has less design supply than the bedroom: no largest-supply heuristic.
            MechanicalVentilationGuidanceRoom room_Stat = guidanceCooling.Rooms.Single(x => x.Guid_Space == guidanceCooling.Guid_Space_Stat);
            Assert.Equal(12.0, room_Stat.DesignSupply_Lps, Tolerance);

            Assert.Equal(CoolingActivationSignal.RoomTemperature, guidanceCooling.Settings.OperatingStrategy.CoolingActivationSignal);
        }

        [Fact]
        public void TheVentilationDesign_IsTheSelectedProductsWithoutTheCoil()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation plain = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE());
            MechanicalVentilationMaterialisation guidance = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, GuidanceSettings(adjacencyCluster)));

            AssertMaterialised(plain);
            AssertMaterialised(guidance);

            Assert.Equal(
                MechanicalVentilationTestModel.DesignFlowRates(plain).Values.OrderBy(x => x),
                MechanicalVentilationTestModel.DesignFlowRates(guidance).Values.OrderBy(x => x));
        }

        [Fact]
        public void ATemplateWithoutAnExchanger_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, GuidanceSettings(adjacencyCluster))), "heat exchanger");
        }

        [Fact]
        public void GuidanceAndRecirculationCooling_AreNeverCombined()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings settings = Settings(airHandlingUnit, GuidanceSettings(adjacencyCluster));
            settings.CoolingSettings = new Dictionary<Guid, MechanicalVentilationCoolingSettings> { { airHandlingUnit.Guid, new MechanicalVentilationCoolingSettings() } };

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), settings), "never both");
        }

        [Fact]
        public void AnUnresolvedElevatedAirflow_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationGuidanceSettings guidanceSettings = GuidanceSettings();
            guidanceSettings.OperatingStrategy.ElevatedAirFlow_Lps = double.NaN;

            guidanceSettings.CoolingStatSpaceGuid = adjacencyCluster.GetSpaces().Single(x => x.Name == "Living").Guid;
            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, guidanceSettings)), "elevated cooling airflow");
        }

        [Fact]
        public void TheSettings_SurviveAJsonRoundTrip()
        {
            MechanicalVentilationSettings settings = Settings(Guid.NewGuid(), GuidanceSettings());

            MechanicalVentilationSettings roundTrip = new(settings.ToJsonObject());

            MechanicalVentilationGuidanceSettings guidanceSettings = Assert.Single(roundTrip.GuidanceSettings).Value;
            Assert.Null(guidanceSettings.Refusal());
            Assert.Equal(SupplyTemperatureRuleType.IntakeOffset, guidanceSettings.OperatingStrategy.CoolingSupplyTemperatureRule.SupplyTemperatureRuleType);
            Assert.False(new MechanicalVentilationSettings().ToJsonObject().ContainsKey("GuidanceSettings"));
        }

        [Fact]
        public void ControlRoom_MustBeExplicitServedAndSupplied()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit unit);
            MechanicalVentilationGuidanceSettings guidance = GuidanceSettings();
            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(unit, guidance)), "selected cooling control room is missing");

            guidance.CoolingStatSpaceGuid = Guid.NewGuid();
            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(unit, guidance)), "not served by this unit");

            guidance.CoolingStatSpaceGuid = adjacencyCluster.GetSpaces().Single(x => x.Name == "Kitchen").Guid;
            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(unit, guidance)), "not supplied by this unit");

            guidance.CoolingStatSpaceGuid = adjacencyCluster.GetSpaces().Single(x => x.Name == "Living").Guid;
            MechanicalVentilationSettings saved = Settings(unit, guidance);
            MechanicalVentilationSettings reopened = new(saved.ToJsonObject());
            Assert.Equal(guidance.CoolingStatSpaceGuid, reopened.GuidanceSettings[unit.Guid].CoolingStatSpaceGuid);
        }

        [Fact]
        public void HeatingAnotherRoomAlone_DoesNotCallCoolingAtTheSelectedRoomStat()
        {
            MechanicalVentilationGuidanceSettings guidance = GuidanceSettings();
            guidance.SupplyTemperature(30.0, 25.0, 22.0, 25.0, out VentilationUnitOperatingMode off, out double airflow_Off);
            guidance.SupplyTemperature(30.0, 25.0, 22.1, 25.0, out VentilationUnitOperatingMode on, out double airflow_On);

            Assert.NotEqual(VentilationUnitOperatingMode.Cooling, off);
            Assert.Equal(VentilationUnitOperatingMode.Cooling, on);
            Assert.Equal(25.0, airflow_Off);
            Assert.Equal(Elevated_Lps, airflow_On);
        }

        // =====================================================================================================
        // Fixtures
        // =====================================================================================================

        private static MechanicalVentilationGuidanceSettings GuidanceSettings()
        {
            return new MechanicalVentilationGuidanceSettings
            {
                SourceIdentifier = "Test Fixture, manufacturer modelling guidance, v.1 - not a real product",
                OperatingStrategy = new VentilationUnitOperatingStrategy
                {
                    Source = "Test Fixture, manufacturer modelling guidance, v.1 - not a real product",
                    CoolingActivationTemperature_C = 22.0,
                    CoolingActivationSignal = CoolingActivationSignal.RoomTemperature,
                    MinimumCoolingActivationTemperature_C = 22.0,
                    MaximumCoolingActivationTemperature_C = 25.0,
                    BypassMinimumIntakeTemperature_C = 12.0,
                    BypassMinimumExtractTemperature_C = 18.0,
                    ElevatedAirFlow_Lps = Elevated_Lps,
                    MinimumElevatedAirFlow_Lps = 70.0,
                    MaximumElevatedAirFlow_Lps = 90.0,
                    SummerBypassSupplyTemperatureRule = SupplyTemperatureRule.OutdoorAir(),
                    HeatCoolthRecoverySupplyTemperatureRule = SupplyTemperatureRule.LinearBlend(0.8),
                    CoolingSupplyTemperatureRule = SupplyTemperatureRule.IntakeOffset([70.0, 80.0, 90.0], [15.0, 14.0, 13.0]),
                },
            };
        }

        private static MechanicalVentilationGuidanceSettings GuidanceSettings(AdjacencyCluster adjacencyCluster)
        {
            MechanicalVentilationGuidanceSettings result = GuidanceSettings();
            result.CoolingStatSpaceGuid = adjacencyCluster.GetSpaces().Single(x => x.Name == "Living").Guid;
            return result;
        }

        private static MechanicalVentilationSettings Settings(AirHandlingUnit airHandlingUnit, MechanicalVentilationGuidanceSettings guidanceSettings)
        {
            return Settings(airHandlingUnit.Guid, guidanceSettings);
        }

        private static MechanicalVentilationSettings Settings(Guid guid_AirHandlingUnit, MechanicalVentilationGuidanceSettings guidanceSettings)
        {
            return new MechanicalVentilationSettings
            {
                GuidanceSettings = new Dictionary<Guid, MechanicalVentilationGuidanceSettings> { { guid_AirHandlingUnit, guidanceSettings } },
            };
        }

        private static ISystemComponent Component(SystemPlantRoom systemPlantRoom, Guid guid)
        {
            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents() ?? [])
            {
                if (systemComponent is SAMObject sAMObject && sAMObject.Guid == guid)
                {
                    return systemComponent;
                }
            }

            throw new InvalidOperationException("No component " + guid);
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
