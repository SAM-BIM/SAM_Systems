// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        /// <summary>The manufacturer ventilation unit catalogue shipped with this repository.</summary>
        public const string VentilationUnitCatalogueFileName = "VentilationUnitCatalogue.JSON";

        /// <summary>
        /// The exact schema tag every ventilation unit catalogue this reader accepts must declare.
        /// <para>
        /// Versioned rather than assumed: a future reshaping of the catalogue - a field renamed, a shape
        /// changed - has to be a new tag, and this reader has to be taught that tag before it reads a file
        /// carrying it. An unrecognised tag is refused, never quietly parsed as if it were this version -
        /// that is exactly the mistake that would let a genuinely different file be read as though it agreed
        /// with code that was never updated for it.
        /// </para>
        /// </summary>
        public const string VentilationUnitCatalogueSchema = "VentilationUnitCatalogue:v1";

        /// <summary>
        /// The schema tag a catalogue may declare to carry the PR5A manufacturer-aware vocabulary -
        /// <c>HeatRecoveryPerformance</c> and <c>FanPerformance</c> (SAM#111 plan §K.1) - on a template.
        /// <para>
        /// <b>v1 stays legal and stays exactly what it always meant: "no behaviour data".</b> This reader
        /// accepts both tags identically; neither the tag nor the presence of the new fields is required of
        /// a v2 file. A v1 file with the new fields absent, and a v2 file with them absent, read as the same
        /// thing. What v2 adds is only that a template <i>may</i> also carry them - and if it does, they are
        /// validated exactly as every other optional field here is: present-and-unusable refuses the whole
        /// catalogue, absent is legal.
        /// </para>
        /// </summary>
        public const string VentilationUnitCatalogueSchemaV2 = "VentilationUnitCatalogue:v2";

        /// <summary>
        /// The manufacturer ventilation unit products this repository ships - what each one is, where its
        /// figures came from, what it can move, and what it does under the conditions its manufacturer
        /// published.
        /// <para>
        /// <b>This is the catalogue seam Approved Document O Iteration 2 was left needing.</b>
        /// <c>SAM.Analytical</c> owns the vocabulary and the selection rule and is <i>handed</i>
        /// descriptors; which products exist is a fact about whoever is asking, and for anyone asking this
        /// repository it is this file. The exact arrangement <see cref="SystemCapabilityDescriptors"/>
        /// established for system templates, and for the same reason: the core library carries no
        /// manufacturer list, adding a product needs no change there, and choosing a unit in
        /// <c>SAM.Analytical</c> opens no file.
        /// </para>
        /// <para>
        /// <b>One unusable entry refuses the whole catalogue.</b> Skipping it would make a product quietly
        /// vanish from the library, and a dwelling would then be told nothing offered could serve it - or,
        /// worse, be given the next unit up - because of a typo nobody was shown. That is the same
        /// refuse-loudly rule <see cref="SystemCapabilityDescriptors"/> follows, and it is the reason every
        /// entry is validated before any of them is returned.
        /// </para>
        /// <para>
        /// <b>A template without an established capacity is returned, not dropped.</b> It is a complete,
        /// valid record of a published product; it is simply not <i>selectable</i>, which
        /// <c>Analytical.Query.CapacityDescriptors</c> decides and
        /// <c>Analytical.Query.UnselectableVentilationUnitTemplates</c> reports. Dropping it here would
        /// hide from Iteration 3 the very performance data it exists to carry.
        /// </para>
        /// </summary>
        /// <param name="directory">
        /// Where to read the catalogue from. Null uses the shipped resources directory. Supplied so a test
        /// can point at this repository's own resources rather than a copy.
        /// </param>
        /// <returns>The templates, or null where the catalogue is missing, unreadable or unusable.</returns>
        public static List<VentilationUnitTemplate> VentilationUnitTemplates(string directory = null)
        {
            JsonObject jsonObject = VentilationUnitCatalogue(directory);
            if (jsonObject == null)
            {
                return null;
            }

            //Checked before anything else is read. Missing, the wrong JSON shape, or a value that is not
            //EXACTLY one of the accepted tags all refuse here - an unrecognised schema, future or
            //otherwise, is never interpreted as one of these versions.
            JsonNode jsonNode_Schema = jsonObject["Schema"];
            if (jsonNode_Schema == null || jsonNode_Schema.GetValueKind() != JsonValueKind.String)
            {
                return null;
            }

            string schema = jsonNode_Schema.GetValue<string>();
            if (!string.Equals(schema, VentilationUnitCatalogueSchema, StringComparison.Ordinal) && !string.Equals(schema, VentilationUnitCatalogueSchemaV2, StringComparison.Ordinal))
            {
                return null;
            }

            if (!(jsonObject["Templates"] is JsonArray jsonArray))
            {
                return null;
            }

            List<VentilationUnitTemplate> result = new List<VentilationUnitTemplate>();

            foreach (JsonNode jsonNode in jsonArray)
            {
                if (!(jsonNode is JsonObject jsonObject_Template))
                {
                    return null;
                }

                VentilationUnitTemplate ventilationUnitTemplate = new VentilationUnitTemplate(jsonObject_Template);

                //Named product, and a source its figures can be traced to. A template that fails either is
                //not a record of anything.
                if (!ventilationUnitTemplate.IsValid)
                {
                    return null;
                }

                //Performance data is OPTIONAL - a product may be catalogued for selection alone. What is not
                //allowed is data that is PRESENT and unusable: a grid whose values no longer line up with its
                //axes still answers every query, with numbers attributed to the wrong conditions. It has to
                //refuse here, because nothing downstream can tell the difference.
                //
                //Checked against the raw JSON key, not the parsed template - "PerformanceTable": null and a
                //genuinely absent key both parse to a null PerformanceTable, and only the raw key can tell
                //them apart. An absent key is the documented "no performance data" state; a key that is
                //PRESENT but null, or present and not an object VentilationUnitPerformanceTable could parse
                //at all, is malformed data and must refuse the catalogue exactly as an unusable-but-parsed
                //table does, not be silently read as "optional data absent".
                if (jsonObject_Template.ContainsKey("PerformanceTable") && (ventilationUnitTemplate.PerformanceTable == null || !ventilationUnitTemplate.PerformanceTable.IsValid))
                {
                    return null;
                }

                if (jsonObject_Template.ContainsKey("FlowFractionByControlTemperature") && (ventilationUnitTemplate.FlowFractionByControlTemperature == null || !ventilationUnitTemplate.FlowFractionByControlTemperature.IsValid))
                {
                    return null;
                }

                //PR5A manufacturer-aware vocabulary (SAM#111 plan §K.1) - optional on both v1 and v2, and
                //checked the same way as every other optional field above: present-and-unusable refuses the
                //whole catalogue rather than being read as "not stated". A v1 catalogue with neither field
                //behaves exactly as it always has.
                if (jsonObject_Template.ContainsKey("HeatRecoveryPerformance") && (ventilationUnitTemplate.HeatRecoveryPerformance == null || !ventilationUnitTemplate.HeatRecoveryPerformance.IsValid))
                {
                    return null;
                }

                if (jsonObject_Template.ContainsKey("FanPerformance") && (ventilationUnitTemplate.FanPerformance == null || !ventilationUnitTemplate.FanPerformance.IsValid))
                {
                    return null;
                }

                //A capacity that is PRESENT has to be usable, for the same reason. Absent is a legal, named
                //state - see UnresolvedCapacityNote - and is not checked here.
                if (!IsUsableCapacity(jsonObject_Template, "MaximumSupplyFlowRate_Lps") || !IsUsableCapacity(jsonObject_Template, "MaximumExtractFlowRate_Lps"))
                {
                    return null;
                }

                //A MISSING OR NON-INTEGER Rank REFUSES THE WHOLE CATALOGUE, rather than defaulting to zero.
                //This is the trap SystemCapabilityDescriptors records at length and was hardened for: a
                //missing rank is a unique 0, 0 sorts FIRST, and the entry somebody forgot to rank quietly
                //becomes the preferred answer between two products of the same size. Rank decides selections,
                //so it has to be declared.
                if (!(jsonObject_Template["Rank"] is JsonValue jsonValue_Rank) || !jsonValue_Rank.TryGetValue(out int _))
                {
                    return null;
                }

                //One identity, one product. The model stores an identity and looks the template up by it
                //later, so a catalogue giving one identity two entries has no single answer to "what did we
                //select" - the same defect Analytical.Query.SelectSmallestCapableVentilationUnit refuses a
                //catalogue for, caught here at the point the file is read.
                foreach (VentilationUnitTemplate ventilationUnitTemplate_Existing in result)
                {
                    if (ventilationUnitTemplate.VentilationUnitReference.Matches(ventilationUnitTemplate_Existing.VentilationUnitReference))
                    {
                        return null;
                    }
                }

                result.Add(ventilationUnitTemplate);
            }

            return result;
        }

        /// <summary>
        /// The shipped products that can be offered to an Approved Document O ventilation unit selection.
        /// <para>
        /// The one call a Grasshopper component or a workflow needs: it produces exactly the
        /// <see cref="VentilationUnitCapacityDescriptor"/> list <c>Query.SelectSmallestCapableVentilationUnit</c>
        /// consumes, so the selection kernel is reached with real products and is itself unchanged.
        /// </para>
        /// <para>
        /// Null - not empty - where the catalogue could not be read, so "there is no catalogue" and "the
        /// catalogue offers nothing for this duty" stay distinguishable.
        /// </para>
        /// </summary>
        public static List<VentilationUnitCapacityDescriptor> VentilationUnitCapacityDescriptors(string directory = null)
        {
            List<VentilationUnitTemplate> ventilationUnitTemplates = VentilationUnitTemplates(directory);

            return ventilationUnitTemplates == null ? null : Analytical.Query.CapacityDescriptors(ventilationUnitTemplates);
        }

        /// <summary>
        /// The catalogue as it stands on disk, for a test that needs more of it than the templates carry -
        /// its schema tag, its notes. Mirrors <see cref="SystemCapabilityIndex"/>.
        /// </summary>
        public static JsonObject VentilationUnitCatalogue(string directory = null)
        {
            string directory_Temp = string.IsNullOrWhiteSpace(directory) ? DefaultVentilationUnitDirectory() : directory;

            if (string.IsNullOrWhiteSpace(directory_Temp))
            {
                return null;
            }

            string path = Path.Combine(directory_Temp, VentilationUnitCatalogueFileName);

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves the bundled ventilation unit catalogue directory, or null when it cannot be located.
        /// The same resolution <see cref="DefaultSystemEnergyCentreDirectory"/> performs, against its own
        /// setting.
        /// </summary>
        /// <param name="setting">
        /// The setting to resolve against. Null uses <see cref="ActiveSetting.Setting"/> - the parameter
        /// exists so a test can hand in a specific persisted-setting shape (fresh, legacy, custom) without
        /// mutating the process-wide active setting.
        /// </param>
        /// <param name="resourcesDirectory">
        /// The resources root to resolve the leaf against. Null uses <see cref="ResourcesDirectory"/> - the
        /// parameter exists for the same reason <paramref name="setting"/> does: <see cref="ResourcesDirectory"/>
        /// itself falls back to a real per-user install location
        /// (<see cref="Core.Query.UserSAMDirectory"/>) when one happens to exist on the machine, which a test
        /// must not depend on.
        /// </param>
        public static string DefaultVentilationUnitDirectory(Setting setting = null, string resourcesDirectory = null)
        {
            if (setting == null)
            {
                setting = ActiveSetting.Setting;
            }

            string directory = setting?.GetValue<string>(AnalyticalSystemSettingParameter.DefaultVentilationUnitFileDirectory);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }

            if (string.IsNullOrWhiteSpace(resourcesDirectory))
            {
                resourcesDirectory = ResourcesDirectory();
            }

            if (string.IsNullOrWhiteSpace(resourcesDirectory))
            {
                return null;
            }

            string name = setting?.GetValue<string>(AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName);
            if (string.IsNullOrWhiteSpace(name))
            {
                //A Setting persisted before this parameter existed - loaded as-is by ActiveSetting.Load(),
                //not merged with GetDefault() - carries neither value. Fall back to the SAME default leaf
                //GetDefault() defines, rather than the resources root itself: the two must not name the
                //leaf independently, or a change to one silently strands installations relying on the other.
                name = ActiveSetting.GetDefault().GetValue<string>(AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName);
            }

            directory = string.IsNullOrWhiteSpace(name) ? resourcesDirectory : Path.Combine(resourcesDirectory, name);

            return Directory.Exists(directory) ? directory : null;
        }

        /// <summary>
        /// Whether a capacity key is either absent - the legal unresolved state - or a usable number of
        /// litres per second.
        /// <para>
        /// Checked against the raw JSON rather than the parsed template, because the two states this has to
        /// separate both arrive as <see cref="double.NaN"/> on the template: a key nobody wrote, and a key
        /// somebody wrote badly. The first is a documented condition; the second is a mistake that would
        /// otherwise be indistinguishable from it and would take a product out of the catalogue silently.
        /// </para>
        /// </summary>
        private static bool IsUsableCapacity(JsonObject jsonObject, string name)
        {
            if (jsonObject == null || !jsonObject.ContainsKey(name))
            {
                return true;
            }

            //JsonValue.TryGetValue rather than GetValue<object> + Core.Query.IsNumeric: a PARSED number is
            //backed by a JsonElement, which is not a numeric CLR type, so the IsNumeric form would call
            //every capacity in every file unusable and refuse every catalogue on disk.
            JsonValue jsonValue = jsonObject[name] as JsonValue;

            if (jsonValue == null)
            {
                return false;
            }

            double value;
            long value_Long;

            if (jsonValue.TryGetValue(out value))
            {
            }
            else if (jsonValue.TryGetValue(out value_Long))
            {
                value = value_Long;
            }
            else
            {
                return false;
            }

            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        }
    }
}
