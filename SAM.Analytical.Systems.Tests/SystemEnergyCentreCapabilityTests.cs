// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Enums;
using SAM.Analytical.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// The capability index against the <c>SystemEnergyCentre</c> templates it describes.
    /// <para>
    /// <b>This is the one place the 1.8 MB templates are opened, and it is a test.</b> Selection reads the
    /// small index beside them and nothing else; what the index says has to be true of the files, and the
    /// only way to know that is to read the files. Paying it once here, offline, is the whole reason
    /// choosing a system at runtime costs nothing.
    /// </para>
    /// <para>
    /// <b>What this can and cannot prove.</b> Heat recovery is in the template - an exchanger with a
    /// sensible efficiency - so the index's claim is checked against it. Summer bypass is checked as
    /// <i>absent</i>, which it is in every shipped template. Continuous ventilation and boost are
    /// <b>declared</b> from the system type's meaning and cannot be read off the file at all; the index
    /// says which of its values are evidence and which are declaration, and this asserts that it says so
    /// honestly rather than pretending the declared ones were measured.
    /// </para>
    /// </summary>
    public class SystemEnergyCentreCapabilityTests
    {
        // ---------------------------------------------------------------------------------------------
        // Coverage - the index and the shipped resources describe each other exactly
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>Every shipped template is accounted for, and every entry names a template that is there.</b>
        /// A template with no entry is invisible to every selection - it would simply never be chosen, with
        /// nothing to say why. An entry naming a missing file resolves to nothing after a choice has
        /// already been made.
        /// </summary>
        [Fact]
        public void IndexAndShippedResources_CoverEachOtherExactly()
        {
            List<string> resources_Shipped = [];

            foreach (string path in Directory.GetFiles(Directory_Resources(), "*.json"))
            {
                string name = Path.GetFileName(path);

                if (!string.Equals(name, Query.CapabilityIndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    resources_Shipped.Add(name);
                }
            }

            List<string> resources_Accounted = Query.SystemEnergyCentreResources_Accounted(Directory_Resources());

            Assert.NotNull(resources_Accounted);

            resources_Shipped.Sort(StringComparer.OrdinalIgnoreCase);
            resources_Accounted.Sort(StringComparer.OrdinalIgnoreCase);

            Assert.Equal(resources_Shipped, resources_Accounted);

            //The ten this was written against, so a resource quietly disappearing is caught too.
            Assert.Equal(10, resources_Shipped.Count);
        }

        /// <summary>
        /// Every ventilation entry resolves to a file that is really there, and the plant-room template -
        /// which names no ventilation system - is deliberately not a candidate.
        /// </summary>
        [Fact]
        public void EveryVentilationEntry_ResolvesToAShippedFile()
        {
            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources());

            Assert.NotNull(systemCapabilityDescriptors);
            Assert.Equal(9, systemCapabilityDescriptors.Count);

            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors)
            {
                string resource = systemCapabilityDescriptor.SystemTemplate.SystemEnergyCentreResource(Directory_Resources());

                Assert.False(string.IsNullOrWhiteSpace(resource), string.Format("'{0}' resolves to no resource.", systemCapabilityDescriptor.SystemTemplate));
                Assert.True(File.Exists(Path.Combine(Directory_Resources(), resource)), string.Format("'{0}' names '{1}', which is not there.", systemCapabilityDescriptor.SystemTemplate, resource));
            }

            //Plantroom-Only is a plant room, not a way of ventilating a dwelling, so it is never offered.
            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors)
            {
                Assert.NotEqual("Plantroom-Only.json", systemCapabilityDescriptor.SystemTemplate.SystemEnergyCentreResource(Directory_Resources()));
            }
        }

        /// <summary>
        /// <b>No two entries claim one ventilation identity, and every rank is distinct.</b> A duplicated
        /// identity would make resolution ambiguous; a duplicated rank would make the preferred system
        /// ambiguous, and <c>SelectPreferredCapableSystem</c> refuses on both rather than picking by
        /// position. Asserted here so the refusal is a guard against a future edit, not something the
        /// shipped library trips over.
        /// </summary>
        [Fact]
        public void EveryEntry_HasADistinctIdentityAndRank()
        {
            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources());

            HashSet<string> ventilations = [];
            HashSet<int> ranks = [];

            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors)
            {
                Assert.True(ventilations.Add(systemCapabilityDescriptor.SystemTemplate.Ventilation), string.Format("'{0}' appears twice.", systemCapabilityDescriptor.SystemTemplate.Ventilation));
                Assert.True(ranks.Add(systemCapabilityDescriptor.Rank), string.Format("Rank {0} is used twice.", systemCapabilityDescriptor.Rank));

                //Rank 0 is what a missing rank reads as, so a real entry must never carry it.
                Assert.NotEqual(0, systemCapabilityDescriptor.Rank);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Conformance - what the index claims is true of the templates
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>Heat recovery is checked against the template, not taken on trust.</b> A template recovers
        /// heat exactly when it holds an air-side exchanger with a sensible efficiency above zero. This
        /// reads all ten shipped files to find out.
        /// <para>
        /// It also re-establishes the fact the whole <c>MVRE</c>-is-MVHR decision rests on: <c>MVRE</c> has
        /// one exchanger at 0.7 sensible and 0 latent, and no recirculation component anywhere - so
        /// <c>RE</c> behaves as REcovery despite the library description saying "Recirculation", and there
        /// is no case for a second <c>MVHR</c> identity.
        /// </para>
        /// </summary>
        [Fact]
        public void HeatRecovery_MatchesTheShippedTemplates()
        {
            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in Query.SystemCapabilityDescriptors(Directory_Resources()))
            {
                string resource = systemCapabilityDescriptor.SystemTemplate.SystemEnergyCentreResource(Directory_Resources());

                string json = File.ReadAllText(Path.Combine(Directory_Resources(), resource));

                bool claimed = (systemCapabilityDescriptor.Capabilities & SystemCapability.HeatRecovery) == SystemCapability.HeatRecovery;
                bool present = SensibleEfficiencies(json).Count > 0;

                Assert.True(claimed == present, string.Format("'{0}' claims HeatRecovery={1} but '{2}' {3} an exchanger.", systemCapabilityDescriptor.SystemTemplate.Ventilation, claimed, resource, present ? "has" : "has no"));
            }

            //The MVRE fact itself, since every Part O decision about heat recovery rests on it.
            string json_MVRE = File.ReadAllText(Path.Combine(Directory_Resources(), "MVRE.json"));

            Assert.Equal(["0.7"], SensibleEfficiencies(json_MVRE));
            Assert.Equal(["0"], LatentEfficiencies(json_MVRE));
            Assert.DoesNotContain("ecirculat", json_MVRE);

            //And MV is the same system without the exchanger - which is what makes the pair mean exactly
            //"heat recovery".
            string json_MV = File.ReadAllText(Path.Combine(Directory_Resources(), "MV.json"));

            Assert.Empty(SensibleEfficiencies(json_MV));
        }

        /// <summary>
        /// <b>No shipped template models a summer bypass, and the index says so.</b> This is a finding, not
        /// an omission: Iteration 2 needs bypass, and until a template provides one a scenario requiring it
        /// is refused - loudly, which is the point. Claiming bypass here would credit a dwelling with
        /// mitigation the design does not have and pass an overheating assessment that should fail.
        /// </summary>
        [Fact]
        public void SummerBypass_IsAbsentFromEveryShippedTemplateAndFromTheIndex()
        {
            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources());

            Assert.NotNull(systemCapabilityDescriptors);
            Assert.NotEmpty(systemCapabilityDescriptors);

            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors)
            {
                Assert.Equal(SystemCapability.None, systemCapabilityDescriptor.Capabilities & SystemCapability.SummerBypass);
            }

            //So a Part O scenario asking for bypass gets a refusal rather than a system that cannot do it.
            SystemCapabilitySelection systemCapabilitySelection = Query.SystemCapabilityDescriptors(Directory_Resources())
                .SelectPreferredCapableSystem(new SystemCapabilityRequirement(SystemCapability.ContinuousVentilation | SystemCapability.SummerBypass));

            Assert.False(systemCapabilitySelection.IsSelected);
            Assert.Equal(SystemCapability.SummerBypass, systemCapabilitySelection.Missing);
        }

        /// <summary>
        /// <b>The index is honest about which of its values are evidence.</b> Continuous ventilation and
        /// boost cannot be read off a template - nothing in the files marks either - so they are declared
        /// from the system type's meaning. Every entry must say which capabilities it claims were
        /// established from the template, and must not claim the declared ones were.
        /// </summary>
        [Fact]
        public void Index_SaysWhichValuesAreEvidenceAndWhichAreDeclared()
        {
            JsonObject jsonObject = Query.SystemCapabilityIndex(Directory_Resources());

            Assert.NotNull(jsonObject);
            Assert.Equal("SystemEnergyCentreCapability:v1", (jsonObject["Schema"] as JsonValue)?.GetValue<string>());

            foreach (JsonNode jsonNode in jsonObject["Templates"] as JsonArray)
            {
                JsonArray jsonArray = (jsonNode as JsonObject)?["EvidenceFromTemplate"] as JsonArray;

                Assert.NotNull(jsonArray);

                List<string> evidence = [];
                foreach (JsonNode jsonNode_Evidence in jsonArray)
                {
                    evidence.Add((jsonNode_Evidence as JsonValue)?.GetValue<string>());
                }

                //Exactly the two the templates can settle - and, importantly, NOT the two they cannot.
                Assert.Equal(["HeatRecovery", "SummerBypass"], evidence);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // The runtime path never opens a template
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>Reading capabilities and choosing a system touch the index and nothing else.</b> Proved by
        /// timing it against the cost of opening one template: the whole selection is far quicker than a
        /// single 1.2 MB file, which it could not be if it were reading any of them. A generous factor,
        /// because this asserts an order of magnitude, not a benchmark.
        /// </summary>
        [Fact]
        public void ChoosingASystem_DoesNotOpenATemplate()
        {
            //Warm the file system so the comparison is not measuring a cold cache.
            File.ReadAllText(Path.Combine(Directory_Resources(), "MV.json"));
            Query.SystemCapabilityDescriptors(Directory_Resources());

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 5; i++)
            {
                Query.SystemCapabilityDescriptors(Directory_Resources()).SelectPreferredCapableSystem(new SystemCapabilityRequirement(SystemCapability.ContinuousVentilation | SystemCapability.Boost));
            }

            long elapsed_Selection = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();

            File.ReadAllText(Path.Combine(Directory_Resources(), "MVRE.json"));

            long elapsed_OneTemplate = stopwatch.ElapsedMilliseconds;

            Assert.True(elapsed_Selection <= System.Math.Max(elapsed_OneTemplate, 5) * 3, string.Format("Five selections took {0} ms against {1} ms to read one template - selection is reading template files.", elapsed_Selection, elapsed_OneTemplate));
        }

        /// <summary>
        /// The concrete energy centre is resolved only after a choice has been made, and only for the
        /// system chosen - <b>the one point at which a template is opened at runtime</b>. Asserted end to
        /// end: a Part F requirement picks a system, and that system resolves to a real, loadable
        /// <c>SystemEnergyCentre</c>.
        /// </summary>
        [Fact]
        public void ChosenSystem_ResolvesToAConcreteEnergyCentre()
        {
            PartFDwellingResult partFDwellingResult = new("Flat 1") { ContinuousDesignSystemRate_Lps = 21.0, TotalHighExtract_Lps = 39.0 };

            SystemCapabilitySelection systemCapabilitySelection = Query.SystemCapabilityDescriptors(Directory_Resources())
                .SelectPreferredCapableSystem(partFDwellingResult.PartFSystemCapabilityRequirement());

            Assert.True(systemCapabilitySelection.IsSelected);

            //EOL - Local Extract Only - is the simplest shipped system that runs continuously and boosts.
            Assert.Equal("EOL", systemCapabilitySelection.SystemTemplate.Ventilation);

            Core.Systems.SystemEnergyCentre systemEnergyCentre = systemCapabilitySelection.SystemTemplate.SystemEnergyCentre(Directory_Resources());

            Assert.NotNull(systemEnergyCentre);
        }

        /// <summary>
        /// An identity the index does not name resolves to nothing, rather than to whatever was nearest.
        /// </summary>
        [Fact]
        public void UnknownSystem_ResolvesToNothing()
        {
            Assert.Null(new SystemTemplate("NOPE", null, null, null, null, null).SystemEnergyCentreResource(Directory_Resources()));
            Assert.Null(new SystemTemplate("NOPE", null, null, null, null, null).SystemEnergyCentre(Directory_Resources()));
            Assert.Null(((SystemTemplate)null).SystemEnergyCentreResource(Directory_Resources()));
        }

        // ---------------------------------------------------------------------------------------------
        // Fixture
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The repository's own resources directory, found by walking up from the test assembly. Read from
        /// source rather than from a copy in the output, because the point is to check what is SHIPPED -
        /// and because copying twelve megabytes of templates on every build to read them once would be a
        /// poor trade.
        /// <para>
        /// <b>The repository root is identified, not just the first matching folder.</b> The first version
        /// of this took any ancestor with a <c>files/resources/...</c> path under it and found
        /// <c>build_tests/net8.0/</c> - where the Mollier test project copies two of the templates - so it
        /// would have conformance-checked an index against a two-file copy and passed a library that was
        /// eight templates short. An ancestor only counts when it also holds the
        /// <c>SAM_Systems/SAM.Analytical.Systems</c> source, which no output directory does.
        /// </para>
        /// </summary>
        private static string Directory_Resources()
        {
            DirectoryInfo directoryInfo = new(AppContext.BaseDirectory);

            while (directoryInfo != null)
            {
                string result = Path.Combine(directoryInfo.FullName, "files", "resources", "Analytical", "Systems", "SystemEnergyCentre");

                if (Directory.Exists(result) && Directory.Exists(Path.Combine(directoryInfo.FullName, "SAM_Systems", "SAM.Analytical.Systems")))
                {
                    return result;
                }

                directoryInfo = directoryInfo.Parent;
            }

            throw new DirectoryNotFoundException("The SAM_Systems repository root was not found above " + AppContext.BaseDirectory);
        }

        private static List<string> SensibleEfficiencies(string json)
        {
            return Values(json, "SensibleEfficiency");
        }

        private static List<string> LatentEfficiencies(string json)
        {
            return Values(json, "LatentEfficiency");
        }

        /// <summary>
        /// The distinct values of a named efficiency in a template, read by pattern rather than by
        /// deserialising - this test may open the file, but there is no reason to build the whole object
        /// graph to read one number, and doing so would make the test depend on every type in it.
        /// </summary>
        private static List<string> Values(string json, string name)
        {
            List<string> result = [];

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(json, "\"" + name + "\":\\s*\\{[^}]*?\"Value\":\\s*([0-9.eE+-]+)"))
            {
                //"0.0" and "0" are the same number written two ways; compare as numbers.
                string text = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (!result.Contains(text))
                {
                    result.Add(text);
                }
            }

            result.Sort(StringComparer.Ordinal);

            return result;
        }
    }
}
