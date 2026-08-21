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
            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined);

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
            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined);

            HashSet<string> ventilations = [];

            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors)
            {
                Assert.True(ventilations.Add(systemCapabilityDescriptor.SystemTemplate.Ventilation), string.Format("'{0}' appears twice.", systemCapabilityDescriptor.SystemTemplate.Ventilation));

                //Rank 0 is what a missing rank reads as, so a real entry must never carry it.
                Assert.NotEqual(0, systemCapabilityDescriptor.Rank);
            }

            //Ranks must be distinct WITHIN an application, which is the only set they ever order. Requiring
            //them globally distinct would forbid a harmless CAV/NV collision and read as a real constraint
            //to whoever confirms the provisional domestic order.
            foreach (SystemApplication systemApplication in new[] { SystemApplication.Domestic, SystemApplication.Commercial })
            {
                HashSet<int> ranks = [];

                foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in Query.SystemCapabilityDescriptors(Directory_Resources(), systemApplication))
                {
                    Assert.True(ranks.Add(systemCapabilityDescriptor.Rank), string.Format("Rank {0} is used twice within {1}, so the preferred system would be refused as ambiguous.", systemCapabilityDescriptor.Rank, systemApplication));
                }
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
            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined))
            {
                string resource = systemCapabilityDescriptor.SystemTemplate.SystemEnergyCentreResource(Directory_Resources());

                string json = File.ReadAllText(Path.Combine(Directory_Resources(), resource));

                bool claimed = (systemCapabilityDescriptor.Capabilities & SystemCapability.HeatRecovery) == SystemCapability.HeatRecovery;
                bool present = SensibleEfficiencies(json).Exists(x => x > 0);

                Assert.True(claimed == present, string.Format("'{0}' claims HeatRecovery={1} but '{2}' {3} an exchanger.", systemCapabilityDescriptor.SystemTemplate.Ventilation, claimed, resource, present ? "has" : "has no"));
            }

            //The MVRE fact itself, since every Part O decision about heat recovery rests on it.
            string json_MVRE = File.ReadAllText(Path.Combine(Directory_Resources(), "MVRE.json"));

            Assert.Equal([0.7], SensibleEfficiencies(json_MVRE));
            Assert.Equal([0.0], LatentEfficiencies(json_MVRE));

            //"Exchanger 1" and nothing else - the index Description claims exactly one.
            Assert.Single(ExchangerNames(json_MVRE));
            Assert.DoesNotContain("ecirculat", json_MVRE);

            //And MV is the same system without the exchanger - which is what makes the pair mean exactly
            //"heat recovery".
            string json_MV = File.ReadAllText(Path.Combine(Directory_Resources(), "MV.json"));

            Assert.Empty(SensibleEfficiencies(json_MV));
            Assert.Empty(ExchangerNames(json_MV));
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
            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined);

            Assert.NotNull(systemCapabilityDescriptors);
            Assert.NotEmpty(systemCapabilityDescriptors);

            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors)
            {
                Assert.Equal(SystemCapability.None, systemCapabilityDescriptor.Capabilities & SystemCapability.SummerBypass);
            }

            //THE EVIDENCE. The index claims SummerBypass was established from the templates, so establish it:
            //no template may hold a bypass token at all, other than BypassFactor - which is a cooling-coil
            //parameter and would false-positive a naive search. An earlier revision of this test asserted
            //only what the index said about itself, which proved nothing.
            foreach (string path in Directory.GetFiles(Directory_Resources(), "*.json"))
            {
                if (string.Equals(Path.GetFileName(path), Query.CapabilityIndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string json = File.ReadAllText(path).Replace("BypassFactor", string.Empty);

                Assert.DoesNotContain("ypass", json, StringComparison.OrdinalIgnoreCase);
            }

            //So a Part O scenario asking for bypass gets a refusal rather than a system that cannot do it.
            SystemCapabilitySelection systemCapabilitySelection = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined)
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
        /// <b>Reading capabilities and choosing a system touch the index and nothing else - proved
        /// structurally, not by a stopwatch.</b> The index is copied ALONE into an empty directory, with no
        /// template beside it, and the whole selection still produces the same answer. Any code path that
        /// opened a template would fail outright.
        /// <para>
        /// An earlier revision timed the selection against the cost of reading one template. That was not
        /// vacuous - opening ten of them would have blown the bar - but it was machine-dependent, a single
        /// long pause on a loaded runner could flip it, and it only covered the one requirement it happened
        /// to exercise. This is deterministic.
        /// </para>
        /// </summary>
        [Fact]
        public void ChoosingASystem_DoesNotOpenATemplate()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM_CapabilityIndexOnly_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(directory);

                File.Copy(Path.Combine(Directory_Resources(), Query.CapabilityIndexFileName), Path.Combine(directory, Query.CapabilityIndexFileName));

                //Nothing else is there. Confirmed rather than assumed, so this cannot pass by accident.
                Assert.Single(Directory.GetFiles(directory));

                List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(directory, SystemApplication.Undefined);

                Assert.NotNull(systemCapabilityDescriptors);
                Assert.Equal(Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined).Count, systemCapabilityDescriptors.Count);

                //Every requirement Part F can produce, answered identically without a template in sight.
                foreach (SystemCapability systemCapability in new[]
                {
                    SystemCapability.ContinuousVentilation,
                    SystemCapability.ContinuousVentilation | SystemCapability.Boost,
                    SystemCapability.ContinuousVentilation | SystemCapability.MechanicalSupply,
                    SystemCapability.ContinuousVentilation | SystemCapability.MechanicalSupply | SystemCapability.Boost
                })
                {
                    SystemCapabilityRequirement systemCapabilityRequirement = new(systemCapability);

                    Assert.Equal(
                        Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined).SelectPreferredCapableSystem(systemCapabilityRequirement).SystemTemplate?.Ventilation,
                        systemCapabilityDescriptors.SelectPreferredCapableSystem(systemCapabilityRequirement).SystemTemplate?.Ventilation);
                }

                //And resolution DOES need the template - the one thing that must fail without it, so this
                //test cannot be passing merely because nothing reads anything.
                Assert.Null(new SystemTemplate("MVRE", null, null, null, null, null).SystemEnergyCentre(directory));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
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

            //Domestic: Approved Document F selection must never be offered a commercial air-handling type,
            //and it is eligibility that guarantees it - not the ranks happening to favour domestic.
            SystemCapabilitySelection systemCapabilitySelection = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Domestic)
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
        // Application is an eligibility constraint
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>A dwelling is offered the domestic templates and the ones marked <c>Any</c>, and nothing
        /// else.</b> A commercial air-handling type is not a worse answer for a flat - it is not an
        /// answer - so it is filtered out here, before anything reaches <c>SAM.Analytical</c>, which never
        /// learns the words "domestic" or "commercial".
        /// </summary>
        [Fact]
        public void DomesticRequest_IsOfferedOnlyDomesticAndAnyTemplates()
        {
            List<string> ventilations = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Domestic).ConvertAll(x => x.SystemTemplate.Ventilation);

            Assert.Equal(6, ventilations.Count);
            Assert.Equal(["NV", "EOL", "EOC", "MV", "MVRE", "UV"], Sorted(ventilations, ["NV", "EOL", "EOC", "MV", "MVRE", "UV"]));

            foreach (string text in new[] { "CAV", "VAV", "DISP" })
            {
                Assert.DoesNotContain(text, ventilations);
            }

            //Undefined means no constraint, which is right for a caller that has not said what it is
            //assessing and wrong for a dwelling. All nine come back.
            Assert.Equal(9, Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined).Count);
            Assert.Equal(9, Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined).Count);

            //And a commercial request gets the commercial ones plus Any.
            List<string> ventilations_Commercial = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Commercial).ConvertAll(x => x.SystemTemplate.Ventilation);

            Assert.Equal(4, ventilations_Commercial.Count);
            Assert.Equal(["CAV", "VAV", "DISP", "UV"], Sorted(ventilations_Commercial, ["CAV", "VAV", "DISP", "UV"]));
        }

        /// <summary>
        /// <b>Rank is not what keeps a commercial system out of a dwelling answer.</b> Every rank in the
        /// index is inverted - so the commercial templates are ranked ahead of every domestic one - and a
        /// domestic request still never sees them. Before <c>Application</c> became a constraint this test
        /// would have returned a variable-air-volume unit for a flat, and one edited number was all it
        /// would have taken.
        /// </summary>
        [Fact]
        public void CommercialTemplates_AreExcludedByEligibilityAndNotByRank()
        {
            string json = Json_Index();

            //The inversion below marks each replacement with '!' so it cannot be re-matched by a later
            //pass. That only works while no '!' occurs in the file to begin with.
            Assert.DoesNotContain("!", json);

            //Invert the order: 10 -> 90, 20 -> 80, ... so commercial sorts first.
            foreach (int rank in new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 })
            {
                json = json.Replace("\"Rank\": " + rank + ",", "\"Rank\": " + (100 - rank) + "!,");
            }

            json = json.Replace("!,", ",");

            string directory = Directory_Temp(json);

            try
            {
                //The premise: commercial really is ranked ahead of domestic now.
                List<SystemCapabilityDescriptor> systemCapabilityDescriptors_All = Query.SystemCapabilityDescriptors(directory, SystemApplication.Undefined);

                Assert.Equal("DISP", systemCapabilityDescriptors_All.CapableSystems(new SystemCapabilityRequirement(SystemCapability.ContinuousVentilation))[0].SystemTemplate.Ventilation);

                //The conclusion: a dwelling is still never offered one.
                List<SystemCapabilityDescriptor> systemCapabilityDescriptors_Domestic = Query.SystemCapabilityDescriptors(directory, SystemApplication.Domestic);

                foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors_Domestic)
                {
                    Assert.DoesNotContain(systemCapabilityDescriptor.SystemTemplate.Ventilation, new[] { "CAV", "VAV", "DISP" });
                }

                PartFDwellingResult partFDwellingResult = new("Flat 1") { ContinuousDesignSystemRate_Lps = 21.0, TotalSupply_Lps = 21.0, TotalHighExtract_Lps = 39.0 };

                SystemCapabilitySelection systemCapabilitySelection = systemCapabilityDescriptors_Domestic.SelectPreferredCapableSystem(partFDwellingResult.PartFSystemCapabilityRequirement());

                Assert.True(systemCapabilitySelection.IsSelected);
                Assert.Equal("MVRE", systemCapabilitySelection.SystemTemplate.Ventilation);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// An entry with a missing or unrecognised <c>Application</c> refuses the whole index. Defaulting
        /// it to eligible would offer an unclassified template to a dwelling; defaulting it to ineligible
        /// would make a system quietly vanish from the library. Neither is a thing to guess at.
        /// </summary>
        [Theory]
        [InlineData("\"Application\": \"Domestic\",", "")]
        [InlineData("\"Application\": \"Domestic\",", "\"Application\": \"Residential\",")]
        [InlineData("\"Application\": \"Domestic\",", "\"Application\": \"Undefined\",")]
        [InlineData("\"Application\": \"Domestic\",", "\"Application\": 3,")]
        public void MissingOrUnrecognisedApplication_RefusesTheIndex(string find, string replace)
        {
            string directory = Directory_Temp(Json_Index().Replace(find, replace));

            try
            {
                Assert.Null(Query.SystemCapabilityDescriptors(directory, SystemApplication.Domestic));
                Assert.Null(Query.SystemCapabilityDescriptors(directory, SystemApplication.Undefined));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// <b>The commercial <c>Boost</c> values are recorded as unverified, not reasoned from a name.</b>
        /// An earlier revision justified <c>CAV</c> and <c>DISP</c> boost = false from "constant volume"
        /// and "displacement regime", which is inferring a capability from a label rather than knowing it.
        /// The values stay false because false produces a refusal and true would produce a silently wrong
        /// assessment - but the index must say they are unconfirmed.
        /// </summary>
        [Fact]
        public void CommercialBoostDeclarations_AreRecordedAsUnverified()
        {
            JsonObject jsonObject = Query.SystemCapabilityIndex(Directory_Resources());

            foreach (JsonNode jsonNode in jsonObject["Templates"] as JsonArray)
            {
                JsonObject jsonObject_Template = jsonNode as JsonObject;

                bool commercial = (jsonObject_Template["Application"] as JsonValue)?.GetValue<string>() == "Commercial";

                JsonArray jsonArray = jsonObject_Template["UnverifiedDeclarations"] as JsonArray;

                //**Asserted, not commented.** An earlier revision skipped non-commercial entries with a
                //comment claiming they may not carry an unverified declaration - so adding one to MVRE, the
                //entry every Part O heat-recovery decision rests on, would have stayed green.
                if (!commercial)
                {
                    Assert.Null(jsonArray);
                    continue;
                }

                Assert.NotNull(jsonArray);

                List<string> unverified = [];
                foreach (JsonNode jsonNode_Unverified in jsonArray)
                {
                    unverified.Add((jsonNode_Unverified as JsonValue)?.GetValue<string>());
                }

                Assert.Contains("Boost", unverified);

                //**Every unverified capability must be FALSE.** A review found VAV declaring Boost true
                //while listing it as unverified - crediting a capability nobody confirmed, which the index's
                //own note said could not happen. False refuses; true silently mis-assesses.
                foreach (string text in unverified)
                {
                    Assert.False((jsonObject_Template[text] as JsonValue)?.GetValue<bool>(), string.Format("'{0}' declares {1} true while recording it as unverified. An unconfirmed capability must not be credited.", (jsonObject_Template["Resource"] as JsonValue)?.GetValue<string>(), text));
                }

                //And no reasoning from the type's NAME survives in the description - the whole point of
                //requirement 3. Checked as "says nothing about boost at all" rather than as one exact
                //phrase, which an earlier revision pinned and any rewording would have slipped past.
                string description = (jsonObject_Template["Description"] as JsonValue)?.GetValue<string>() ?? string.Empty;

                Assert.DoesNotContain("boost", description, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// <b>A request for <c>Any</c> means "I will take anything", not "only the entries marked Any".</b>
        /// The enum is used in two roles - a classification on an entry, and a request from a caller - and a
        /// review found the second reading inverted: asking for <c>Any</c> returned exactly one descriptor,
        /// <c>UV</c>, the one template that can never be selected. Every dwelling would then have been
        /// refused with a message about missing capabilities rather than about the filter.
        /// </summary>
        [Fact]
        public void AnyAsARequest_MeansNoConstraint()
        {
            Assert.Equal(
                Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Undefined).Count,
                Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Any).Count);

            Assert.Equal(9, Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Any).Count);
        }

        /// <summary>
        /// <b>An <c>Application</c> that is numeric, or a comma-separated list, refuses the index.</b>
        /// <c>Enum.TryParse</c> accepts both, and because the members number
        /// <c>Domestic = 1, Commercial = 2, Any = 3</c>, a well-meaning <c>"Domestic,Commercial"</c> ORs to
        /// <c>Any</c> - the most permissive value there is - and <c>Enum.IsDefined</c> cannot catch it
        /// because the result genuinely is defined. That is the one hand-edit the refuse-rather-than-guess
        /// design exists to stop, and it was the only one getting through.
        /// </summary>
        [Theory]
        [InlineData("\"Application\": \"Domestic,Commercial\",")]
        [InlineData("\"Application\": \"Domestic, Commercial\",")]
        [InlineData("\"Application\": \"3\",")]
        [InlineData("\"Application\": \"1\",")]
        [InlineData("\"Application\": \"domestic\",")]
        public void NumericOrListApplication_RefusesTheIndex(string replace)
        {
            string directory = Directory_Temp(Json_Index().Replace("\"Application\": \"Domestic\",", replace));

            try
            {
                Assert.Null(Query.SystemCapabilityDescriptors(directory, SystemApplication.Domestic));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// <b>The eligibility guarantee is scoped to capability selection, and that limit is recorded here
        /// rather than left for somebody to discover.</b> <c>SystemEnergyCentreResource</c> and
        /// <c>SystemEnergyCentre</c> resolve by identity, after a choice has been made, and read no
        /// <c>Application</c> at all - so a caller holding a commercial identity still resolves its file.
        /// That is defensible for resolution, and it is also the mechanism by which the older
        /// <c>Query.DefaultSystemEnergyCentres</c> / <c>Create.SystemEnergyCentre</c> path - which derives a
        /// template from a file NAME and matches it against a space's internal condition, consulting neither
        /// this index nor <c>Application</c> - can still put a commercial system in a dwelling model.
        /// <b>Guarding that path is separate work</b>; this pins the current behaviour so the limit is
        /// visible and a future fix has something to change.
        /// </summary>
        [Fact]
        public void ResolutionIsIdentityDriven_AndDoesNotApplyEligibility()
        {
            Assert.Equal("VAV.json", Template("VAV").SystemEnergyCentreResource(Directory_Resources()));
            Assert.NotNull(Template("VAV").SystemEnergyCentre(Directory_Resources()));

            //Eligibility is decided upstream, where the descriptors are produced - and there it holds.
            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Domestic))
            {
                Assert.NotEqual("VAV", systemCapabilityDescriptor.SystemTemplate.Ventilation);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // A malformed index refuses, it does not answer
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>A broken index yields nothing, not a confident wrong answer.</b> The case that made this
        /// necessary: a review changed VAV's <c>"Rank"</c> to <c>"rank"</c> - one letter - and every dwelling
        /// requirement then selected a commercial variable-air-volume unit in place of a dwelling extract
        /// fan, silently, because a missing rank defaulted to 0 and 0 sorts first. A half-edited index must
        /// disable selection loudly rather than reorder it quietly.
        /// </summary>
        [Theory]
        [InlineData("\"Rank\": 20,", "\"rank\": 20,")]
        [InlineData("\"Rank\": 20,", "")]
        [InlineData("\"Rank\": 20,", "\"Rank\": \"20\",")]
        [InlineData("\"Ventilation\": \"EOL\"", "\"Ventilation\": 5")]
        [InlineData("\"Templates\": [", "\"Templates\": 5, \"Unused\": [")]
        public void MalformedIndex_ProducesNoDescriptors(string find, string replace)
        {
            string json = Json_Index();

            Assert.Contains(find, json);

            string directory = Directory_Temp(json.Replace(find, replace));

            try
            {
                Assert.Null(Query.SystemCapabilityDescriptors(directory, SystemApplication.Undefined));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// <b>A broken entry refuses the index whoever is asking, including when the caller would never have
        /// been offered it.</b> The defect a review found: eligibility was applied before the rest of an
        /// entry was validated, so <c>continue</c> skipped the ventilation, identity and <c>Rank</c> checks
        /// along with the entry - and the same half-edited catalogue was then accepted for one application
        /// and refused for another.
        /// <para>
        /// <b>Broken here is VAV, which is <c>Commercial</c>, and that is the point.</b> It is precisely the
        /// entry whose case-typo'd <c>"rank"</c> the reader's own comment records as the original defect. A
        /// domestic request is never offered VAV, so the reordering is the only thing making a domestic
        /// caller notice that the library it is reading from is half-edited. <c>MalformedIndex_ProducesNoDescriptors</c>
        /// breaks EOL, a domestic entry, and so passed throughout.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData("\"Rank\": 70,", "\"rank\": 70,")]
        [InlineData("\"Rank\": 70,", "")]
        [InlineData("\"Rank\": 70,", "\"Rank\": \"70\",")]
        [InlineData("\"Ventilation\": \"VAV\"", "\"Ventilation\": 5")]
        [InlineData("\"Ventilation\": \"VAV\"", "\"Ventilation\": \"\"")]
        public void BrokenEntryOutsideTheRequestedApplication_StillRefusesTheIndex(string find, string replace)
        {
            string json = Json_Index();

            //The premise: the text really is there to break, and it belongs to a commercial entry.
            Assert.Contains(find, json);

            string directory = Directory_Temp(json.Replace(find, replace));

            try
            {
                //Every request kind, including the two that would never see VAV.
                foreach (SystemApplication systemApplication in new[] { SystemApplication.Domestic, SystemApplication.Commercial, SystemApplication.Any, SystemApplication.Undefined })
                {
                    Assert.Null(Query.SystemCapabilityDescriptors(directory, systemApplication));
                }
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// The other half of the reordering: <b>validating every entry must not change which descriptors a
        /// caller is offered</b>. An intact index answers exactly as before - so the fix above cannot have
        /// been bought by leaking commercial templates into a domestic answer.
        /// </summary>
        [Fact]
        public void ValidatingEveryEntry_DoesNotChangeWhatIsOffered()
        {
            Assert.Equal(["NV", "EOL", "EOC", "MV", "MVRE", "UV"], Sorted(Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Domestic).ConvertAll(x => x.SystemTemplate.Ventilation), ["NV", "EOL", "EOC", "MV", "MVRE", "UV"]));
            Assert.Equal(["CAV", "VAV", "DISP", "UV"], Sorted(Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Commercial).ConvertAll(x => x.SystemTemplate.Ventilation), ["CAV", "VAV", "DISP", "UV"]));
            Assert.Equal(6, Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Domestic).Count);
            Assert.Equal(4, Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Commercial).Count);
        }

        /// <summary>
        /// A <c>Resource</c> is a file name in the resources directory and nothing else. An index that
        /// reached outside the directory it ships in would load whatever it was pointed at.
        /// </summary>
        [Theory]
        [InlineData("..\\\\..\\\\elsewhere.json")]
        [InlineData("sub/MV.json")]
        [InlineData("C:\\\\MV.json")]
        public void ResourceOutsideTheDirectory_IsRefused(string resource)
        {
            string directory = Directory_Temp(Json_Index().Replace("\"Resource\": \"MV.json\",", "\"Resource\": \"" + resource + "\","));

            try
            {
                Assert.Null(Template("MV").SystemEnergyCentreResource(directory));
                Assert.Null(Template("MV").SystemEnergyCentre(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// An index entry written with a stray space still matches a caller who built the same identity
        /// through the constructor. <c>SystemTemplate</c>'s property setters strip spaces but its JSON path
        /// assigns its fields raw, so <c>"M V"</c> and <c>"MV"</c> would otherwise be two systems - the
        /// descriptor would be offered and then resolve to nothing.
        /// </summary>
        [Fact]
        public void IndexEntryWithAStraySpace_StillMatches()
        {
            string directory = Directory_Temp(Json_Index().Replace("\"Ventilation\": \"MV\"", "\"Ventilation\": \"M V\""));

            try
            {
                Assert.Contains("MV", Query.SystemCapabilityDescriptors(directory, SystemApplication.Undefined).ConvertAll(x => x.SystemTemplate.Ventilation));
                Assert.Equal("MV.json", Template("MV").SystemEnergyCentreResource(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // What the shipped library can actually answer
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>Which systems the shipped library can ever select, recorded so a change is noticed.</b> A
        /// review pointed out that nine catalogued templates were expressing a two-valued function; with
        /// mechanical supply in the vocabulary it is four, and the entries that remain unreachable are so
        /// for stated reasons rather than by accident.
        /// <para>
        /// A characterisation test. It endorses nothing - if a rank or a capability changes it will fail, and
        /// the right response is to read it and decide, not to update the expectation blindly.
        /// </para>
        /// </summary>
        [Fact]
        public void ShippedLibrary_AnswersEveryPartFRequirementItCan()
        {
            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Domestic);

            //Continuous only - a natural-ventilation dwelling.
            Assert.Equal("NV", Selected(systemCapabilityDescriptors, SystemCapability.ContinuousVentilation));

            //Continuous plus boost, extract only - Approved Document F system 3.
            Assert.Equal("EOL", Selected(systemCapabilityDescriptors, SystemCapability.ContinuousVentilation | SystemCapability.Boost));

            //Continuous plus mechanical supply - balanced, no wet room above the whole-dwelling rate.
            Assert.Equal("MV", Selected(systemCapabilityDescriptors, SystemCapability.ContinuousVentilation | SystemCapability.MechanicalSupply));

            //All three - system 4, the MVHR case. MV, because Part F does not require heat recovery.
            Assert.Equal("MV", Selected(systemCapabilityDescriptors, SystemCapability.ContinuousVentilation | SystemCapability.MechanicalSupply | SystemCapability.Boost));

            //Heat recovery, once something asks for it.
            Assert.Equal("MVRE", Selected(systemCapabilityDescriptors, SystemCapability.ContinuousVentilation | SystemCapability.MechanicalSupply | SystemCapability.HeatRecovery));

            //And nothing in the shipped library can bypass, so Iteration 2 is blocked loudly.
            Assert.Null(Selected(systemCapabilityDescriptors, SystemCapability.ContinuousVentilation | SystemCapability.SummerBypass));
        }

        /// <summary>
        /// <b>A balanced dwelling is never offered an extract-only system.</b> The defect a review found: a
        /// design with a supply terminal in every habitable room - paragraph 1.67 - was met by
        /// <c>Local Extract Only</c>, because extract-only does run continuously and can boost. The
        /// overheating simulation would have run a system with no supply and no heat recovery against a
        /// building that has both.
        /// </summary>
        [Fact]
        public void BalancedDwelling_IsNeverOfferedAnExtractOnlySystem()
        {
            PartFDwellingResult partFDwellingResult = new("Flat 1")
            {
                ContinuousDesignSystemRate_Lps = 21.0,
                TotalSupply_Lps = 21.0,
                TotalHighSupply_Lps = 21.0,
                TotalHighExtract_Lps = 39.0
            };

            SystemCapabilityRequirement systemCapabilityRequirement = partFDwellingResult.PartFSystemCapabilityRequirement();

            Assert.True(systemCapabilityRequirement.Requires(SystemCapability.MechanicalSupply));

            List<SystemCapabilityDescriptor> systemCapabilityDescriptors = Query.SystemCapabilityDescriptors(Directory_Resources(), SystemApplication.Domestic);

            foreach (SystemCapabilityDescriptor systemCapabilityDescriptor in systemCapabilityDescriptors.CapableSystems(systemCapabilityRequirement))
            {
                Assert.DoesNotContain(systemCapabilityDescriptor.SystemTemplate.Ventilation, new[] { "EOL", "EOC", "NV", "UV" });
            }

            Assert.Equal("MV", Selected(systemCapabilityDescriptors, systemCapabilityRequirement.Capabilities));
        }

        // ---------------------------------------------------------------------------------------------
        // Fixture
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The given names in the given order, so a set comparison reads as a set rather than depending on
        /// the order the index happens to list them in.
        /// <para>
        /// <b>Set equality, not ordering, and it de-duplicates</b> - so callers assert the count separately.
        /// A missing name shortens the list and a surplus one is appended, both of which fail the
        /// comparison; a repeated one would not, which is what the count catches.
        /// </para>
        /// </summary>
        private static List<string> Sorted(List<string> texts, IEnumerable<string> order)
        {
            List<string> result = [];

            foreach (string text in order)
            {
                if (texts.Contains(text))
                {
                    result.Add(text);
                }
            }

            //Anything unexpected is appended, so a surplus entry fails the comparison rather than hiding.
            foreach (string text in texts)
            {
                if (!result.Contains(text))
                {
                    result.Add(text);
                }
            }

            return result;
        }

        private static string Json_Index()
        {
            return File.ReadAllText(Path.Combine(Directory_Resources(), Query.CapabilityIndexFileName));
        }

        /// <summary>A temp directory holding one capability index, for the malformed-index cases.</summary>
        private static string Directory_Temp(string json)
        {
            string result = Path.Combine(Path.GetTempPath(), "SAM_CapabilityIndex_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(result);
            File.WriteAllText(Path.Combine(result, Query.CapabilityIndexFileName), json);

            return result;
        }

        private static SystemTemplate Template(string ventilation)
        {
            return new SystemTemplate(ventilation, null, null, null, null, null);
        }

        private static string Selected(List<SystemCapabilityDescriptor> systemCapabilityDescriptors, SystemCapability systemCapability)
        {
            return systemCapabilityDescriptors.SelectPreferredCapableSystem(new SystemCapabilityRequirement(systemCapability)).SystemTemplate?.Ventilation;
        }

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

        private static List<double> SensibleEfficiencies(string json)
        {
            return Values(json, "SensibleEfficiency");
        }

        private static List<double> LatentEfficiencies(string json)
        {
            return Values(json, "LatentEfficiency");
        }

        /// <summary>The names of the exchangers a template holds.</summary>
        private static List<string> ExchangerNames(string json)
        {
            List<string> result = [];

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(json, @"""Name"":\s*""(Exchanger[^""]*)"""))
            {
                if (!result.Contains(match.Groups[1].Value))
                {
                    result.Add(match.Groups[1].Value);
                }
            }

            return result;
        }

        /// <summary>
        /// The distinct values of a named efficiency in a template, read by pattern rather than by
        /// deserialising - this test may open the file, but there is no reason to build the whole object
        /// graph to read one number, and doing so would make the test depend on every type in it.
        /// <para>
        /// Returned as numbers, not text: "0.0" and "0" are the same efficiency written two ways, and
        /// sorting them as strings would put "0.7" before "10".
        /// </para>
        /// </summary>
        private static List<double> Values(string json, string name)
        {
            List<double> result = [];

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(json, @"""" + name + @""":\s*\{[^}]*?""Value"":\s*([0-9.eE+-]+)"))
            {
                double value = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

                if (!result.Contains(value))
                {
                    result.Add(value);
                }
            }

            result.Sort();

            return result;
        }
    }
}
