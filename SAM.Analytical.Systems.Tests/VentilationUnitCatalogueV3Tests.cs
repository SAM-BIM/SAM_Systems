// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// SAM#123: the catalogue reader's support for a template carrying its manufacturer's own modelling
    /// <c>OperatingStrategy</c>, tagged <c>VentilationUnitCatalogue:v3</c> - and the shipped domestic hybrid
    /// entry's transcribed strategy.
    /// <para>
    /// <b>v3 changes nothing about what v1 and v2 meant.</b> A file of any accepted version without the
    /// field reads exactly as it always did. What v3 adds is only that a template <i>may</i> carry a
    /// strategy, validated exactly as every other optional field is: present-and-usable is read,
    /// present-and-unusable refuses the whole catalogue, absent is legal.
    /// </para>
    /// <para>
    /// <b>The shipped figures are pinned against the source string that traces them.</b> The manufacturer's
    /// guidance document itself is held privately under a redistribution restriction and is not in this
    /// repository: what is committed is the transcription and its provenance, and the assertions below are
    /// what stop either drifting silently. The fixtures, as everywhere else in this suite, name no real
    /// product.
    /// </para>
    /// <para>
    /// <b>A strategy is manufacturer modelling guidance and is not certified performance.</b> The shipped
    /// entry carries a strategy and still carries neither <c>HeatRecoveryPerformance</c> nor
    /// <c>FanPerformance</c> - the E1/E2 evidence gates are untouched by it, and a test below pins that.
    /// </para>
    /// </summary>
    public class VentilationUnitCatalogueV3Tests
    {
        private const double tolerance = 1e-9;

        // =================================================================================================
        // A. The reader
        // =================================================================================================

        [Fact]
        public void V3Schema_WithNoStrategy_ReadsExactlyAsV1Would()
        {
            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v3", Entry("FIXTURE-60"));

            try
            {
                List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(directory);

                Assert.NotNull(ventilationUnitTemplates);
                Assert.Null(Assert.Single(ventilationUnitTemplates).OperatingStrategy);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void V3Schema_WithAStatedStrategy_ReadsIt()
        {
            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v3", Entry("FIXTURE-60", strategy: true));

            try
            {
                List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(directory);

                Assert.NotNull(ventilationUnitTemplates);

                VentilationUnitOperatingStrategy ventilationUnitOperatingStrategy = Assert.Single(ventilationUnitTemplates).OperatingStrategy;

                Assert.NotNull(ventilationUnitOperatingStrategy);
                Assert.Null(ventilationUnitOperatingStrategy.TemplateRefusal());

                //A catalogue states the manufacturer's range; the dwelling's own elevated airflow is not a
                //catalogue fact, so the entry is complete and not yet operable.
                Assert.False(ventilationUnitOperatingStrategy.IsResolved);
                Assert.NotNull(ventilationUnitOperatingStrategy.Refusal());

                Assert.Equal(VentilationUnitOperatingMode.Cooling, ventilationUnitOperatingStrategy.WithElevatedAirFlow(80).OperatingMode(15.0, 22.1));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// A strategy that is present and unusable refuses the whole catalogue, rather than being read as
        /// "not stated" - the same rule every other optional field follows, for the same reason.
        /// </summary>
        [Theory]
        [InlineData("Null")]
        [InlineData("NoSource")]
        [InlineData("SetpointOutsideRange")]
        [InlineData("NoRecoveryRule")]
        [InlineData("BlendFractionAboveOne")]
        [InlineData("HalfStatedRange")]
        public void V3Schema_WithAnUnusableStrategy_RefusesTheWholeCatalogue(string defect)
        {
            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v3", Entry("FIXTURE-60", strategy: true, defect: defect));

            try
            {
                Assert.Null(Query.VentilationUnitTemplates(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>A strategy is legal on a v1 or v2 file too - the tag records the vocabulary, it does not gate it.</summary>
        [Theory]
        [InlineData("VentilationUnitCatalogue:v1")]
        [InlineData("VentilationUnitCatalogue:v2")]
        public void AnEarlierSchema_WithAStatedStrategy_StillReadsIt(string schema)
        {
            string directory = TemporaryCatalogue(schema, Entry("FIXTURE-60", strategy: true));

            try
            {
                List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(directory);

                Assert.NotNull(ventilationUnitTemplates);
                Assert.NotNull(Assert.Single(ventilationUnitTemplates).OperatingStrategy);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        // =================================================================================================
        // B. The shipped entry
        // =================================================================================================

        /// <summary>
        /// The shipped domestic hybrid entry carries the manufacturer's stated strategy, figure for figure,
        /// and every figure is traceable to the source string beside it.
        /// </summary>
        [Fact]
        public void TheShippedHybridEntry_CarriesItsManufacturersStatedStrategy()
        {
            VentilationUnitTemplate ventilationUnitTemplate = ShippedHybridTemplate();

            VentilationUnitOperatingStrategy ventilationUnitOperatingStrategy = ventilationUnitTemplate.OperatingStrategy;

            Assert.NotNull(ventilationUnitOperatingStrategy);
            Assert.Null(ventilationUnitOperatingStrategy.TemplateRefusal());

            Assert.Equal(22.0, ventilationUnitOperatingStrategy.CoolingActivationTemperature_C, tolerance);
            Assert.Equal(22.0, ventilationUnitOperatingStrategy.MinimumCoolingActivationTemperature_C, tolerance);
            Assert.Equal(25.0, ventilationUnitOperatingStrategy.MaximumCoolingActivationTemperature_C, tolerance);
            Assert.Equal(12.0, ventilationUnitOperatingStrategy.BypassMinimumIntakeTemperature_C, tolerance);
            Assert.Equal(19.0, ventilationUnitOperatingStrategy.BypassMinimumExtractTemperature_C, tolerance);
            Assert.Equal(60.0, ventilationUnitOperatingStrategy.MinimumElevatedAirFlow_Lps, tolerance);
            Assert.Equal(120.0, ventilationUnitOperatingStrategy.MaximumElevatedAirFlow_Lps, tolerance);
            Assert.Equal(80.0, ventilationUnitOperatingStrategy.DefaultElevatedAirFlow_Lps, tolerance);
            Assert.True(double.IsNaN(ventilationUnitOperatingStrategy.ElevatedAirFlow_Lps));

            Assert.Equal(SupplyTemperatureRuleType.OutdoorAir, ventilationUnitOperatingStrategy.SummerBypassSupplyTemperatureRule.SupplyTemperatureRuleType);

            Assert.Equal(SupplyTemperatureRuleType.LinearBlend, ventilationUnitOperatingStrategy.HeatCoolthRecoverySupplyTemperatureRule.SupplyTemperatureRuleType);
            Assert.Equal(0.8, ventilationUnitOperatingStrategy.HeatCoolthRecoverySupplyTemperatureRule.ExtractFraction, tolerance);

            //Cooling (Nuaire, 24 Sep 2026): exchanger, then the DX drop less the supply-motor heat, never below
            //13 degC, nothing stated outside the presentation's 60-120 l/s.
            SupplyTemperatureRule supplyTemperatureRule_Cooling = ventilationUnitOperatingStrategy.CoolingSupplyTemperatureRule;
            Assert.Equal(SupplyTemperatureRuleType.ExchangerThenCoil, supplyTemperatureRule_Cooling.SupplyTemperatureRuleType);
            Assert.Equal(new[] { 60.0, 80.0, 100.0, 120.0 }, supplyTemperatureRule_Cooling.AirFlowRates_Lps);
            Assert.Equal(new[] { 0.8796, 0.8576, 0.8356, 0.8136 }, supplyTemperatureRule_Cooling.ExtractFractions);
            Assert.Equal(new[] { 9.265, 8.745, 8.225, 7.705 }, supplyTemperatureRule_Cooling.CoilTemperatureDrops_K);
            Assert.Equal(new[] { 0.3, 0.5, 0.8, 1.1 }, supplyTemperatureRule_Cooling.FanTemperatureRises_K);
            Assert.Equal(13.0, supplyTemperatureRule_Cooling.MinimumSupplyTemperature_C, tolerance);
            Assert.Equal(PerformanceDomainPolicy.Refuse, supplyTemperatureRule_Cooling.PerformanceDomainPolicy);

            //At 90 l/s the figures are the manufacturer's own IES switching formula (0.8466, 7.84 K).
            Assert.Equal(0.8466, supplyTemperatureRule_Cooling.ExchangerExtractFraction(90.0), 1e-9);
            Assert.Equal(7.84, supplyTemperatureRule_Cooling.CoilNetTemperatureDrop_K(90.0), 0.006);

            //Cooling is switched by the room cooling-stat; bypass and recovery stay on the extract.
            Assert.Equal(CoolingActivationSignal.RoomTemperature, ventilationUnitOperatingStrategy.CoolingActivationSignal);
        }

        /// <summary>
        /// The strategy's own source names the manufacturer's guidance, records that the document is held
        /// privately rather than committed, and says in terms that it is modelling guidance and not
        /// certified performance.
        /// </summary>
        [Fact]
        public void TheShippedStrategy_TracesToPrivatelyHeldManufacturerGuidance()
        {
            string source = ShippedHybridTemplate().OperatingStrategy.Source;

            Assert.False(string.IsNullOrWhiteSpace(source));

            Assert.Contains("2026-09-18", source, StringComparison.Ordinal);
            Assert.Contains("HELD PRIVATELY", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NOT CERTIFIED PERFORMANCE", source, StringComparison.OrdinalIgnoreCase);

            //The document's own restriction is why nothing of it but these figures is in the repository.
            Assert.Contains("not committed", source, StringComparison.OrdinalIgnoreCase);

            //The cooling rule cites the reply that superseded the 13 Aug 2025 offset rule, and claims no approval.
            Assert.Contains("13 Aug 2025", source, StringComparison.Ordinal);
            Assert.Contains("24 SEP 2026", source, StringComparison.Ordinal);
            Assert.Contains("has not described it as certified or approved", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Carrying a manufacturer's modelling strategy does not make a certified figure appear. The
        /// external evidence gates are exactly where they were.
        /// </summary>
        [Fact]
        public void TheShippedHybridEntry_StillCarriesNoCertifiedPerformance()
        {
            VentilationUnitTemplate ventilationUnitTemplate = ShippedHybridTemplate();

            Assert.NotNull(ventilationUnitTemplate.OperatingStrategy);
            Assert.Null(ventilationUnitTemplate.HeatRecoveryPerformance);
            Assert.Null(ventilationUnitTemplate.FanPerformance);
        }

        /// <summary>
        /// The commercial entry carries no strategy: no manufacturer control guidance was published for it,
        /// and none was invented. Absent is the legal "not supplied" state, never a null or a borrowed one.
        /// </summary>
        [Fact]
        public void TheShippedCommercialEntry_CarriesNoStrategy()
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(Directory_Resources());

            Assert.NotNull(ventilationUnitTemplates);

            VentilationUnitTemplate ventilationUnitTemplate = ventilationUnitTemplates.Find(x => x.OperatingStrategy == null);

            Assert.NotNull(ventilationUnitTemplate);
            Assert.NotEqual(ShippedHybridTemplate().VentilationUnitReference.ToString(), ventilationUnitTemplate.VentilationUnitReference.ToString());
        }

        /// <summary>
        /// The shipped strategy and the shipped cooling table are the same product's data and work together:
        /// resolved for a dwelling, the cooling mode reads that table, and the two background modes do not.
        /// </summary>
        [Fact]
        public void TheShippedStrategyAndTable_AnswerTogether()
        {
            VentilationUnitTemplate ventilationUnitTemplate = ShippedHybridTemplate();

            VentilationUnitOperatingStrategy ventilationUnitOperatingStrategy = ventilationUnitTemplate.OperatingStrategy.WithElevatedAirFlow(90.0);

            Assert.Null(ventilationUnitOperatingStrategy.Refusal());

            //Bypass: intake air (room below the stat, extract above intake and 19 degC).
            Assert.Equal(15.0, ventilationUnitOperatingStrategy.SupplyTemperature(15.0, 21.0, 21.0, 30.0, ventilationUnitTemplate.PerformanceTable, out VentilationUnitOperatingMode ventilationUnitOperatingMode_Bypass, out double airFlow_Bypass), tolerance);
            Assert.Equal(VentilationUnitOperatingMode.SummerBypass, ventilationUnitOperatingMode_Bypass);
            Assert.Equal(30.0, airFlow_Bypass, tolerance);

            //Not above 19 degC extract: recovery (it was bypass under the earlier 18 degC threshold).
            Assert.Equal((0.8 * 18.5) + (0.2 * 15.0), ventilationUnitOperatingStrategy.SupplyTemperature(15.0, 18.5, 18.5, 30.0, ventilationUnitTemplate.PerformanceTable, out VentilationUnitOperatingMode ventilationUnitOperatingMode_Below19, out _), tolerance);
            Assert.Equal(VentilationUnitOperatingMode.HeatCoolthRecovery, ventilationUnitOperatingMode_Below19);

            //Recovery: the stated blend - cold intake, room below the stat.
            Assert.Equal((0.8 * 25.0) + (0.2 * 5.0), ventilationUnitOperatingStrategy.SupplyTemperature(5.0, 25.0, 20.0, 30.0, ventilationUnitTemplate.PerformanceTable, out VentilationUnitOperatingMode ventilationUnitOperatingMode_Recovery, out _), tolerance);
            Assert.Equal(VentilationUnitOperatingMode.HeatCoolthRecovery, ventilationUnitOperatingMode_Recovery);

            //Cooling: room above the stat -> coolth recovery at 90 l/s, then the 7.835 K net coil drop.
            double supplyTemperature_C = ventilationUnitOperatingStrategy.SupplyTemperature(32.0, 24.0, 23.0, 30.0, ventilationUnitTemplate.PerformanceTable, out VentilationUnitOperatingMode ventilationUnitOperatingMode_Cooling, out double airFlow_Cooling);

            Assert.Equal(VentilationUnitOperatingMode.Cooling, ventilationUnitOperatingMode_Cooling);
            Assert.Equal(90.0, airFlow_Cooling, tolerance);
            Assert.Equal((0.8466 * 24.0) + (0.1534 * 32.0) - 7.835, supplyTemperature_C, tolerance);

            //Cooling in bypass, and the 13 degC limit once bypassed intake less the drop would fall below it.
            Assert.Equal(22.0 - 7.835, ventilationUnitOperatingStrategy.SupplyTemperature(22.0, 23.0, 23.0, 30.0, ventilationUnitTemplate.PerformanceTable, out _, out _), tolerance);
            Assert.Equal(13.0, ventilationUnitOperatingStrategy.SupplyTemperature(19.0, 23.0, 23.0, 30.0, ventilationUnitTemplate.PerformanceTable, out _, out _), tolerance);

            //Outside the stated 60-120 l/s nothing is stated.
            Assert.NotNull(ventilationUnitTemplate.OperatingStrategy.WithElevatedAirFlow(50.0).Refusal());

            //A room-stat entry is never evaluated on the extract alone.
            Assert.True(double.IsNaN(ventilationUnitOperatingStrategy.SupplyTemperature(32.0, 24.0, 30.0, ventilationUnitTemplate.PerformanceTable, out VentilationUnitOperatingMode ventilationUnitOperatingMode_NoRoom, out _)));
            Assert.Equal(VentilationUnitOperatingMode.Undefined, ventilationUnitOperatingMode_NoRoom);
        }

        /// <summary>
        /// The shipped entry resolves to manufacturer-guidance settings for a dwelling: the elevated operating
        /// airflow is the manufacturer's stated default (80 l/s), within the unit's capacity, the cooling rule
        /// states its figures there, and the product's table carries the capacity bound the grounding reads.
        /// </summary>
        [Fact]
        public void TheShippedHybridEntry_ResolvesToGuidanceSettings()
        {
            VentilationUnitTemplate ventilationUnitTemplate = ShippedHybridTemplate();

            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings = ventilationUnitTemplate.MechanicalVentilationGuidanceSettings(out string refusal);

            Assert.Null(refusal);
            Assert.NotNull(mechanicalVentilationGuidanceSettings);
            Assert.Null(mechanicalVentilationGuidanceSettings.Refusal());

            VentilationUnitOperatingStrategy strategy = mechanicalVentilationGuidanceSettings.OperatingStrategy;
            Assert.Equal(80.0, strategy.ElevatedAirFlow_Lps, tolerance);
            Assert.Equal(0.8576, strategy.CoolingSupplyTemperatureRule.ExchangerExtractFraction(strategy.ElevatedAirFlow_Lps), 1e-9);
            Assert.Equal(8.245, strategy.CoolingSupplyTemperatureRule.CoilNetTemperatureDrop_K(strategy.ElevatedAirFlow_Lps), 1e-9);
            Assert.Null(strategy.CoolingSupplyTemperatureRule.AirFlowDomainCondition(strategy.ElevatedAirFlow_Lps));
            Assert.NotNull(mechanicalVentilationGuidanceSettings.SupplyAirTemperatureTable.Output(VentilationUnitPerformanceOutput.Name_CombinedCoolingCapacity));
            Assert.Contains("MRXBOX", mechanicalVentilationGuidanceSettings.SourceIdentifier, StringComparison.Ordinal);

            //The catalogue entry itself stays unresolved - a dwelling's choice is never written back into it.
            Assert.True(double.IsNaN(ventilationUnitTemplate.OperatingStrategy.ElevatedAirFlow_Lps));
        }

        /// <summary>
        /// A dwelling's own cooling airflow (commissioned data) takes precedence over the stated default, within
        /// the stated 60-120 l/s; outside it the settings refuse rather than clamp.
        /// </summary>
        [Theory]
        [InlineData(60.0, true)]
        [InlineData(100.0, true)]
        [InlineData(120.0, true)]
        [InlineData(59.0, false)]
        [InlineData(125.0, false)]
        public void ADwellingsOwnCoolingAirflow_OverridesTheDefault_WithinTheStatedRange(double elevated_Lps, bool accepted)
        {
            VentilationUnitTemplate ventilationUnitTemplate = ShippedHybridTemplate();
            ventilationUnitTemplate.OperatingStrategy = ventilationUnitTemplate.OperatingStrategy.WithElevatedAirFlow(elevated_Lps);

            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings = ventilationUnitTemplate.MechanicalVentilationGuidanceSettings(out string refusal);

            Assert.Equal(accepted, mechanicalVentilationGuidanceSettings != null);
            Assert.Equal(accepted, refusal == null);
            if (accepted)
            {
                Assert.Equal(elevated_Lps, mechanicalVentilationGuidanceSettings.OperatingStrategy.ElevatedAirFlow_Lps, tolerance);
            }
        }

        // =================================================================================================
        // Fixtures
        // =================================================================================================

        private static VentilationUnitTemplate ShippedHybridTemplate()
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(Directory_Resources());

            Assert.NotNull(ventilationUnitTemplates);

            VentilationUnitTemplate result = ventilationUnitTemplates.Find(x => x.OperatingStrategy != null);

            Assert.NotNull(result);

            return result;
        }

        private static string TemporaryCatalogue(string schema, string entry)
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_VentilationUnitCatalogueV3_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            File.WriteAllText(
                Path.Combine(directory, Query.VentilationUnitCatalogueFileName),
                "{ \"Schema\": \"" + schema + "\", \"Templates\": [" + entry + "] }");

            return directory;
        }

        private static string Entry(string model, bool strategy = false, string defect = null)
        {
            List<string> fields =
            [
                "\"_type\": \"SAM.Analytical.VentilationUnitTemplate,SAM.Analytical\"",
                "\"VentilationUnitReference\": { \"_type\": \"SAM.Analytical.VentilationUnitReference,SAM.Analytical\", \"Manufacturer\": \"Test Fixture\", \"Model\": \"" + model + "\" }",
                "\"MaximumSupplyFlowRate_Lps\": 60",
                "\"MaximumExtractFlowRate_Lps\": 60",
                "\"Source\": \"Test Fixture, Performance Data, v.1 - not a real product\"",
                "\"Rank\": 0",
            ];

            if (strategy)
            {
                if (defect == "Null")
                {
                    fields.Add("\"OperatingStrategy\": null");
                }
                else
                {
                    fields.Add("\"OperatingStrategy\": { " + string.Join(",", StrategyFields(defect)) + " }");
                }
            }

            return "{ " + string.Join(",", fields) + " }";
        }

        private static List<string> StrategyFields(string defect)
        {
            List<string> result =
            [
                "\"_type\": \"SAM.Analytical.VentilationUnitOperatingStrategy,SAM.Analytical\"",
                "\"CoolingActivationTemperature_C\": " + (defect == "SetpointOutsideRange" ? "26.0" : "22.0"),
                "\"MinimumCoolingActivationTemperature_C\": 22.0",
                "\"MaximumCoolingActivationTemperature_C\": 25.0",
                "\"BypassMinimumIntakeTemperature_C\": 12.0",
                "\"BypassMinimumExtractTemperature_C\": 18.0",
                "\"MinimumElevatedAirFlow_Lps\": 70.0",
                "\"SummerBypassSupplyTemperatureRule\": { \"_type\": \"SAM.Analytical.SupplyTemperatureRule,SAM.Analytical\", \"SupplyTemperatureRuleType\": \"OutdoorAir\" }",
                "\"CoolingSupplyTemperatureRule\": { \"_type\": \"SAM.Analytical.SupplyTemperatureRule,SAM.Analytical\", \"SupplyTemperatureRuleType\": \"PerformanceTable\", \"MinimumSupplyTemperature_C\": 16.0 }",
            ];

            if (defect != "HalfStatedRange")
            {
                result.Add("\"MaximumElevatedAirFlow_Lps\": 90.0");
            }

            if (defect != "NoSource")
            {
                result.Add("\"Source\": \"Test Fixture, manufacturer modelling guidance, v.1 - not a real product\"");
            }

            if (defect != "NoRecoveryRule")
            {
                result.Add(
                    "\"HeatCoolthRecoverySupplyTemperatureRule\": { \"_type\": \"SAM.Analytical.SupplyTemperatureRule,SAM.Analytical\", \"SupplyTemperatureRuleType\": \"LinearBlend\", \"ExtractFraction\": "
                    + (defect == "BlendFractionAboveOne" ? "1.4" : "0.8")
                    + " }");
            }

            return result;
        }

        private static string Directory_Resources()
        {
            DirectoryInfo directoryInfo = new(AppContext.BaseDirectory);

            while (directoryInfo != null)
            {
                string result = Path.Combine(directoryInfo.FullName, "files", "resources", "Analytical", "Systems", "VentilationUnit");

                if (Directory.Exists(result) && Directory.Exists(Path.Combine(directoryInfo.FullName, "SAM_Systems", "SAM.Analytical.Systems")))
                {
                    return result;
                }

                directoryInfo = directoryInfo.Parent;
            }

            throw new DirectoryNotFoundException("The SAM_Systems repository root was not found above " + AppContext.BaseDirectory);
        }
    }
}
