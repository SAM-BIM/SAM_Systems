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
    /// Part O Iteration 3 PR5A: the catalogue reader's support for the schema evolution SAM#117 added to
    /// <c>SAM.Analytical</c> - <c>HeatRecoveryPerformance</c> and <c>FanPerformance</c> on a template, tagged
    /// <c>VentilationUnitCatalogue:v2</c>.
    /// <para>
    /// <b>v2 changes nothing about what v1 meant.</b> A v2 file with neither field states exactly what a v1
    /// file states - "no behaviour data" - and reads identically. What v2 adds is only that a template
    /// <i>may</i> carry the two new fields, each optional and each validated the same way every other
    /// optional catalogue field already is (<see cref="VentilationUnitCatalogueTests"/>): present-and-usable
    /// is read, present-and-unusable refuses the whole catalogue, absent is legal.
    /// </para>
    /// <para>
    /// No real manufacturer is named and no real figure is transcribed here - the certified MRXBOX data
    /// (E1/E2) is not yet sourced, and the shipped catalogue stays untouched by PR5A for that reason. These
    /// fixtures exist only to prove the reader's own logic.
    /// </para>
    /// </summary>
    public class VentilationUnitCatalogueV2Tests
    {
        [Fact]
        public void V2Schema_WithNeitherNewField_ReadsExactlyAsV1Would()
        {
            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v2", Entry("FIXTURE-60", "60", "60"));

            try
            {
                List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(directory);

                Assert.NotNull(ventilationUnitTemplates);
                Assert.Single(ventilationUnitTemplates);
                Assert.Null(ventilationUnitTemplates[0].HeatRecoveryPerformance);
                Assert.Null(ventilationUnitTemplates[0].FanPerformance);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void V2Schema_WithBothNewFieldsStatedAndValid_ReadsThem()
        {
            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v2", Entry("FIXTURE-60", "60", "60", heatRecovery: true, fanPerformance: true));

            try
            {
                List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(directory);

                Assert.NotNull(ventilationUnitTemplates);
                VentilationUnitTemplate ventilationUnitTemplate = Assert.Single(ventilationUnitTemplates);

                Assert.NotNull(ventilationUnitTemplate.HeatRecoveryPerformance);
                Assert.True(ventilationUnitTemplate.HeatRecoveryPerformance.IsValid);
                Assert.Equal(0.86, ventilationUnitTemplate.HeatRecoveryPerformance.SensibleHeatRecoveryEfficiency(60));

                Assert.NotNull(ventilationUnitTemplate.FanPerformance);
                Assert.True(ventilationUnitTemplate.FanPerformance.IsValid);
                Assert.Equal(0.62, ventilationUnitTemplate.FanPerformance.SpecificFanPower_WPerLps(60));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Theory]
        [InlineData("HeatRecoveryPerformance")]
        [InlineData("FanPerformance")]
        public void V2Schema_WithOneFieldPresentButNull_RefusesTheWholeCatalogue(string field)
        {
            string entry = Entry(
                "FIXTURE-60",
                "60",
                "60",
                heatRecovery: field == "HeatRecoveryPerformance" ? (bool?)null : false,
                heatRecoveryNull: field == "HeatRecoveryPerformance",
                fanPerformance: field == "FanPerformance" ? (bool?)null : false,
                fanPerformanceNull: field == "FanPerformance");

            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v2", entry);

            try
            {
                Assert.Null(Query.VentilationUnitTemplates(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void V2Schema_WithNoBasisStated_RefusesTheWholeCatalogue()
        {
            //A figure without a basis is exactly as unusable here as it is on SAM.Analytical's own type -
            //the catalogue reader must not treat "parses" as "usable".
            string entry = Entry("FIXTURE-60", "60", "60", heatRecovery: true, heatRecoveryBasis: null);

            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v2", entry);

            try
            {
                Assert.Null(Query.VentilationUnitTemplates(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void V1Schema_IsStillAccepted_AlongsideV2()
        {
            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v1", Entry("FIXTURE-60", "60", "60"));

            try
            {
                Assert.NotNull(Query.VentilationUnitTemplates(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void AnUnrecognisedSchema_StillRefuses()
        {
            string directory = TemporaryCatalogue("VentilationUnitCatalogue:v3", Entry("FIXTURE-60", "60", "60"));

            try
            {
                Assert.Null(Query.VentilationUnitTemplates(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string TemporaryCatalogue(string schema, string entry)
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_VentilationUnitCatalogueV2_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            File.WriteAllText(
                Path.Combine(directory, Query.VentilationUnitCatalogueFileName),
                "{ \"Schema\": \"" + schema + "\", \"Templates\": [" + entry + "] }");

            return directory;
        }

        private static string Entry(
            string model,
            string maximumSupply,
            string maximumExtract,
            bool? heatRecovery = false,
            bool heatRecoveryNull = false,
            string heatRecoveryBasis = "SupplyTemperatureEfficiency",
            bool? fanPerformance = false,
            bool fanPerformanceNull = false)
        {
            List<string> fields =
            [
                "\"_type\": \"SAM.Analytical.VentilationUnitTemplate,SAM.Analytical\"",
                "\"VentilationUnitReference\": { \"_type\": \"SAM.Analytical.VentilationUnitReference,SAM.Analytical\", \"Manufacturer\": \"Test Fixture\", \"Model\": \"" + model + "\" }",
                "\"MaximumSupplyFlowRate_Lps\": " + maximumSupply,
                "\"MaximumExtractFlowRate_Lps\": " + maximumExtract,
                "\"Source\": \"Test Fixture, Certified Performance, v.1 - not a real product\"",
                "\"Rank\": 0",
            ];

            if (heatRecoveryNull)
            {
                fields.Add("\"HeatRecoveryPerformance\": null");
            }
            else if (heatRecovery == true)
            {
                string basisJson = heatRecoveryBasis == null ? string.Empty : ", \"HeatRecoveryEfficiencyBasis\": \"" + heatRecoveryBasis + "\"";

                fields.Add(
                    "\"HeatRecoveryPerformance\": { \"_type\": \"SAM.Analytical.HeatRecoveryPerformance,SAM.Analytical\"," +
                    "\"PerformanceTable\": { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceTable,SAM.Analytical\"," +
                    "\"Axes\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceAxis,SAM.Analytical\", \"Name\": \"AirFlowRate\", \"Unit\": \"l/s\", \"Values\": [30,60,90,150] } ]," +
                    "\"Outputs\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceOutput,SAM.Analytical\", \"Name\": \"SensibleHeatRecoveryEfficiency\", \"Unit\": \"-\", \"Values\": [0.90,0.86,0.82,0.74] } ] }" +
                    basisJson +
                    ", \"PerformanceDomainPolicy\": \"Refuse\", \"Source\": \"Test Fixture, Certified Performance, v.1 - not a real product\" }");
            }

            if (fanPerformanceNull)
            {
                fields.Add("\"FanPerformance\": null");
            }
            else if (fanPerformance == true)
            {
                fields.Add(
                    "\"FanPerformance\": { \"_type\": \"SAM.Analytical.FanPerformance,SAM.Analytical\"," +
                    "\"PerformanceTable\": { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceTable,SAM.Analytical\"," +
                    "\"Axes\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceAxis,SAM.Analytical\", \"Name\": \"AirFlowRate\", \"Unit\": \"l/s\", \"Values\": [30,60,90,150] } ]," +
                    "\"Outputs\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceOutput,SAM.Analytical\", \"Name\": \"SpecificFanPower\", \"Unit\": \"W/(l/s)\", \"Values\": [0.50,0.62,0.80,1.40] } ] }," +
                    "\"SpecificFanPowerBasis\": \"TotalBothFans\", \"PerformanceDomainPolicy\": \"Refuse\", \"Source\": \"Test Fixture, Certified Performance, v.1 - not a real product\" }");
            }

            return "{ " + string.Join(",", fields) + " }";
        }
    }
}
