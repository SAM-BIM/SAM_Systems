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

        [Fact]
        public void TheCoil_SitsBetweenTheExchangerAndTheSupplyFan()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation guidance = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, GuidanceSettings()));

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
            MechanicalVentilationMaterialisation guidance = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, GuidanceSettings()));

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

            //The stat room: the supplied room with the largest design supply (the fixture's 13 l/s bedroom).
            MechanicalVentilationGuidanceRoom room_Stat = guidanceCooling.Rooms.Single(x => x.Guid_Space == guidanceCooling.Guid_Space_Stat);
            Assert.Equal(13.0, room_Stat.DesignSupply_Lps, Tolerance);

            Assert.Equal(CoolingActivationSignal.RoomTemperature, guidanceCooling.Settings.OperatingStrategy.CoolingActivationSignal);
        }

        [Fact]
        public void TheVentilationDesign_IsTheSelectedProductsWithoutTheCoil()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation plain = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE());
            MechanicalVentilationMaterialisation guidance = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, GuidanceSettings()));

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

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, GuidanceSettings())), "heat exchanger");
        }

        [Fact]
        public void GuidanceAndRecirculationCooling_AreNeverCombined()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings settings = Settings(airHandlingUnit, GuidanceSettings());
            settings.CoolingSettings = new Dictionary<Guid, MechanicalVentilationCoolingSettings> { { airHandlingUnit.Guid, new MechanicalVentilationCoolingSettings() } };

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), settings), "never both");
        }

        [Fact]
        public void AnUnresolvedElevatedAirflow_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationGuidanceSettings guidanceSettings = GuidanceSettings();
            guidanceSettings.OperatingStrategy.ElevatedAirFlow_Lps = double.NaN;

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
