// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// <b>The shipped manufacturer ventilation unit catalogue, checked against the document it was
    /// transcribed from.</b>
    /// <para>
    /// <c>SAM.Analytical</c> owns the vocabulary and the Approved Document O selection rule and is handed
    /// descriptors; which products exist is a fact about <i>this</i> repository. So this is where a real
    /// manufacturer is named, and this is where the assertion "the file says what the brochure says"
    /// belongs.
    /// </para>
    /// <para>
    /// <b>What the tables below are, and what they are not.</b> They are a second transcription of the
    /// Nuaire selection tables, laid out the way the brochure lays them out - one block per external air
    /// temperature, one row per internal temperature, one column per published airflow. That catches the
    /// realistic failure: a flattening, ordering or off-by-one mistake between the document's layout and
    /// the catalogue's single flat array. It does not catch a misreading shared by both transcriptions,
    /// and nobody should read it as claiming to. The supply air temperatures were separately confirmed
    /// against the engineering spreadsheet that preceded this work, which holds the same 3 x 4 x 8 table.
    /// </para>
    /// <para>
    /// <b>The capacity tests are the point of the exercise.</b> The brochure's cooling-duty table publishes
    /// airflow as a set of DUTY POINTS (50-120 l/s), and the catalogue must never read that table's largest
    /// axis value as the unit's capacity - several tests below exist purely to prove that the 120 l/s at the
    /// end of the performance table has not quietly become one. The unit's actual maximum airflow instead
    /// comes from the SAME brochure pages' separate fan static-pressure chart: each of its three fan-speed
    /// curves (319 W / 167 W / 77 W, per the Electrical and Sound Data table) ends at a distinct free-air
    /// (0 Pa) flow rate, and the highest, Curve 1, ends at 150 l/s - a real manufacturer figure, digitised
    /// from the chart's own vector paths, not the performance table's axis and not invented.
    /// </para>
    /// </summary>
    public class VentilationUnitCatalogueTests
    {
        private const string manufacturer_Nuaire = "Nuaire";

        private const string model_Nuaire = "MRXBOXAB-ECO5-AECV";

        private const string coolingModule_Nuaire = "MR-ECO-COOL-V";

        private const string model_XBC15 = "XBC15";

        /// <summary>How many products the shipped catalogue holds. Stated once, so a test that means
        /// "the whole catalogue" cannot be confused with a test that means "this one product".</summary>
        private const int count_ShippedProducts = 2;

        // =================================================================================================
        // A. The catalogue reads, and says what it is
        // =================================================================================================

        /// <summary>
        /// The shipped catalogue reads, and holds the one product it is meant to: the Nuaire hybrid unit
        /// with its cooling module, traceable to the brochure it came from.
        /// </summary>
        [Fact]
        public void TheShippedCatalogue_HoldsTheNuaireHybridUnit()
        {
            VentilationUnitTemplate ventilationUnitTemplate = Nuaire();

            Assert.True(ventilationUnitTemplate.IsValid);

            Assert.Equal(manufacturer_Nuaire, ventilationUnitTemplate.VentilationUnitReference.Manufacturer);
            Assert.Equal(model_Nuaire, ventilationUnitTemplate.VentilationUnitReference.Model);

            //The cooling module is part of the IDENTITY, because the same base unit with and without it
            //publishes different performance and is a different thing to select.
            Assert.Equal(coolingModule_Nuaire, ventilationUnitTemplate.VentilationUnitReference.Reference);
            Assert.Equal(coolingModule_Nuaire, ventilationUnitTemplate.CoolingModuleModel);

            //Every figure on the template can be traced back to a document.
            Assert.Contains("Nuaire", ventilationUnitTemplate.Source);
            Assert.Contains("MRXBOX Hybrid Cooling System", ventilationUnitTemplate.Source);
            Assert.Contains("July 2022", ventilationUnitTemplate.Source);

            //And the brochure's own caveat travels with it, rather than being lost on the way in.
            Assert.Contains("TYPICAL", ventilationUnitTemplate.Source, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The catalogue declares its schema, so a later format change can be told from a corrupt file.</summary>
        [Fact]
        public void TheShippedCatalogue_DeclaresItsSchema()
        {
            JsonObject jsonObject = Query.VentilationUnitCatalogue(Directory_Resources());

            Assert.NotNull(jsonObject);
            Assert.Equal("VentilationUnitCatalogue:v1", jsonObject["Schema"]?.ToString());
            Assert.NotNull(jsonObject["Note"]);

            //The reader and the file agree on the name, so neither can be renamed alone.
            Assert.True(File.Exists(Path.Combine(Directory_Resources(), Query.VentilationUnitCatalogueFileName)));
        }

        // =================================================================================================
        // B. The raw manufacturer data
        // =================================================================================================

        /// <summary>
        /// The published conditions are preserved exactly as conditions - three external temperatures, four
        /// internal, eight airflows - and every one of the 96 points is kept for both published quantities.
        /// <para>
        /// Not a slice of them, and not a curve fitted through them. The legacy route kept a single 80 l/s
        /// slice; a slice cannot answer what the unit does at 110 l/s, and an equation cannot be checked
        /// against the brochure.
        /// </para>
        /// </summary>
        [Fact]
        public void TheNuaireRawAxes_ArePreservedExactly()
        {
            VentilationUnitPerformanceTable ventilationUnitPerformanceTable = Nuaire().PerformanceTable;

            Assert.NotNull(ventilationUnitPerformanceTable);
            Assert.True(ventilationUnitPerformanceTable.IsValid);
            Assert.Equal(3, ventilationUnitPerformanceTable.AxisCount);

            Assert.Equal(new double[] { 29, 32, 34 }, ventilationUnitPerformanceTable.Axis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature).Values);
            Assert.Equal(new double[] { 23, 24, 25, 26 }, ventilationUnitPerformanceTable.Axis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature).Values);
            Assert.Equal(new double[] { 50, 60, 70, 80, 90, 100, 110, 120 }, ventilationUnitPerformanceTable.Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate).Values);

            Assert.Equal("degC", ventilationUnitPerformanceTable.Axis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature).Unit);
            Assert.Equal("degC", ventilationUnitPerformanceTable.Axis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature).Unit);
            Assert.Equal("l/s", ventilationUnitPerformanceTable.Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate).Unit);

            Assert.Equal(96, ventilationUnitPerformanceTable.PointCount);
            Assert.Equal(96, ventilationUnitPerformanceTable.Output(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature).Count);
            Assert.Equal(96, ventilationUnitPerformanceTable.Output(VentilationUnitPerformanceOutput.Name_CombinedCoolingCapacity).Count);

            Assert.Equal("degC", ventilationUnitPerformanceTable.Output(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature).Unit);
            Assert.Equal("kW", ventilationUnitPerformanceTable.Output(VentilationUnitPerformanceOutput.Name_CombinedCoolingCapacity).Unit);
        }

        /// <summary>
        /// Representative published values, quoted from the brochure page: the first cell of each block,
        /// the last, and a few in between, on both outputs.
        /// </summary>
        [Fact]
        public void RepresentativeNuaireValues_MatchTheBrochure()
        {
            VentilationUnitTemplate ventilationUnitTemplate = Nuaire();

            //29 degC external, 23 degC internal, 50 l/s - the top-left cell of the first block.
            Assert.Equal(14.3, ventilationUnitTemplate.SupplyAirTemperature_C(29, 23, 50));
            Assert.Equal(0.88, ventilationUnitTemplate.CombinedCoolingCapacity_kW(29, 23, 50));

            //29 / 23 / 80 - the cell the legacy spreadsheet kept a whole slice at.
            Assert.Equal(15.7, ventilationUnitTemplate.SupplyAirTemperature_C(29, 23, 80));

            //29 / 26 / 120 - the bottom-right of the first block.
            Assert.Equal(19.8, ventilationUnitTemplate.SupplyAirTemperature_C(29, 26, 120));
            Assert.Equal(1.33, ventilationUnitTemplate.CombinedCoolingCapacity_kW(29, 26, 120));

            //32 / 26 / 50 - the row the spreadsheet notes flagged as missing from an earlier transcription.
            Assert.Equal(17.3, ventilationUnitTemplate.SupplyAirTemperature_C(32, 26, 50));
            Assert.Equal(0.88, ventilationUnitTemplate.CombinedCoolingCapacity_kW(32, 26, 50));

            //34 / 23 / 120 - the largest combined cooling figure the brochure publishes.
            Assert.Equal(18.9, ventilationUnitTemplate.SupplyAirTemperature_C(34, 23, 120));
            Assert.Equal(2.20, ventilationUnitTemplate.CombinedCoolingCapacity_kW(34, 23, 120));

            //34 / 26 / 120 - the bottom-right of the whole table.
            Assert.Equal(21.2, ventilationUnitTemplate.SupplyAirTemperature_C(34, 26, 120));
            Assert.Equal(1.85, ventilationUnitTemplate.CombinedCoolingCapacity_kW(34, 26, 120));
        }

        /// <summary>
        /// <b>Every one of the 96 published points, for both quantities</b>, against a transcription laid
        /// out the way the brochure is - so a flattening or ordering mistake shows up as a specific cell
        /// rather than as a plausible table.
        /// </summary>
        [Fact]
        public void TheWholeNuaireTable_MatchesTheBrochure()
        {
            VentilationUnitTemplate ventilationUnitTemplate = Nuaire();

            double[] externals = new double[] { 29, 32, 34 };
            double[] enterings = new double[] { 23, 24, 25, 26 };
            double[] airFlowRates_Lps = new double[] { 50, 60, 70, 80, 90, 100, 110, 120 };

            double[][] supplyAirTemperatures_C = SupplyAirTemperatures_C();
            double[][] combinedCoolingCapacities_kW = CombinedCoolingCapacities_kW();

            for (int i = 0; i < externals.Length; i++)
            {
                for (int j = 0; j < enterings.Length; j++)
                {
                    for (int k = 0; k < airFlowRates_Lps.Length; k++)
                    {
                        string where = string.Format("{0} degC external, {1} degC internal, {2} l/s", externals[i], enterings[j], airFlowRates_Lps[k]);

                        Assert.True(
                            supplyAirTemperatures_C[(i * 4) + j][k] == ventilationUnitTemplate.SupplyAirTemperature_C(externals[i], enterings[j], airFlowRates_Lps[k]),
                            "Supply air temperature at " + where);

                        Assert.True(
                            combinedCoolingCapacities_kW[(i * 4) + j][k] == ventilationUnitTemplate.CombinedCoolingCapacity_kW(externals[i], enterings[j], airFlowRates_Lps[k]),
                            "Combined cooling at " + where);
                    }
                }
            }
        }

        // =================================================================================================
        // C. Capacity comes from the fan curve, never from a duty point
        // =================================================================================================

        /// <summary>
        /// <b>The resolved fact, and where it is NOT read from.</b> The brochure's cooling-duty table
        /// publishes 50-120 l/s as sample DUTY POINTS - that is not a capacity, and 120 must never appear as
        /// either maximum. The unit's real maximum comes from the separate fan static-pressure chart on the
        /// same pages: Curve 1 (319 W, the highest of the three fan-speed curves the Electrical and Sound
        /// Data table names) reaches 0 Pa - free air, no external resistance - at 150 l/s.
        /// </summary>
        [Fact]
        public void TheNuaireCapacity_IsResolvedFromTheFanCurveNotTheTablesLargestAirflow()
        {
            VentilationUnitTemplate ventilationUnitTemplate = Nuaire();

            Assert.Equal(150, ventilationUnitTemplate.MaximumSupplyFlowRate_Lps, 6);
            Assert.Equal(150, ventilationUnitTemplate.MaximumExtractFlowRate_Lps, 6);
            Assert.True(ventilationUnitTemplate.HasSelectionCapacity);
            Assert.Null(ventilationUnitTemplate.UnresolvedCapacityNote);

            //The performance table's own axis maximum is a different fact and must not equal the capacity -
            //if it ever does, the two sources have been confused with each other again.
            double tableMaximum = ventilationUnitTemplate.PerformanceTable.Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate).Maximum;
            Assert.Equal(120, tableMaximum);
            Assert.NotEqual(tableMaximum, ventilationUnitTemplate.MaximumSupplyFlowRate_Lps);

            //Traceable to the chart it was read from, not just to the brochure in general.
            Assert.Contains("fan static-pressure chart", ventilationUnitTemplate.Source);
            Assert.Contains("Curve 1", ventilationUnitTemplate.Source);
            Assert.Contains("150 l/s", ventilationUnitTemplate.Source);

            //So the product is now genuinely selectable, and it is one of the products the catalogue offers.
            Assert.NotNull(Analytical.Query.CapacityDescriptor(ventilationUnitTemplate));

            List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = Query.VentilationUnitCapacityDescriptors(Directory_Resources());

            Assert.Equal(count_ShippedProducts, ventilationUnitCapacityDescriptors.Count);
            Assert.Contains(ventilationUnitCapacityDescriptors, x => string.Equals(x.VentilationUnitReference.Model, model_Nuaire, StringComparison.Ordinal));
        }

        /// <summary>
        /// The shipped catalogue's one product is selectable, and nothing is reported as unselectable - the
        /// fan-curve figure is a stated capacity, not an absence to explain.
        /// </summary>
        [Fact]
        public void TheNuaireProduct_IsSelectableAndReportsNoUnselectableEntries()
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(Directory_Resources());

            Assert.NotNull(ventilationUnitTemplates);
            Assert.NotEmpty(ventilationUnitTemplates);

            Assert.Empty(Analytical.Query.UnselectableVentilationUnitTemplates(ventilationUnitTemplates));

            VentilationUnitCapacityDescriptor descriptor = Descriptor(Analytical.Query.CapacityDescriptors(ventilationUnitTemplates), model_Nuaire);
            Assert.Equal(150, descriptor.MaximumSupplyFlowRate_Lps, 6);
            Assert.Equal(150, descriptor.MaximumExtractFlowRate_Lps, 6);
        }

        /// <summary>
        /// A duty within the 150 l/s free-air maximum selects the Nuaire unit; a duty above it is refused -
        /// proving the chart figure actually reaches the selection kernel, both ways.
        /// </summary>
        [Fact]
        public void ADutyWithinTheFanCurveMaximum_SelectsNuaire_AboveItRefuses()
        {
            List<VentilationUnitCapacityDescriptor> descriptors = Query.VentilationUnitCapacityDescriptors(Directory_Resources());

            VentilationUnitSelection selected = Analytical.Query.SelectSmallestCapableVentilationUnit(descriptors, 100, 100);
            Assert.True(selected.IsSelected);
            Assert.Equal(model_Nuaire, selected.VentilationUnitReference.Model);
            Assert.Equal(50, selected.SupplyHeadroom_Lps, 6);

            VentilationUnitSelection refused = Analytical.Query.SelectSmallestCapableVentilationUnit(descriptors, 200, 200);
            Assert.False(refused.IsSelected);
            Assert.Null(refused.Descriptor);
            Assert.False(string.IsNullOrWhiteSpace(refused.Reason));
        }

        // =================================================================================================
        // D. The control curve
        // =================================================================================================

        /// <summary>
        /// The controller ramp is in the catalogue as data: 30% of airflow at 22 &#176;C rising to 100% at
        /// 26 &#176;C, saturating above because the source says "and above".
        /// </summary>
        [Fact]
        public void TheNuaireControlCurve_Is22DegreesTo30PercentAnd26DegreesTo100Percent()
        {
            FlowFractionControlCurve flowFractionControlCurve = Nuaire().FlowFractionByControlTemperature;

            Assert.NotNull(flowFractionControlCurve);
            Assert.True(flowFractionControlCurve.IsValid);

            Assert.Equal(new double[] { 22, 26 }, flowFractionControlCurve.ControlTemperatures_C);
            Assert.Equal(new double[] { 0.3, 1.0 }, flowFractionControlCurve.FlowFractions);

            Assert.Equal(0.3, flowFractionControlCurve.FlowFraction(22));
            Assert.Equal(1.0, flowFractionControlCurve.FlowFraction(26));
            Assert.Equal(0.65, flowFractionControlCurve.FlowFraction(24), 12);

            //"100% at 26 degrees and above" - stated on the curve, not assumed by whoever reads it.
            Assert.Equal(PerformanceDomainPolicy.ClampToDomain, flowFractionControlCurve.PerformanceDomainPolicy);
            Assert.Equal(1.0, flowFractionControlCurve.FlowFraction(31), 12);
        }

        // =================================================================================================
        // E. SAM's own extrapolation arithmetic - NOT a legacy-compatibility claim
        // =================================================================================================

        /// <summary>
        /// <b>What SAM answers outside the published domain under
        /// <see cref="PerformanceDomainPolicy.OuterCellLinearExtrapolation"/>.</b>
        /// <para>
        /// <b>This test pins SAM's explicit linear extrapolation arithmetic. It is NOT an IES or TAS
        /// compatibility test, and passing it proves nothing about either.</b> Every value below is this
        /// library continuing the straight line through the two outermost published points; Nuaire has not
        /// published any of them, none of them is in the catalogue, and none is manufacturer data.
        /// </para>
        /// <para>
        /// <b>The legacy IES spreadsheet disagrees, and that is recorded rather than chased.</b> Its derived
        /// figures give roughly 14.9 &#176;C at 26 / 23 / 80 where this policy gives 15.1, and roughly
        /// 18.9 &#176;C at 26 / 26 / 120 where this policy gives 19.4. That spreadsheet is <b>historical
        /// reference material and is not authoritative</b>: its derivation is not available, the engineer
        /// who produced it is not available, and it is not being reconstructed. Its values are deliberately
        /// not asserted anywhere - asserting them would either fail or bend this library towards a method
        /// nobody has. Should a project ever require exact compatibility with that tool, it is a separate
        /// task needing an authoritative specification or validated acceptance data.
        /// </para>
        /// <para>
        /// The three authorities this suite keeps apart:
        /// </para>
        /// <code>
        /// Nuaire raw published table   MANUFACTURER AUTHORITY           <- the catalogue, and only this
        /// SAM linear extrapolation     explicit generic policy          <- what is pinned below
        /// legacy IES spreadsheet       historical, NON-AUTHORITATIVE    <- neither stored nor asserted
        /// </code>
        /// <para>
        /// <b>Read the downward cases sceptically.</b> Below the published 29 &#176;C external floor this is
        /// a straight-line continuation of the 29-to-32 &#176;C gradient, and a hybrid coolth-recovery plus
        /// direct-expansion unit does not behave linearly there. The further below 29 &#176;C a query goes,
        /// the less its answer means. That is an argument for the default being
        /// <see cref="PerformanceDomainPolicy.Refuse"/>; it is not an endorsement of these values.
        /// </para>
        /// </summary>
        [Theory]
        //Below the published external floor.
        [InlineData(26, 23, 80, 15.1)]
        [InlineData(26, 26, 80, 16.5)]
        [InlineData(26, 23, 50, 13.6)]
        [InlineData(26, 26, 120, 19.4)]
        [InlineData(28, 23, 80, 15.5)]
        //Above the published entering ceiling.
        [InlineData(32, 27, 80, 19.1)]
        [InlineData(29, 27, 80, 18.1)]
        [InlineData(32, 28, 80, 19.7)]
        [InlineData(29, 28, 80, 18.7)]
        public void SAMsLinearExtrapolation_IsPinnedOutsideThePublishedDomain(double external_C, double entering_C, double airFlowRate_Lps, double expected_C)
        {
            VentilationUnitTemplate ventilationUnitTemplate = Nuaire();

            //Outside the published domain, so the DEFAULT refuses - which is the whole point of the policy
            //being named at the call site.
            Assert.True(double.IsNaN(ventilationUnitTemplate.SupplyAirTemperature_C(external_C, entering_C, airFlowRate_Lps)));

            Assert.Equal(
                expected_C,
                ventilationUnitTemplate.SupplyAirTemperature_C(external_C, entering_C, airFlowRate_Lps, PerformanceDomainPolicy.OuterCellLinearExtrapolation),
                6);
        }

        /// <summary>
        /// The policy changes nothing inside the published domain: a published condition returns the
        /// published number exactly, whichever policy is named.
        /// <para>
        /// This is what will make a future legacy comparison interpretable - any disagreement with the
        /// legacy workflow is then provably a disagreement about <i>extrapolation</i>, not about the
        /// transcribed manufacturer data underneath it.
        /// </para>
        /// </summary>
        [Fact]
        public void EveryPolicy_ReturnsThePublishedValueInsideThePublishedDomain()
        {
            VentilationUnitTemplate ventilationUnitTemplate = Nuaire();

            foreach (PerformanceDomainPolicy performanceDomainPolicy in new[] { PerformanceDomainPolicy.Refuse, PerformanceDomainPolicy.ClampToDomain, PerformanceDomainPolicy.OuterCellLinearExtrapolation })
            {
                Assert.Equal(15.7, ventilationUnitTemplate.SupplyAirTemperature_C(29, 23, 80, performanceDomainPolicy));
                Assert.Equal(20.2, ventilationUnitTemplate.SupplyAirTemperature_C(32, 26, 120, performanceDomainPolicy));
                Assert.Equal(2.20, ventilationUnitTemplate.CombinedCoolingCapacity_kW(34, 23, 120, performanceDomainPolicy));
            }
        }

        // =================================================================================================
        // F. A broken catalogue refuses, loudly
        // =================================================================================================

        /// <summary>
        /// Every way of breaking one entry refuses the <b>whole</b> catalogue. Skipping the bad entry would
        /// make a product quietly vanish, and a dwelling would then be told nothing offered could serve it
        /// because of a typo nobody was shown.
        /// </summary>
        [Theory]
        [InlineData("Source")]
        [InlineData("RaggedGrid")]
        [InlineData("DuplicateIdentity")]
        [InlineData("UnusableCapacity")]
        [InlineData("BrokenControlPolicy")]
        [InlineData("MissingRank")]
        [InlineData("NotAnObject")]
        [InlineData("NullPerformanceTable")]
        [InlineData("NullFlowFractionByControlTemperature")]
        [InlineData("MissingSchema")]
        [InlineData("WrongSchema")]
        [InlineData("FutureSchema")]
        public void ABrokenEntry_RefusesTheWholeCatalogue(string defect)
        {
            string directory = TemporaryCatalogue(defect);

            try
            {
                Assert.Null(Query.VentilationUnitTemplates(directory));
                Assert.Null(Query.VentilationUnitCapacityDescriptors(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>The exact schema tag the reader requires, checked against the constant it enforces - and a v1 catalogue is read end to end under it.</summary>
        [Fact]
        public void TheExactV1Schema_IsAccepted()
        {
            Assert.Equal("VentilationUnitCatalogue:v1", Query.VentilationUnitCatalogueSchema);

            string directory = TemporaryCatalogue("Resolved");

            try
            {
                Assert.NotNull(Query.VentilationUnitTemplates(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// <b>Preserves the distinction the present-but-null fix must not erase.</b> A product legitimately
        /// catalogued without performance data or a control curve - both keys simply absent, not present and
        /// null - is still accepted; only a key that is PRESENT but null or unusable refuses the catalogue.
        /// </summary>
        [Fact]
        public void AnEntryWithBothOptionalObjectsAbsent_IsStillAccepted()
        {
            string directory = TemporaryCatalogueWithAbsentOptionalData();

            try
            {
                List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(directory);

                Assert.NotNull(ventilationUnitTemplates);

                VentilationUnitTemplate ventilationUnitTemplate = Assert.Single(ventilationUnitTemplates);

                Assert.True(ventilationUnitTemplate.IsValid);
                Assert.Null(ventilationUnitTemplate.PerformanceTable);
                Assert.Null(ventilationUnitTemplate.FlowFractionByControlTemperature);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// A missing catalogue is null rather than empty, so "there is no catalogue" and "the catalogue
        /// offers nothing for this duty" stay distinguishable.
        /// </summary>
        [Fact]
        public void AMissingCatalogue_IsNullRatherThanEmpty()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_VentilationUnitCatalogue_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            try
            {
                Assert.Null(Query.VentilationUnitCatalogue(directory));
                Assert.Null(Query.VentilationUnitTemplates(directory));
                Assert.Null(Query.VentilationUnitCapacityDescriptors(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        // =================================================================================================
        // G. Deployment - the catalogue has to reach an installed SAM, not just a source checkout
        // =================================================================================================

        /// <summary>
        /// <b>The repository path and the runtime-resolved path are the same path.</b>
        /// <para>
        /// A catalogue only a source checkout can find is not a catalogue. At runtime,
        /// <c>Query.DefaultVentilationUnitDirectory</c> resolves
        /// <c>&lt;resources&gt;/Analytical/Systems/VentilationUnit</c>, where the
        /// <c>Analytical/Systems</c> part is <b>derived from this assembly's own name</b> by
        /// <c>Core.Query.ResourcesDirectory</c> - it strips the leading <c>SAM.</c> and turns the remaining
        /// dots into separators - and <c>VentilationUnit</c> comes from
        /// <see cref="AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName"/>.
        /// </para>
        /// <para>
        /// The shipped file has to sit at that same relative path under <c>files/resources</c>, because the
        /// deployment step copies that tree wholesale. This test fails if the folder is moved in the
        /// repository, if the setting is renamed, or if the assembly is renamed - each of which would
        /// silently strand the catalogue in an installed SAM while every other test here still passed
        /// against the checkout.
        /// </para>
        /// <para>
        /// <b>Machine-independent by construction.</b> It compares relative shape, not any particular
        /// machine's installed tree, so it means the same thing on a clean CI runner as on a developer box.
        /// </para>
        /// </summary>
        [Fact]
        public void TheCatalogue_SitsWhereTheRuntimeResolverWillLookForIt()
        {
            //Exactly what Core.Query.ResourcesDirectory(setting, assembly) derives from the assembly name.
            string name = typeof(Query).Assembly.GetName().Name;

            Assert.Equal("SAM.Analytical.Systems", name);

            string segment = name.Substring(4).Replace(".", Path.DirectorySeparatorChar.ToString());

            Assert.Equal("Analytical" + Path.DirectorySeparatorChar + "Systems", segment);

            //And exactly what ActiveSetting defaults the leaf directory name to.
            string leaf = ActiveSetting.GetDefault().GetValue<string>(AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName);

            Assert.Equal("VentilationUnit", leaf);

            //The shipped file must be at <resources>/<segment>/<leaf>/<file name>.
            string directory_Resources = Directory_Resources();

            string expected = Path.GetFullPath(Path.Combine(directory_Resources, "..", "..", "..", "..", "resources", segment, leaf, Query.VentilationUnitCatalogueFileName));

            Assert.True(File.Exists(expected), "The shipped catalogue is not where the runtime resolver will look: " + expected);

            //Same relative shape as the already-shipped sibling this deployment follows, so the catalogue
            //rides the mechanism that is already proven to reach an install rather than a second one.
            Assert.True(Directory.Exists(Path.Combine(Path.GetDirectoryName(directory_Resources), "SystemEnergyCentre")));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(directory_Resources), "SystemEnergyCentre", Query.CapabilityIndexFileName)));
        }

        /// <summary>
        /// The reader's file name constant and the shipped file agree, so neither can be renamed alone -
        /// a rename on one side only would deploy a file nothing opens.
        /// </summary>
        [Fact]
        public void TheReaderAndTheShippedFile_AgreeOnTheFileName()
        {
            Assert.Equal("VentilationUnitCatalogue.JSON", Query.VentilationUnitCatalogueFileName);
            Assert.True(File.Exists(Path.Combine(Directory_Resources(), Query.VentilationUnitCatalogueFileName)));

            //And the directory really is named as the setting says, on disk.
            Assert.Equal("VentilationUnit", new DirectoryInfo(Directory_Resources()).Name);
        }

        // =================================================================================================
        // H. The seam, end to end
        // =================================================================================================

        /// <summary>
        /// <b>The seam works.</b> A catalogue entry that <i>does</i> state its capacities reaches the
        /// unchanged Approved Document O selection kernel and is chosen by it - so the only thing standing
        /// between the Nuaire product and a selection is the manufacturer fact nobody has yet established.
        /// </summary>
        [Fact]
        public void AResolvedCatalogueEntry_ReachesTheSelectionKernel()
        {
            string directory = TemporaryCatalogue("Resolved");

            try
            {
                List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = Query.VentilationUnitCapacityDescriptors(directory);

                Assert.NotNull(ventilationUnitCapacityDescriptors);
                Assert.Equal(2, ventilationUnitCapacityDescriptors.Count);

                //Smallest compliant, never nearest - the kernel's own rule, over descriptors that came out
                //of a file.
                VentilationUnitSelection ventilationUnitSelection = Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors, 55, 55);

                Assert.True(ventilationUnitSelection.IsSelected);
                Assert.Equal("FIXTURE-60", ventilationUnitSelection.VentilationUnitReference.Model);
                Assert.Equal(5, ventilationUnitSelection.SupplyHeadroom_Lps, 6);

                //And a duty neither can move is refused rather than answered with the larger one.
                Assert.False(Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors, 500, 500).IsSelected);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        // =================================================================================================
        // I. The upgrade path - a Setting persisted before these two parameters existed
        // =================================================================================================

        /// <summary>
        /// <b>The Codex P1: a legacy persisted Setting must still resolve the shipped catalogue.</b>
        /// <para>
        /// <c>ActiveSetting.Load()</c> can hand back a <c>Setting</c> that was persisted before
        /// <c>DefaultVentilationUnitFileDirectory</c>/<c>DefaultVentilationUnitDirectoryName</c> existed, as-is -
        /// it is not merged with <see cref="ActiveSetting.GetDefault"/>. Both values then read back
        /// null, and <see cref="Query.DefaultVentilationUnitDirectory(Setting)"/> used to fall back straight to
        /// the resources root itself rather than that root's <c>VentilationUnit</c> child, one directory too
        /// high for <see cref="Query.VentilationUnitCatalogueFileName"/> to be found in after an upgrade.
        /// </para>
        /// <para>
        /// A <see cref="Setting"/> parameter is injected here rather than exercised through the process-wide
        /// <see cref="ActiveSetting.Setting"/>, so this test cannot leak state into any other test that reads
        /// it.
        /// </para>
        /// </summary>
        [Fact]
        public void ALegacyPersistedSetting_StillResolvesTheShippedCatalogue()
        {
            //Exactly the shape ActiveSetting.Load() hands back for a Setting persisted before either
            //ventilation-unit parameter existed: present, but with neither value.
            Setting setting_Legacy = new Setting();

            //This repository's own resources root injected explicitly - see the resourcesDirectory
            //parameter's remarks: Query.ResourcesDirectory() falls back to a real per-user SAM install
            //directory when one happens to exist on the machine running the test, which this test must not
            //depend on to be deterministic.
            string directory = Query.DefaultVentilationUnitDirectory(setting_Legacy, ResourcesRoot());

            Assert.Equal(Directory_Resources(), directory);

            //And the real shipped catalogue is reachable through it - the upgrade path, proven end to end.
            Assert.NotNull(Query.VentilationUnitCatalogue(directory));
            Assert.NotEmpty(Query.VentilationUnitTemplates(directory));
        }

        /// <summary>
        /// A fresh, never-persisted default Setting resolves a directory holding the real shipped catalogue.
        /// <para>
        /// Exercised through the REAL <see cref="ActiveSetting.GetDefault"/> and the REAL, unparameterised
        /// <see cref="Query.DefaultVentilationUnitDirectory(Setting, string)"/> resolution - unlike the other
        /// tests in this section, deliberately not pinned to <see cref="Directory_Resources"/> by exact path,
        /// because <c>GetDefault</c> itself resolves <c>DefaultVentilationUnitFileDirectory</c> against
        /// whatever real per-user SAM install <see cref="Core.Query.ResourcesDirectory()"/> finds, which can
        /// be a real installed copy of the same shipped catalogue rather than this checkout's own copy. What
        /// has to be true everywhere is that a fresh Setting resolves to SOME directory holding a usable
        /// catalogue - not to any one specific path.
        /// </para>
        /// </summary>
        [Fact]
        public void AFreshDefaultSetting_ResolvesTheShippedCatalogueDirectory()
        {
            string directory = Query.DefaultVentilationUnitDirectory(ActiveSetting.GetDefault());

            Assert.NotNull(directory);
            Assert.NotNull(Query.VentilationUnitCatalogue(directory));
            Assert.NotEmpty(Query.VentilationUnitTemplates(directory));
        }

        /// <summary>
        /// An explicit <c>DefaultVentilationUnitFileDirectory</c> wins outright, whatever the leaf name
        /// says or does not say.
        /// </summary>
        [Fact]
        public void AnExplicitVentilationUnitDirectory_IsUsedDirectly()
        {
            Setting setting = new Setting();
            setting.SetValue(AnalyticalSystemSettingParameter.DefaultVentilationUnitFileDirectory, Directory_Resources());

            Assert.Equal(Directory_Resources(), Query.DefaultVentilationUnitDirectory(setting));
        }

        /// <summary>
        /// An explicit custom leaf name - declared, just not "VentilationUnit" - is combined with the
        /// resources root rather than falling back to <see cref="ActiveSetting.GetDefault"/>'s leaf.
        /// </summary>
        [Fact]
        public void AnExplicitCustomDirectoryName_IsCombinedWithTheResourcesRoot()
        {
            Setting setting = new Setting();
            setting.SetValue(AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName, "VentilationUnit");

            //Same leaf as the default, so it round-trips to the same real directory - proving the explicit
            //name was actually read, not merely ignored in favour of the fallback that happens to agree.
            Assert.Equal(Directory_Resources(), Query.DefaultVentilationUnitDirectory(setting, ResourcesRoot()));

            //A leaf that does not exist on disk refuses rather than silently substituting anything else.
            Setting setting_Missing = new Setting();
            setting_Missing.SetValue(AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName, "NoSuchLeaf");

            Assert.Null(Query.DefaultVentilationUnitDirectory(setting_Missing, ResourcesRoot()));
        }

        /// <summary>This repository's resources root - the parent <see cref="Directory_Resources"/>'s <c>VentilationUnit</c> leaf sits under.</summary>
        private static string ResourcesRoot()
        {
            return Path.GetDirectoryName(Directory_Resources());
        }

        // =================================================================================================
        // J. The Grasshopper catalogue component's exact combination - SAMAnalyticalSystemVentilationUnitCatalogue
        //    reads VentilationUnitTemplates once and derives both its outputs from that same list, exactly
        //    as these tests do. Not a retest of B/C/H above - a check that what the component hands to
        //    Grasshopper is self-consistent, and that the two required engineering behaviours hold.
        // =================================================================================================

        /// <summary>
        /// <b>Required behaviour, Case 1: the real Nuaire product must be visible, and must be selectable.</b>
        /// The catalogue read succeeds and reports exactly the Nuaire product on the selectable side, with
        /// nothing on the unselectable side - the fan-curve maximum resolved its capacity, so there is no
        /// absence left to report.
        /// </summary>
        [Fact]
        public void ShippedCatalogue_NuaireIsVisibleAndSelectable_UnselectableListIsEmpty()
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(Directory_Resources());

            //The read itself succeeded - the fact that distinguishes "nothing unselectable because every
            //product resolved" from "empty because the catalogue could not be read", which is exactly the
            //distinction SAMAnalyticalSystemVentilationUnitCatalogue reports through separate runtime
            //messages.
            Assert.NotNull(ventilationUnitTemplates);

            List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = Analytical.Query.CapacityDescriptors(ventilationUnitTemplates);
            List<KeyValuePair<VentilationUnitTemplate, string>> unselectable = Analytical.Query.UnselectableVentilationUnitTemplates(ventilationUnitTemplates);

            Assert.Empty(unselectable);

            //Every shipped product is on the selectable side - none of them has an unresolved capacity.
            Assert.Equal(count_ShippedProducts, ventilationUnitCapacityDescriptors.Count);

            Descriptor(ventilationUnitCapacityDescriptors, model_Nuaire);
            Descriptor(ventilationUnitCapacityDescriptors, model_XBC15);

            //Selectable and unselectable together account for every template read - nothing silently
            //vanishes between the two outputs the component hands to Grasshopper.
            Assert.Equal(ventilationUnitTemplates.Count, ventilationUnitCapacityDescriptors.Count + unselectable.Count);
        }

        /// <summary>
        /// <b>Required behaviour, Case 2: a controlled fixture with two genuinely selectable products.</b>
        /// 100/100 l/s and 150/150 l/s, read back with their capacities, rank and identity preserved - and,
        /// run through the unchanged selection kernel against a 115/115 l/s dwelling duty, the 150 l/s
        /// product is chosen. Not 100 (undersized on both sides), not "nearest", and 115 is never written
        /// back anywhere as a capacity.
        /// </summary>
        [Fact]
        public void ControlledFixtureCatalogue_TwoSelectableProducts_150IsChosenFor115DwellingDuty()
        {
            string directory = TemporaryTwoProductCatalogue();

            try
            {
                List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(directory);

                Assert.NotNull(ventilationUnitTemplates);
                Assert.Equal(2, ventilationUnitTemplates.Count);

                List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = Analytical.Query.CapacityDescriptors(ventilationUnitTemplates);

                Assert.Empty(Analytical.Query.UnselectableVentilationUnitTemplates(ventilationUnitTemplates));
                Assert.Equal(2, ventilationUnitCapacityDescriptors.Count);

                VentilationUnitCapacityDescriptor descriptor_A = ventilationUnitCapacityDescriptors.Find(x => x.VentilationUnitReference.Model == "FIXTURE-A-100");
                VentilationUnitCapacityDescriptor descriptor_B = ventilationUnitCapacityDescriptors.Find(x => x.VentilationUnitReference.Model == "FIXTURE-B-150");

                Assert.NotNull(descriptor_A);
                Assert.NotNull(descriptor_B);
                Assert.Equal(100, descriptor_A.MaximumSupplyFlowRate_Lps, 6);
                Assert.Equal(100, descriptor_A.MaximumExtractFlowRate_Lps, 6);
                Assert.Equal(150, descriptor_B.MaximumSupplyFlowRate_Lps, 6);
                Assert.Equal(150, descriptor_B.MaximumExtractFlowRate_Lps, 6);

                VentilationUnitSelection ventilationUnitSelection = Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors, 115, 115);

                Assert.True(ventilationUnitSelection.IsSelected);
                Assert.Equal("FIXTURE-B-150", ventilationUnitSelection.VentilationUnitReference.Model);
                Assert.Equal(115, ventilationUnitSelection.SupplyDuty_Lps, 6);
                Assert.Equal(35, ventilationUnitSelection.SupplyHeadroom_Lps, 6);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>Two products at exactly the sizes Iteration 2's required Case 2 uses: 100/100 l/s and 150/150 l/s.</summary>
        private static string TemporaryTwoProductCatalogue()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_VentilationUnitCatalogue_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            string entries = Entry("FIXTURE-A-100", "100", "100") + "," + Entry("FIXTURE-B-150", "150", "150");

            File.WriteAllText(
                Path.Combine(directory, Query.VentilationUnitCatalogueFileName),
                "{ \"Schema\": \"VentilationUnitCatalogue:v1\", \"Templates\": [" + entries + "] }");

            return directory;
        }

        // =================================================================================================
        // K. The real selection ladder - Nuaire MRXBOX 150/150 and Nuaire XBOXER XBC15 190/190
        //    Two real products, so the "between capacities" case the shipped catalogue could not exercise
        //    with one product is now a manufacturer boundary rather than a fixture. Section J's controlled
        //    two-product fixture stays: the algorithm is tested against fixtures built for the purpose, and
        //    this section tests what the FILE says reaches that algorithm.
        // =================================================================================================

        /// <summary>
        /// The catalogue holds both Nuaire products, both selectable, with the identities, capacities and
        /// ranks the sources state - and nothing reported as unselectable.
        /// </summary>
        [Fact]
        public void TheShippedCatalogue_HoldsBothNuaireProducts()
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(Directory_Resources());

            Assert.NotNull(ventilationUnitTemplates);
            Assert.Equal(count_ShippedProducts, ventilationUnitTemplates.Count);

            Assert.Empty(Analytical.Query.UnselectableVentilationUnitTemplates(ventilationUnitTemplates));

            VentilationUnitTemplate ventilationUnitTemplate_MRXBOX = Nuaire();
            VentilationUnitTemplate ventilationUnitTemplate_XBC15 = XBC15();

            Assert.Equal(manufacturer_Nuaire, ventilationUnitTemplate_MRXBOX.VentilationUnitReference.Manufacturer);
            Assert.Equal(manufacturer_Nuaire, ventilationUnitTemplate_XBC15.VentilationUnitReference.Manufacturer);

            Assert.Equal(150, ventilationUnitTemplate_MRXBOX.MaximumSupplyFlowRate_Lps, 6);
            Assert.Equal(150, ventilationUnitTemplate_MRXBOX.MaximumExtractFlowRate_Lps, 6);
            Assert.Equal(190, ventilationUnitTemplate_XBC15.MaximumSupplyFlowRate_Lps, 6);
            Assert.Equal(190, ventilationUnitTemplate_XBC15.MaximumExtractFlowRate_Lps, 6);

            //Rank is declared, never inferred - and a missing one would have refused the whole catalogue.
            Assert.Equal(10, ventilationUnitTemplate_MRXBOX.Rank);
            Assert.Equal(20, ventilationUnitTemplate_XBC15.Rank);

            //Rank decides nothing here, and the test says so rather than leaving it implied: the two are
            //different sizes, so size separates them before rank is ever consulted.
            Assert.NotEqual(ventilationUnitTemplate_MRXBOX.MaximumSupplyFlowRate_Lps, ventilationUnitTemplate_XBC15.MaximumSupplyFlowRate_Lps);

            //The cooling module is part of the MRXBOX identity and the XBC15 has none - which is what makes
            //them two identities rather than one product described twice.
            Assert.Equal(coolingModule_Nuaire, ventilationUnitTemplate_MRXBOX.VentilationUnitReference.Reference);
            Assert.True(string.IsNullOrEmpty(ventilationUnitTemplate_XBC15.VentilationUnitReference.Reference));
            Assert.True(string.IsNullOrEmpty(ventilationUnitTemplate_XBC15.CoolingModuleModel));
        }

        /// <summary>
        /// <b>Where the XBC15's 190 l/s comes from, and what it is not.</b>
        /// <para>
        /// The catalogue publishes a Specific Fan Power table whose 100% fan-speed row is tabulated against
        /// external static pressure, and its free-air (0 Pa) end is 0.19 m3/s. That is the flow the unit
        /// delivers against no external resistance, so it is the capacity - the same free-air-endpoint
        /// reasoning as the MRXBOX entry, except that this source states it numerically instead of drawing
        /// it on a chart.
        /// </para>
        /// <para>
        /// <b>The asymmetry with the MRXBOX capacity test is deliberate.</b> For the MRXBOX the capacity had
        /// to be different from anything on its performance table, because that table is a grid of cooling
        /// duty points and its largest airflow (120 l/s) is not a fan limit. For the XBC15 the capacity IS
        /// the table's own 0 Pa entry, because that table is the fan row. So what this test guards is not
        /// "capacity differs from the table" but the thing that actually goes wrong: that a published DUTY
        /// POINT - the catalogue's 0.1 m3/s @ 100 Pa example selection, and the 75% fan-speed row's
        /// 0.14 m3/s - is never read as the capacity.
        /// </para>
        /// </summary>
        [Fact]
        public void TheXBC15Capacity_IsTheFreeAirEndOfItsPublishedFanRow()
        {
            VentilationUnitTemplate ventilationUnitTemplate = XBC15();

            Assert.True(ventilationUnitTemplate.HasSelectionCapacity);
            Assert.Null(ventilationUnitTemplate.UnresolvedCapacityNote);

            Assert.Equal(190, ventilationUnitTemplate.MaximumSupplyFlowRate_Lps, 6);
            Assert.Equal(190, ventilationUnitTemplate.MaximumExtractFlowRate_Lps, 6);

            VentilationUnitPerformanceTable ventilationUnitPerformanceTable = ventilationUnitTemplate.PerformanceTable;

            Assert.NotNull(ventilationUnitPerformanceTable);

            //The capacity IS the 0 Pa entry of the published row, expressed in l/s rather than m3/s. Read
            //off the table by index rather than restated, so the two cannot drift apart.
            double airFlowRate_FreeAir_M3s = ventilationUnitPerformanceTable.PublishedValue("AirFlowRate", 0);

            Assert.Equal(0.19, airFlowRate_FreeAir_M3s, 6);
            Assert.Equal(ventilationUnitTemplate.MaximumSupplyFlowRate_Lps, airFlowRate_FreeAir_M3s * 1000, 6);

            //And the duty points are NOT the capacity. 0.1 m3/s @ 100 Pa is the catalogue's own example
            //selection - the number a reader is most likely to mistake for the unit's rating - and the 100 Pa
            //column of this row is 0.17 m3/s, which is what the unit actually does there.
            Assert.Equal(0.17, ventilationUnitPerformanceTable.PublishedValue("AirFlowRate", 2), 6);
            Assert.NotEqual(100, ventilationUnitTemplate.MaximumSupplyFlowRate_Lps);
            Assert.NotEqual(140, ventilationUnitTemplate.MaximumSupplyFlowRate_Lps);

            //Traceable to the row it was read from, not merely to the catalogue in general.
            Assert.Contains("Specific Fan Power", ventilationUnitTemplate.Source);
            Assert.Contains("100% fan-speed row", ventilationUnitTemplate.Source);
            Assert.Contains("0.19 m3/s = 190 l/s", ventilationUnitTemplate.Source);
            Assert.Contains("is a duty point against duct resistance and is NOT the maximum", ventilationUnitTemplate.Source);
        }

        /// <summary>
        /// The XBC15 performance row is the manufacturer's table, in the manufacturer's units. Airflow stays
        /// in m3/s and specific fan power in W/l/s - neither is converted on the way in, because a converted
        /// figure cannot be checked against the page it came from.
        /// </summary>
        [Fact]
        public void TheXBC15PerformanceTable_IsTheManufacturerRowVerbatim()
        {
            VentilationUnitPerformanceTable ventilationUnitPerformanceTable = XBC15().PerformanceTable;

            Assert.NotNull(ventilationUnitPerformanceTable);
            Assert.True(ventilationUnitPerformanceTable.IsValid);

            //One axis: the published pressure points, strictly increasing, in Pa.
            Assert.Equal(1, ventilationUnitPerformanceTable.AxisCount);

            VentilationUnitPerformanceAxis ventilationUnitPerformanceAxis = ventilationUnitPerformanceTable.Axis("ExternalStaticPressure");

            Assert.NotNull(ventilationUnitPerformanceAxis);
            Assert.Equal("Pa", ventilationUnitPerformanceAxis.Unit);
            Assert.Equal(new double[] { 0, 50, 100, 200, 300, 400, 500, 600, 700 }, ventilationUnitPerformanceAxis.Values);
            Assert.Equal(9, ventilationUnitPerformanceTable.PointCount);

            VentilationUnitPerformanceOutput ventilationUnitPerformanceOutput_AirFlowRate = ventilationUnitPerformanceTable.Output("AirFlowRate");
            VentilationUnitPerformanceOutput ventilationUnitPerformanceOutput_SpecificFanPower = ventilationUnitPerformanceTable.Output("SpecificFanPower");

            Assert.NotNull(ventilationUnitPerformanceOutput_AirFlowRate);
            Assert.NotNull(ventilationUnitPerformanceOutput_SpecificFanPower);

            //THE UNITS THE SOURCE PRINTS, not SAM's preferred ones.
            Assert.Equal("m3/s", ventilationUnitPerformanceOutput_AirFlowRate.Unit);
            Assert.Equal("W/l/s", ventilationUnitPerformanceOutput_SpecificFanPower.Unit);

            Assert.Equal(new double[] { 0.19, 0.18, 0.17, 0.15, 0.13, 0.11, 0.09, 0.07, 0.05 }, ventilationUnitPerformanceOutput_AirFlowRate.Values);
            Assert.Equal(new double[] { 1.80, 1.82, 1.88, 2.13, 2.55, 3.15, 3.92, 4.87, 5.99 }, ventilationUnitPerformanceOutput_SpecificFanPower.Values);
        }

        /// <summary>
        /// <b>The Iteration 3 boundary, asserted rather than assumed.</b> Nuaire publishes no cooling
        /// module, no entering or external dry-bulb conditions, no supply air temperature, no heat exchanger
        /// table and no controller ramp for the XBC range, and none was manufactured to make the two entries
        /// look alike. A common performance model across the two products would have to invent every one of
        /// these, and this test is what fails if somebody does.
        /// </summary>
        [Fact]
        public void TheXBC15Entry_CarriesNoCoolingOrControlDataNobodyPublished()
        {
            VentilationUnitTemplate ventilationUnitTemplate = XBC15();

            //No controller ramp. ABSENT is the legal "not supplied" state; a present-but-null key would have
            //refused the whole catalogue, so the catalogue having loaded at all is half of this assertion.
            Assert.Null(ventilationUnitTemplate.FlowFractionByControlTemperature);

            VentilationUnitPerformanceTable ventilationUnitPerformanceTable = ventilationUnitTemplate.PerformanceTable;

            Assert.DoesNotContain(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, ventilationUnitPerformanceTable.AxisNames);
            Assert.DoesNotContain(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature, ventilationUnitPerformanceTable.AxisNames);
            Assert.DoesNotContain(VentilationUnitPerformanceAxis.Name_ControlTemperature, ventilationUnitPerformanceTable.AxisNames);

            Assert.DoesNotContain(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, ventilationUnitPerformanceTable.OutputNames);
            Assert.DoesNotContain(VentilationUnitPerformanceOutput.Name_CombinedCoolingCapacity, ventilationUnitPerformanceTable.OutputNames);

            //The two products therefore publish DIFFERENT KINDS of performance data, and the catalogue holds
            //each as its source states it. Only the MRXBOX has cooling and control data.
            Assert.NotNull(Nuaire().FlowFractionByControlTemperature);
            Assert.Contains(VentilationUnitPerformanceOutput.Name_CombinedCoolingCapacity, Nuaire().PerformanceTable.OutputNames);
        }

        /// <summary>
        /// <b>The real 130 / 150 / 190 l/s ladder.</b>
        /// <code>
        /// duty 131/131   a 130 l/s product is INCAPABLE          -> MRXBOX 150, 19 l/s headroom
        /// duty 150/150   MRXBOX exactly on its rating            -> MRXBOX 150,  0 l/s headroom
        /// duty 160/160   MRXBOX incapable, XBC15 capable         -> XBC15  190, 30 l/s headroom
        /// duty 190/190   XBC15 exactly on its rating             -> XBC15  190,  0 l/s headroom
        /// duty 191/191   nothing offered can move it             -> refused, naming 190
        /// </code>
        /// <para>
        /// The 130 l/s rung is a controlled descriptor rather than a shipped product: the Nuaire XBOXER
        /// Universal sizes that would supply it in reality are SMALLER than the MRXBOX on both sides and
        /// would change which product an existing project's dwellings select, so cataloguing them is its own
        /// change. What is real here is the 150 -> 190 boundary, and it is the boundary this ladder turns on.
        /// </para>
        /// <para>
        /// <b>Nothing is reduced to fit.</b> Every selection reports the duty it was asked for, unchanged;
        /// the refusal reports no descriptor at all rather than the nearest undersized unit.
        /// </para>
        /// </summary>
        [Fact]
        public void TheRealLadder_RefusesUnder150_KeepsMRXBOXAt150_AndReachesXBC15Above()
        {
            List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = Query.VentilationUnitCapacityDescriptors(Directory_Resources());

            Assert.NotNull(ventilationUnitCapacityDescriptors);

            VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor_MRXBOX = Descriptor(ventilationUnitCapacityDescriptors, model_Nuaire);
            VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor_XBC15 = Descriptor(ventilationUnitCapacityDescriptors, model_XBC15);

            //A real undersized case, offered alongside the two shipped products.
            VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor_130 = new(new VentilationUnitReference(manufacturer_Nuaire, "FIXTURE-UNDERSIZED-130", null), 130, 130, 32);

            List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors_Ladder = [ventilationUnitCapacityDescriptor_130, .. ventilationUnitCapacityDescriptors];

            //---- above 130: the 130 l/s product is not a candidate, and no duty is trimmed to make it one.
            Assert.False(ventilationUnitCapacityDescriptor_130.IsSufficientFor(131, 131));
            Assert.DoesNotContain(ventilationUnitCapacityDescriptor_130, Analytical.Query.CapableVentilationUnits(ventilationUnitCapacityDescriptors_Ladder, 131, 131));

            VentilationUnitSelection ventilationUnitSelection_131 = Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors_Ladder, 131, 131);

            Assert.True(ventilationUnitSelection_131.IsSelected);
            Assert.Equal(model_Nuaire, ventilationUnitSelection_131.VentilationUnitReference.Model);
            Assert.Equal(131, ventilationUnitSelection_131.SupplyDuty_Lps, 6);
            Assert.Equal(19, ventilationUnitSelection_131.SupplyHeadroom_Lps, 6);

            //---- 150: the existing accepted behaviour, unchanged by the XBC15 being on offer.
            VentilationUnitSelection ventilationUnitSelection_150 = Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors_Ladder, 150, 150);

            Assert.True(ventilationUnitSelection_150.IsSelected);
            Assert.Equal(model_Nuaire, ventilationUnitSelection_150.VentilationUnitReference.Model);
            Assert.Equal(0, ventilationUnitSelection_150.SupplyHeadroom_Lps, 6);
            Assert.Equal(0, ventilationUnitSelection_150.ExtractHeadroom_Lps, 6);

            //The XBC15 is capable at 150 and is NOT chosen, because it is larger on both sides. Smallest
            //compliant, never largest available.
            Assert.True(ventilationUnitCapacityDescriptor_XBC15.IsSufficientFor(150, 150));
            Assert.True(ventilationUnitCapacityDescriptor_MRXBOX.Size_Lps < ventilationUnitCapacityDescriptor_XBC15.Size_Lps);

            //---- 160: the real manufacturer boundary. THE point of adding a second product.
            Assert.False(ventilationUnitCapacityDescriptor_MRXBOX.IsSufficientFor(160, 160));

            VentilationUnitSelection ventilationUnitSelection_160 = Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors_Ladder, 160, 160);

            Assert.True(ventilationUnitSelection_160.IsSelected);
            Assert.Equal(model_XBC15, ventilationUnitSelection_160.VentilationUnitReference.Model);
            Assert.Equal(160, ventilationUnitSelection_160.SupplyDuty_Lps, 6);
            Assert.Equal(30, ventilationUnitSelection_160.SupplyHeadroom_Lps, 6);

            //---- 190: exactly on the XBC15's rating is sufficient, not a rounding failure.
            VentilationUnitSelection ventilationUnitSelection_190 = Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors_Ladder, 190, 190);

            Assert.True(ventilationUnitSelection_190.IsSelected);
            Assert.Equal(model_XBC15, ventilationUnitSelection_190.VentilationUnitReference.Model);
            Assert.Equal(0, ventilationUnitSelection_190.SupplyHeadroom_Lps, 6);

            //---- above 190: refused, and the reason says how far short the catalogue fell.
            VentilationUnitSelection ventilationUnitSelection_191 = Analytical.Query.SelectSmallestCapableVentilationUnit(ventilationUnitCapacityDescriptors_Ladder, 191, 191);

            Assert.False(ventilationUnitSelection_191.IsSelected);
            Assert.Null(ventilationUnitSelection_191.Descriptor);
            Assert.Contains("190", ventilationUnitSelection_191.Reason);
            Assert.Contains("an undersized unit is not an answer", ventilationUnitSelection_191.Reason);
        }

        /// <summary>
        /// Each shipped product's identity survives a serialization round trip and resolves back to its own
        /// template - and the two never match each other. Identity is what the model stores and what
        /// performance data is looked up by later, so a collision would make the model's own record
        /// ambiguous.
        /// </summary>
        [Fact]
        public void TheTwoShippedIdentities_RoundTripAndNeverCollide()
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(Directory_Resources());

            VentilationUnitReference ventilationUnitReference_MRXBOX = Nuaire().VentilationUnitReference;
            VentilationUnitReference ventilationUnitReference_XBC15 = XBC15().VentilationUnitReference;

            Assert.False(ventilationUnitReference_MRXBOX.Matches(ventilationUnitReference_XBC15));
            Assert.False(ventilationUnitReference_XBC15.Matches(ventilationUnitReference_MRXBOX));

            foreach (VentilationUnitReference ventilationUnitReference in new[] { ventilationUnitReference_MRXBOX, ventilationUnitReference_XBC15 })
            {
                VentilationUnitReference ventilationUnitReference_RoundTripped = new(ventilationUnitReference.ToJsonObject());

                Assert.Equal(ventilationUnitReference.Manufacturer, ventilationUnitReference_RoundTripped.Manufacturer);
                Assert.Equal(ventilationUnitReference.Model, ventilationUnitReference_RoundTripped.Model);
                Assert.Equal(ventilationUnitReference.Reference, ventilationUnitReference_RoundTripped.Reference);
                Assert.True(ventilationUnitReference.Matches(ventilationUnitReference_RoundTripped));

                //And it still finds its own template, by identity, in the catalogue it came from.
                VentilationUnitTemplate ventilationUnitTemplate = Analytical.Query.MatchingVentilationUnitTemplate(ventilationUnitTemplates, ventilationUnitReference_RoundTripped);

                Assert.NotNull(ventilationUnitTemplate);
                Assert.Equal(ventilationUnitReference.Model, ventilationUnitTemplate.VentilationUnitReference.Model);
            }
        }

        /// <summary>
        /// <b>Fail-closed still means the WHOLE catalogue.</b> With two products in the file, one unusable
        /// entry must still refuse everything rather than quietly returning the good one - a product
        /// silently vanishing from a library is the failure mode that produces a smaller answer nobody
        /// notices, and it gets easier to miss the more products there are.
        /// </summary>
        [Fact]
        public void AnUnusableSecondEntry_StillRefusesTheWholeCatalogue()
        {
            string directory = TemporaryCatalogue_TwoEntriesSecondUnusable();

            try
            {
                //The FIRST entry is perfectly good and is still not returned.
                Assert.Null(Query.VentilationUnitTemplates(directory));
                Assert.Null(Query.VentilationUnitCapacityDescriptors(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        // =================================================================================================
        // Fixtures
        // =================================================================================================

        /// <summary>
        /// The shipped Nuaire MRXBOX entry, resolved by PRODUCT IDENTITY rather than by being the only thing
        /// in the file. The catalogue holds more than one product now, and a fixture that took the first or
        /// the only entry would either break or, worse, silently start asserting a different product's
        /// figures the day the file grows again. Asserted to appear EXACTLY once, which is the invariant the
        /// reader's own duplicate-identity gate exists to protect.
        /// </summary>
        /// <summary>One offered product by model, and the assertion that exactly one answers to it.</summary>
        private static VentilationUnitCapacityDescriptor Descriptor(List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors, string model)
        {
            Assert.NotNull(ventilationUnitCapacityDescriptors);

            VentilationUnitCapacityDescriptor result = Assert.Single(ventilationUnitCapacityDescriptors.FindAll(
                x => x?.VentilationUnitReference is not null && string.Equals(x.VentilationUnitReference.Model, model, StringComparison.Ordinal)));

            Assert.NotNull(result);

            return result;
        }

        /// <summary>
        /// Two entries where the first is valid and the second has no <c>Rank</c> - one of the reader's own
        /// refusal gates, chosen because it is the one a hand-added product is most likely to forget.
        /// </summary>
        private static string TemporaryCatalogue_TwoEntriesSecondUnusable()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_VentilationUnitCatalogue_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            string entries = Entry("FIXTURE-GOOD-150", "150", "150") + "," + Entry("FIXTURE-NO-RANK-190", "190", "190", rank: null);

            File.WriteAllText(
                Path.Combine(directory, Query.VentilationUnitCatalogueFileName),
                "{ \"Schema\": \"VentilationUnitCatalogue:v1\", \"Templates\": [" + entries + "] }");

            return directory;
        }

        private static VentilationUnitTemplate Nuaire()
        {
            return Shipped(model_Nuaire);
        }

        /// <summary>The shipped Nuaire XBOXER XBC15 entry, resolved the same way.</summary>
        private static VentilationUnitTemplate XBC15()
        {
            return Shipped(model_XBC15);
        }

        /// <summary>
        /// One shipped product by model, and the assertion that the catalogue names it once.
        /// </summary>
        private static VentilationUnitTemplate Shipped(string model)
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = Query.VentilationUnitTemplates(Directory_Resources());

            Assert.NotNull(ventilationUnitTemplates);

            List<VentilationUnitTemplate> ventilationUnitTemplates_Matching = ventilationUnitTemplates.FindAll(
                x => x?.VentilationUnitReference is not null && string.Equals(x.VentilationUnitReference.Model, model, StringComparison.Ordinal));

            VentilationUnitTemplate result = Assert.Single(ventilationUnitTemplates_Matching);

            Assert.NotNull(result);

            return result;
        }

        /// <summary>
        /// Supply air temperature [degC], transcribed the way the brochure prints it: twelve rows of eight,
        /// blocked by external air temperature (29, 32, 34) and within each block by internal temperature
        /// (23, 24, 25, 26). Columns are 50, 60, 70, 80, 90, 100, 110, 120 l/s.
        /// </summary>
        private static double[][] SupplyAirTemperatures_C()
        {
            return
            [
                //29 degC external
                [14.3, 14.8, 15.2, 15.7, 16.2, 16.7, 17.2, 17.8],
                [15.0, 15.4, 15.9, 16.3, 16.8, 17.3, 17.8, 18.4],
                [15.5, 15.9, 16.4, 16.9, 17.4, 18.0, 18.5, 19.1],
                [15.9, 16.4, 17.0, 17.5, 18.1, 18.6, 19.2, 19.8],

                //32 degC external
                [15.0, 15.3, 15.8, 16.3, 17.0, 17.7, 18.4, 19.1],
                [16.0, 16.4, 16.8, 17.2, 17.8, 18.3, 18.9, 19.6],
                [16.6, 17.0, 17.4, 17.9, 18.3, 18.8, 19.3, 19.9],
                [17.3, 17.7, 18.1, 18.5, 18.9, 19.3, 19.7, 20.2],

                //34 degC external
                [15.4, 15.9, 16.4, 16.9, 17.4, 17.9, 18.4, 18.9],
                [16.0, 16.5, 17.0, 17.5, 18.0, 18.5, 19.0, 19.5],
                [16.9, 17.4, 17.9, 18.4, 18.9, 19.4, 19.9, 20.4],
                [17.8, 18.3, 18.8, 19.3, 19.7, 20.2, 20.7, 21.2],
            ];
        }

        /// <summary>
        /// Combined cooling [kW] - coolth recovery and sensible cooling together, as the brochure defines
        /// it - in the same twelve-by-eight layout as <see cref="SupplyAirTemperatures_C"/>.
        /// </summary>
        private static double[][] CombinedCoolingCapacities_kW()
        {
            return
            [
                //29 degC external
                [0.88, 1.03, 1.17, 1.29, 1.40, 1.49, 1.57, 1.62],
                [0.82, 0.98, 1.12, 1.24, 1.34, 1.43, 1.49, 1.53],
                [0.80, 0.95, 1.07, 1.18, 1.27, 1.34, 1.39, 1.43],
                [0.78, 0.91, 1.02, 1.11, 1.19, 1.25, 1.30, 1.33],

                //32 degC external
                [1.03, 1.21, 1.37, 1.51, 1.63, 1.73, 1.82, 1.88],
                [0.98, 1.14, 1.29, 1.42, 1.54, 1.65, 1.74, 1.82],
                [0.93, 1.09, 1.24, 1.37, 1.49, 1.60, 1.69, 1.77],
                [0.88, 1.04, 1.19, 1.32, 1.44, 1.55, 1.64, 1.72],

                //34 degC external
                [1.13, 1.31, 1.49, 1.65, 1.80, 1.94, 2.08, 2.20],
                [1.11, 1.27, 1.43, 1.59, 1.73, 1.87, 2.00, 2.12],
                [1.04, 1.21, 1.37, 1.52, 1.65, 1.77, 1.89, 1.99],
                [0.97, 1.15, 1.31, 1.45, 1.57, 1.68, 1.77, 1.85],
            ];
        }

        /// <summary>
        /// Writes a small catalogue into a temporary directory, either sound (<c>Resolved</c>) or with one
        /// named defect in it. Hand-written JSON rather than a serialized object, because what is under
        /// test is how the reader treats a file somebody edited.
        /// </summary>
        private static string TemporaryCatalogue(string defect)
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_VentilationUnitCatalogue_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            string entry_60 = Entry("FIXTURE-60", "60", "60");
            string entry_90 = Entry("FIXTURE-90", "90", "90");

            string entries;
            string schema = "VentilationUnitCatalogue:v1";

            switch (defect)
            {
                case "Resolved":
                    entries = entry_60 + "," + entry_90;
                    break;

                case "Source":
                    entries = entry_60 + "," + Entry("FIXTURE-90", "90", "90", source: null);
                    break;

                case "RaggedGrid":
                    //Seven values where the axes say eight - a grid that would answer every query with
                    //numbers attributed to the wrong conditions.
                    entries = entry_60 + "," + Entry("FIXTURE-90", "90", "90", values: "[1,2,3,4,5,6,7]");
                    break;

                case "DuplicateIdentity":
                    entries = entry_60 + "," + Entry("FIXTURE-60", "90", "90");
                    break;

                case "UnusableCapacity":
                    entries = entry_60 + "," + Entry("FIXTURE-90", "\"ninety\"", "90");
                    break;

                case "MissingRank":
                    //A missing rank is a unique 0, 0 sorts first, and the unranked entry becomes the preferred
                    //answer between two products of the same size. Declared or refused.
                    entries = entry_60 + "," + Entry("FIXTURE-90", "90", "90", rank: null);
                    break;

                case "BrokenControlPolicy":
                    //A mistyped policy name. Silently reading it as the permissive default is the one
                    //direction a typo must never take.
                    entries = entry_60 + "," + Entry("FIXTURE-90", "90", "90", policy: "Refuze");
                    break;

                case "NullPerformanceTable":
                    //PRESENT but null - malformed data, not the documented "no performance data" absence.
                    entries = entry_60 + "," + Entry("FIXTURE-90", "90", "90", performanceTableNull: true);
                    break;

                case "NullFlowFractionByControlTemperature":
                    //Same distinction, on the control curve.
                    entries = entry_60 + "," + Entry("FIXTURE-90", "90", "90", flowFractionNull: true);
                    break;

                case "MissingSchema":
                    entries = entry_60 + "," + entry_90;
                    schema = null;
                    break;

                case "WrongSchema":
                    entries = entry_60 + "," + entry_90;
                    schema = "NotACatalogue";
                    break;

                case "FutureSchema":
                    //Plausible-looking, not one of this reader's accepted versions - must not be quietly
                    //parsed as v1 or v2. (v2 itself became an accepted tag under PR5A - see
                    //VentilationUnitCatalogueV2Tests - so this uses a version still one further out.)
                    entries = entry_60 + "," + entry_90;
                    schema = "VentilationUnitCatalogue:v3";
                    break;

                default:
                    entries = entry_60 + ",\"not an object\"";
                    break;
            }

            string schemaJson = schema == null ? string.Empty : "\"Schema\": \"" + schema + "\", ";

            File.WriteAllText(
                Path.Combine(directory, Query.VentilationUnitCatalogueFileName),
                "{ " + schemaJson + "\"Templates\": [" + entries + "] }");

            return directory;
        }

        /// <summary>A one-entry catalogue whose PerformanceTable and FlowFractionByControlTemperature keys are both simply absent - the legal "no performance data" state, contrasted with the present-but-null cases above.</summary>
        private static string TemporaryCatalogueWithAbsentOptionalData()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_VentilationUnitCatalogue_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            string entry = Entry("FIXTURE-60", "60", "60", performanceTableAbsent: true, flowFractionAbsent: true);

            File.WriteAllText(
                Path.Combine(directory, Query.VentilationUnitCatalogueFileName),
                "{ \"Schema\": \"VentilationUnitCatalogue:v1\", \"Templates\": [" + entry + "] }");

            return directory;
        }

        private static string Entry(
            string model,
            string maximumSupply,
            string maximumExtract,
            string source = "Test Fixture",
            string values = "[1,2,3,4,5,6,7,8]",
            string policy = "ClampToDomain",
            string rank = "0",
            bool performanceTableNull = false,
            bool performanceTableAbsent = false,
            bool flowFractionNull = false,
            bool flowFractionAbsent = false)
        {
            List<string> fields =
            [
                "\"_type\": \"SAM.Analytical.VentilationUnitTemplate,SAM.Analytical\"",
                "\"VentilationUnitReference\": { \"_type\": \"SAM.Analytical.VentilationUnitReference,SAM.Analytical\", \"Manufacturer\": \"Test Fixture\", \"Model\": \"" + model + "\" }",
                "\"MaximumSupplyFlowRate_Lps\": " + maximumSupply,
                "\"MaximumExtractFlowRate_Lps\": " + maximumExtract,
            ];

            if (source != null)
            {
                fields.Add("\"Source\": \"" + source + "\"");
            }

            if (rank != null)
            {
                fields.Add("\"Rank\": " + rank);
            }

            if (performanceTableNull)
            {
                //PRESENT but null.
                fields.Add("\"PerformanceTable\": null");
            }
            else if (!performanceTableAbsent)
            {
                fields.Add(
                    "\"PerformanceTable\": { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceTable,SAM.Analytical\"," +
                    "\"Axes\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceAxis,SAM.Analytical\", \"Name\": \"AirFlowRate\", \"Unit\": \"l/s\", \"Values\": [50,60,70,80,90,100,110,120] } ]," +
                    "\"Outputs\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceOutput,SAM.Analytical\", \"Name\": \"SupplyAirTemperature\", \"Unit\": \"degC\", \"Values\": " + values + " } ] }");
            }

            if (flowFractionNull)
            {
                //PRESENT but null.
                fields.Add("\"FlowFractionByControlTemperature\": null");
            }
            else if (!flowFractionAbsent)
            {
                fields.Add(
                    "\"FlowFractionByControlTemperature\": { \"_type\": \"SAM.Analytical.FlowFractionControlCurve,SAM.Analytical\"," +
                    "\"PerformanceTable\": { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceTable,SAM.Analytical\"," +
                    "\"Axes\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceAxis,SAM.Analytical\", \"Name\": \"ControlTemperature\", \"Unit\": \"degC\", \"Values\": [22,26] } ]," +
                    "\"Outputs\": [ { \"_type\": \"SAM.Analytical.VentilationUnitPerformanceOutput,SAM.Analytical\", \"Name\": \"FlowFraction\", \"Unit\": \"-\", \"Values\": [0.3,1.0] } ] }," +
                    "\"PerformanceDomainPolicy\": \"" + policy + "\" }");
            }

            return "{ " + string.Join(",", fields) + " }";
        }

        /// <summary>
        /// This repository's own resources directory, so the tests check what is <b>shipped</b> rather than
        /// what happened to be copied into a test output. The same walk
        /// <see cref="SystemEnergyCentreCapabilityTests"/> uses.
        /// </summary>
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
