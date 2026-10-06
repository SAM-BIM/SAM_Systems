// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// SAM#123: one unit's resolved manufacturer operating strategy
    /// (<see cref="MechanicalVentilationGuidanceSettings"/>), and how its elevated operating airflow is
    /// divided between rooms (<see cref="MechanicalVentilationOperatingFlows"/>).
    /// <para>
    /// <b>The invariant these tests exist to hold.</b> A design airflow and an operating airflow are
    /// different numbers with different authorities, and neither is ever written into the other:
    /// <c>PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow</c>. The
    /// design distribution is read to derive proportions and is handed back unchanged.
    /// </para>
    /// <para>
    /// <b>And the one a manufacturer's guidance warns about.</b> An elevated airflow divided evenly between
    /// rooms over-cools some and under-cools others while looking entirely reasonable in a results table.
    /// The share of an elevated total a room gets is its own share of the design total, and where there is
    /// no design distribution to derive that from, this refuses rather than falling back to an even split
    /// or to floor area.
    /// </para>
    /// <para>
    /// Every fixture here is invented and names no real product.
    /// </para>
    /// </summary>
    public class MechanicalVentilationGuidanceTests
    {
        private const double tolerance = 1e-9;

        private static readonly Guid guid_Living = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid guid_Bedroom1 = new("22222222-2222-2222-2222-222222222222");
        private static readonly Guid guid_Bedroom2 = new("33333333-3333-3333-3333-333333333333");
        private static readonly Guid guid_Kitchen = new("44444444-4444-4444-4444-444444444444");
        private static readonly Guid guid_Bathroom = new("55555555-5555-5555-5555-555555555555");

        // =================================================================================================
        // A. The elevated distribution holds the design proportions
        // =================================================================================================

        /// <summary>
        /// Each room's elevated share is its own share of the design total, on its own side - and the two
        /// sides' totals are both the elevated total, which is what "the wet-room extract rises to match"
        /// means.
        /// </summary>
        [Fact]
        public void AnElevatedTotal_IsDividedInTheDesignProportions()
        {
            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(DesignSupply(), DesignExtract(), 80.0);

            Assert.Null(mechanicalVentilationOperatingFlows.Refusal());

            //Design supply totals 30 l/s: 13 living, 11 bedroom 1, 6 bedroom 2.
            Assert.Equal(30.0, mechanicalVentilationOperatingFlows.DesignSupplyTotal_Lps, tolerance);

            IReadOnlyDictionary<Guid, double> supply_Lps = mechanicalVentilationOperatingFlows.OperatingSupply_Lps;

            Assert.Equal(80.0 * 13.0 / 30.0, supply_Lps[guid_Living], tolerance);
            Assert.Equal(80.0 * 11.0 / 30.0, supply_Lps[guid_Bedroom1], tolerance);
            Assert.Equal(80.0 * 6.0 / 30.0, supply_Lps[guid_Bedroom2], tolerance);

            //Design extract totals 21 l/s: 13 kitchen, 8 bathroom - a different room list and a different total.
            Assert.Equal(21.0, mechanicalVentilationOperatingFlows.DesignExtractTotal_Lps, tolerance);

            IReadOnlyDictionary<Guid, double> extract_Lps = mechanicalVentilationOperatingFlows.OperatingExtract_Lps;

            Assert.Equal(80.0 * 13.0 / 21.0, extract_Lps[guid_Kitchen], tolerance);
            Assert.Equal(80.0 * 8.0 / 21.0, extract_Lps[guid_Bathroom], tolerance);
        }

        /// <summary>
        /// The ratio between any two rooms is the same at the elevated rate as at the design rate - the
        /// property the proportional balancing exists to hold - at every elevated total.
        /// </summary>
        [Theory]
        [InlineData(70.0)]
        [InlineData(80.0)]
        [InlineData(90.0)]
        public void TheRatioBetweenRooms_IsTheSameElevatedAsDesigned(double operatingAirFlowRate_Lps)
        {
            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(DesignSupply(), DesignExtract(), operatingAirFlowRate_Lps);

            Assert.Null(mechanicalVentilationOperatingFlows.Refusal());

            IReadOnlyDictionary<Guid, double> supply_Lps = mechanicalVentilationOperatingFlows.OperatingSupply_Lps;

            Assert.Equal(13.0 / 11.0, supply_Lps[guid_Living] / supply_Lps[guid_Bedroom1], tolerance);
            Assert.Equal(11.0 / 6.0, supply_Lps[guid_Bedroom1] / supply_Lps[guid_Bedroom2], tolerance);

            IReadOnlyDictionary<Guid, double> extract_Lps = mechanicalVentilationOperatingFlows.OperatingExtract_Lps;

            Assert.Equal(13.0 / 8.0, extract_Lps[guid_Kitchen] / extract_Lps[guid_Bathroom], tolerance);

            //Each share is the design share, stated on its own.
            Assert.Equal(13.0 / 30.0, mechanicalVentilationOperatingFlows.DesignSupplyShare(guid_Living), tolerance);
            Assert.Equal(8.0 / 21.0, mechanicalVentilationOperatingFlows.DesignExtractShare(guid_Bathroom), tolerance);
        }

        /// <summary>Both sides add up to the elevated total: the unit is balanced while it cools.</summary>
        [Theory]
        [InlineData(70.0)]
        [InlineData(85.5)]
        [InlineData(90.0)]
        public void TheElevatedSupplyAndExtract_Balance(double operatingAirFlowRate_Lps)
        {
            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(DesignSupply(), DesignExtract(), operatingAirFlowRate_Lps);

            Assert.Null(mechanicalVentilationOperatingFlows.Refusal());

            Assert.Equal(operatingAirFlowRate_Lps, Total(mechanicalVentilationOperatingFlows.OperatingSupply_Lps), MechanicalVentilationOperatingFlows.Tolerance_Lps);
            Assert.Equal(operatingAirFlowRate_Lps, Total(mechanicalVentilationOperatingFlows.OperatingExtract_Lps), MechanicalVentilationOperatingFlows.Tolerance_Lps);
        }

        /// <summary>
        /// An elevated distribution is never an even split. With rooms of genuinely different design duties,
        /// no two rooms receive the same elevated airflow.
        /// </summary>
        [Fact]
        public void AnElevatedDistribution_IsNeverAnEvenSplit()
        {
            IReadOnlyDictionary<Guid, double> supply_Lps = new MechanicalVentilationOperatingFlows(DesignSupply(), DesignExtract(), 90.0).OperatingSupply_Lps;

            Assert.NotEqual(supply_Lps[guid_Living], supply_Lps[guid_Bedroom1], 6);
            Assert.NotEqual(supply_Lps[guid_Bedroom1], supply_Lps[guid_Bedroom2], 6);

            //An even split would give every room 30 l/s. None of them gets it.
            foreach (double value in supply_Lps.Values)
            {
                Assert.NotEqual(30.0, value, 6);
            }
        }

        /// <summary>The design distribution is an input, and it is handed back exactly as it arrived.</summary>
        [Fact]
        public void TheDesignDistribution_IsReadAndNeverWritten()
        {
            Dictionary<Guid, double> designSupply_Lps = DesignSupply();
            Dictionary<Guid, double> designExtract_Lps = DesignExtract();

            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(designSupply_Lps, designExtract_Lps, 90.0);

            Assert.Null(mechanicalVentilationOperatingFlows.Refusal());

            Assert.Equal(13.0, designSupply_Lps[guid_Living], tolerance);
            Assert.Equal(11.0, designSupply_Lps[guid_Bedroom1], tolerance);
            Assert.Equal(6.0, designSupply_Lps[guid_Bedroom2], tolerance);
            Assert.Equal(13.0, designExtract_Lps[guid_Kitchen], tolerance);
            Assert.Equal(8.0, designExtract_Lps[guid_Bathroom], tolerance);

            //And what it hands out is a copy: writing to it cannot reach the distribution.
            Dictionary<Guid, double> supply_Lps = (Dictionary<Guid, double>)mechanicalVentilationOperatingFlows.OperatingSupply_Lps;
            supply_Lps[guid_Living] = 0.0;

            Assert.Equal(90.0 * 13.0 / 30.0, mechanicalVentilationOperatingFlows.OperatingSupply_Lps[guid_Living], tolerance);
        }

        // =================================================================================================
        // B. Fail closed
        // =================================================================================================

        /// <summary>
        /// Where there is no design distribution to hold, there is nothing to hold it to. Every one of these
        /// refuses rather than dividing the elevated total some other way.
        /// </summary>
        [Fact]
        public void WithNoUsableDesignSupply_ItRefuses()
        {
            Assert.NotNull(new MechanicalVentilationOperatingFlows(null, DesignExtract(), 80.0).Refusal());
            Assert.NotNull(new MechanicalVentilationOperatingFlows(new Dictionary<Guid, double>(), DesignExtract(), 80.0).Refusal());

            //Present, and all zero: a total of nothing is not a set of proportions.
            Dictionary<Guid, double> designSupply_Lps = new() { [guid_Living] = 0.0, [guid_Bedroom1] = 0.0 };

            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(designSupply_Lps, DesignExtract(), 80.0);

            Assert.NotNull(mechanicalVentilationOperatingFlows.Refusal());
            Assert.Contains("design supply total", mechanicalVentilationOperatingFlows.Refusal());
            Assert.Empty(mechanicalVentilationOperatingFlows.OperatingSupply_Lps);
        }

        /// <summary>The extract side refuses on its own terms, so a dwelling cannot be raised on one side only.</summary>
        [Fact]
        public void WithNoUsableDesignExtract_ItRefuses()
        {
            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(DesignSupply(), new Dictionary<Guid, double> { [guid_Kitchen] = 0.0 }, 80.0);

            Assert.NotNull(mechanicalVentilationOperatingFlows.Refusal());
            Assert.Contains("design extract total", mechanicalVentilationOperatingFlows.Refusal());
        }

        /// <summary>A design airflow that is not a usable number refuses, and names the room.</summary>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(-3.0)]
        public void ADesignAirflowThatIsNotUsable_Refuses(double value)
        {
            Dictionary<Guid, double> designSupply_Lps = DesignSupply();
            designSupply_Lps[guid_Bedroom2] = value;

            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(designSupply_Lps, DesignExtract(), 80.0);

            Assert.NotNull(mechanicalVentilationOperatingFlows.Refusal());
            Assert.Contains(guid_Bedroom2.ToString(), mechanicalVentilationOperatingFlows.Refusal());
        }

        /// <summary>An operating airflow that is not a usable number refuses.</summary>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(0.0)]
        [InlineData(-10.0)]
        public void AnOperatingAirflowThatIsNotUsable_Refuses(double operatingAirFlowRate_Lps)
        {
            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(DesignSupply(), DesignExtract(), operatingAirFlowRate_Lps);

            Assert.NotNull(mechanicalVentilationOperatingFlows.Refusal());
            Assert.Contains("operating airflow", mechanicalVentilationOperatingFlows.Refusal());
        }

        /// <summary>
        /// An "elevated" rate below what the dwelling already moves is a contradiction between two decisions,
        /// and is shown rather than quietly reconciled by clamping it up to the design total.
        /// </summary>
        [Fact]
        public void AnElevatedRateBelowTheDesignRate_Refuses()
        {
            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(DesignSupply(), DesignExtract(), 25.0);

            Assert.NotNull(mechanicalVentilationOperatingFlows.Refusal());
            Assert.Contains("already designed to move", mechanicalVentilationOperatingFlows.Refusal());
            Assert.Empty(mechanicalVentilationOperatingFlows.OperatingSupply_Lps);
        }

        /// <summary>An elevated rate exactly equal to the design rate is legal - it is simply no elevation.</summary>
        [Fact]
        public void AnElevatedRateEqualToTheDesignRate_IsLegal()
        {
            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new(DesignSupply(), DesignExtract(), 30.0);

            Assert.Null(mechanicalVentilationOperatingFlows.Refusal());
            Assert.Equal(13.0, mechanicalVentilationOperatingFlows.OperatingSupply_Lps[guid_Living], tolerance);
        }

        // =================================================================================================
        // C. One unit's resolved settings
        // =================================================================================================

        /// <summary>Resolved settings answer the manufacturer's rule in every mode, on the unit's own sensed temperatures.</summary>
        [Fact]
        public void ResolvedSettings_AnswerTheManufacturersRuleInEveryMode()
        {
            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings = Settings();

            Assert.Null(mechanicalVentilationGuidanceSettings.Refusal());

            Assert.Equal(15.0, mechanicalVentilationGuidanceSettings.SupplyTemperature(15.0, 21.0, 30.0, out VentilationUnitOperatingMode ventilationUnitOperatingMode_Bypass, out double airFlow_Bypass), tolerance);
            Assert.Equal(VentilationUnitOperatingMode.SummerBypass, ventilationUnitOperatingMode_Bypass);
            Assert.Equal(30.0, airFlow_Bypass, tolerance);

            Assert.Equal(18.0, mechanicalVentilationGuidanceSettings.SupplyTemperature(10.0, 20.0, 30.0, out VentilationUnitOperatingMode ventilationUnitOperatingMode_Recovery, out _), tolerance);
            Assert.Equal(VentilationUnitOperatingMode.HeatCoolthRecovery, ventilationUnitOperatingMode_Recovery);

            Assert.Equal(18.0, mechanicalVentilationGuidanceSettings.SupplyTemperature(30.0, 24.0, 30.0, out VentilationUnitOperatingMode ventilationUnitOperatingMode_Cooling, out double airFlow_Cooling), tolerance);
            Assert.Equal(VentilationUnitOperatingMode.Cooling, ventilationUnitOperatingMode_Cooling);
            Assert.Equal(80.0, airFlow_Cooling, tolerance);
        }

        /// <summary>Settings whose strategy is not resolved for a dwelling refuse, and answer nothing.</summary>
        [Fact]
        public void SettingsWhoseStrategyIsUnresolved_Refuse()
        {
            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings = Settings();
            mechanicalVentilationGuidanceSettings.OperatingStrategy = mechanicalVentilationGuidanceSettings.OperatingStrategy.WithElevatedAirFlow(double.NaN);

            Assert.NotNull(mechanicalVentilationGuidanceSettings.Refusal());
            Assert.True(double.IsNaN(mechanicalVentilationGuidanceSettings.SupplyTemperature(15.0, 21.0, 30.0, out VentilationUnitOperatingMode ventilationUnitOperatingMode, out _)));
            Assert.Equal(VentilationUnitOperatingMode.Undefined, ventilationUnitOperatingMode);
        }

        /// <summary>
        /// Settings that read a published table in one of their modes and state none refuse - and settings
        /// whose modes need no table do not.
        /// </summary>
        [Fact]
        public void SettingsThatReadATableAndStateNone_Refuse()
        {
            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings = Settings();
            mechanicalVentilationGuidanceSettings.SupplyAirTemperatureTable = null;

            Assert.NotNull(mechanicalVentilationGuidanceSettings.Refusal());
            Assert.Contains("published supply-air temperature table", mechanicalVentilationGuidanceSettings.Refusal());

            mechanicalVentilationGuidanceSettings.OperatingStrategy.CoolingSupplyTemperatureRule = SupplyTemperatureRule.OutdoorAir();

            Assert.Null(mechanicalVentilationGuidanceSettings.Refusal());
        }

        /// <summary>
        /// Settings with no traceable guidance source refuse: a persisted result nobody can revalidate
        /// against a document is not evidence of manufacturer behaviour.
        /// </summary>
        [Fact]
        public void SettingsWithNoGuidanceSource_Refuse()
        {
            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings = Settings();
            mechanicalVentilationGuidanceSettings.SourceIdentifier = "   ";

            Assert.NotNull(mechanicalVentilationGuidanceSettings.Refusal());
            Assert.Contains("guidance source", mechanicalVentilationGuidanceSettings.Refusal());
        }

        /// <summary>Settings survive a round trip, and a copy of them is independent of its original.</summary>
        [Fact]
        public void Settings_RoundTripAndCopyIndependently()
        {
            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings = Settings();

            JsonObject jsonObject = mechanicalVentilationGuidanceSettings.ToJsonObject();

            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings_RoundTrip = new(jsonObject);

            Assert.Null(mechanicalVentilationGuidanceSettings_RoundTrip.Refusal());
            Assert.Equal(mechanicalVentilationGuidanceSettings.SourceIdentifier, mechanicalVentilationGuidanceSettings_RoundTrip.SourceIdentifier);
            Assert.Equal(22.0, mechanicalVentilationGuidanceSettings_RoundTrip.OperatingStrategy.CoolingActivationTemperature_C, tolerance);
            Assert.Equal(80.0, mechanicalVentilationGuidanceSettings_RoundTrip.OperatingStrategy.ElevatedAirFlow_Lps, tolerance);

            Assert.Equal(
                mechanicalVentilationGuidanceSettings.SupplyTemperature(30.0, 24.0, 30.0, out _, out _),
                mechanicalVentilationGuidanceSettings_RoundTrip.SupplyTemperature(30.0, 24.0, 30.0, out _, out _),
                tolerance);

            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings_Copy = new(mechanicalVentilationGuidanceSettings);
            mechanicalVentilationGuidanceSettings_Copy.OperatingStrategy.CoolingActivationTemperature_C = 25.0;

            Assert.Equal(22.0, mechanicalVentilationGuidanceSettings.OperatingStrategy.CoolingActivationTemperature_C, tolerance);
        }

        // =================================================================================================
        // Fixtures
        // =================================================================================================

        /// <summary>A three-room habitable supply distribution: 13 / 11 / 6 l/s, totalling 30 l/s.</summary>
        private static Dictionary<Guid, double> DesignSupply()
        {
            return new Dictionary<Guid, double>
            {
                [guid_Living] = 13.0,
                [guid_Bedroom1] = 11.0,
                [guid_Bedroom2] = 6.0,
            };
        }

        /// <summary>A two-room wet-room extract distribution: 13 / 8 l/s, totalling 21 l/s.</summary>
        private static Dictionary<Guid, double> DesignExtract()
        {
            return new Dictionary<Guid, double>
            {
                [guid_Kitchen] = 13.0,
                [guid_Bathroom] = 8.0,
            };
        }

        private static MechanicalVentilationGuidanceSettings Settings()
        {
            return new MechanicalVentilationGuidanceSettings
            {
                SourceIdentifier = "Test Fixture, manufacturer modelling guidance, v.1 - not a real product",
                SupplyAirTemperatureTable = Table(),
                OperatingStrategy = new VentilationUnitOperatingStrategy
                {
                    Source = "Test Fixture, manufacturer modelling guidance, v.1 - not a real product",
                    CoolingActivationTemperature_C = 22.0,
                    MinimumCoolingActivationTemperature_C = 22.0,
                    MaximumCoolingActivationTemperature_C = 25.0,
                    BypassMinimumIntakeTemperature_C = 12.0,
                    BypassMinimumExtractTemperature_C = 18.0,
                    ElevatedAirFlow_Lps = 80.0,
                    MinimumElevatedAirFlow_Lps = 70.0,
                    MaximumElevatedAirFlow_Lps = 90.0,
                    SummerBypassSupplyTemperatureRule = SupplyTemperatureRule.OutdoorAir(),
                    HeatCoolthRecoverySupplyTemperatureRule = SupplyTemperatureRule.LinearBlend(0.8),
                    CoolingSupplyTemperatureRule = SupplyTemperatureRule.PerformanceTable(16.0),
                },
            };
        }

        /// <summary>A fixture supply-air temperature table over intake temperature, extract temperature and airflow.</summary>
        private static VentilationUnitPerformanceTable Table()
        {
            return new VentilationUnitPerformanceTable(
                [
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, VentilationUnitPerformanceAxis.Unit_DegreesCelsius, [25.0, 30.0]),
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature, VentilationUnitPerformanceAxis.Unit_DegreesCelsius, [23.0, 24.0]),
                    new VentilationUnitPerformanceAxis(VentilationUnitPerformanceAxis.Name_AirFlowRate, VentilationUnitPerformanceAxis.Unit_LitresPerSecond, [70.0, 80.0]),
                ],
                [
                    new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, VentilationUnitPerformanceOutput.Unit_DegreesCelsius, [14.0, 14.0, 15.0, 15.0, 17.0, 17.0, 18.0, 18.0]),
                ]);
        }

        private static double Total(IReadOnlyDictionary<Guid, double> dictionary)
        {
            double result = 0;

            foreach (double value in dictionary.Values)
            {
                result += value;
            }

            return result;
        }
    }
}
