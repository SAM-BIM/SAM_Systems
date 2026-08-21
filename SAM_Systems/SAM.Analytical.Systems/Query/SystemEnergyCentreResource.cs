// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        /// <summary>
        /// The resource file the capability index names for a system identity, or null where the index does
        /// not name that system or names it more than once.
        /// <para>
        /// <b>Matched on the ventilation identity, and refused on ambiguity.</b> These are ventilation
        /// templates: heating, cooling, plant room and controls are chosen elsewhere and do not select among
        /// them, so a system template's ventilation part is what identifies the file. Two entries claiming
        /// one ventilation identity is a broken index rather than a choice, and returning either would pick
        /// a building's plant by position in a file.
        /// </para>
        /// </summary>
        public static string SystemEnergyCentreResource(this SystemTemplate systemTemplate, string directory = null)
        {
            if (systemTemplate == null || string.IsNullOrWhiteSpace(systemTemplate.Ventilation))
            {
                return null;
            }

            JsonObject jsonObject = SystemCapabilityIndex(directory);

            if (!(jsonObject?["Templates"] is JsonArray jsonArray))
            {
                return null;
            }

            string result = null;

            foreach (JsonNode jsonNode in jsonArray)
            {
                if (!(jsonNode is JsonObject jsonObject_Template) || !(jsonObject_Template["SystemTemplate"] is JsonObject jsonObject_SystemTemplate))
                {
                    continue;
                }

                //Normalised through the property setter, as the descriptors are: SystemTemplate's JSON path
                //assigns its fields raw and its setters strip spaces, so an index entry reading "M V" would
                //otherwise never match a caller's constructor-built "MV".
                string ventilation = jsonObject_SystemTemplate["Ventilation"] is JsonValue jsonValue_Ventilation && jsonValue_Ventilation.TryGetValue(out string text_Ventilation) ? text_Ventilation : null;

                if (string.IsNullOrWhiteSpace(ventilation) || !string.Equals(new SystemTemplate(ventilation, null, null, null, null, null).Ventilation, systemTemplate.Ventilation, StringComparison.Ordinal))
                {
                    continue;
                }

                string resource = jsonObject_Template["Resource"] is JsonValue jsonValue && jsonValue.TryGetValue(out string text) ? text : null;

                //A resource is a file name in the resources directory and nothing else. Anything with a
                //separator or a parent reference in it would let an index reach outside the directory it
                //ships in, so it is refused rather than combined.
                if (string.IsNullOrWhiteSpace(resource) || resource.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || resource.Contains(".."))
                {
                    return null;
                }

                //A second match is a broken index. Refuse rather than take the first.
                if (result != null)
                {
                    return null;
                }

                result = resource;
            }

            return result;
        }

        /// <summary>
        /// The concrete <c>SystemEnergyCentre</c> for a chosen system identity - <b>the one point at which a
        /// template is actually opened</b>, and it happens once, after the choice has been made, for the
        /// system that was chosen.
        /// <para>
        /// Nothing in the selection path reaches this. Capability comes from the small index beside the
        /// templates; this is what turns the answer into the megabyte and a half of plant rooms, energy
        /// sources and schematic that the simulation needs.
        /// </para>
        /// <para>
        /// Returns null where the index does not name the system, names it ambiguously, or names a file that
        /// is not there. <b>No nearest match and no default</b>: a system nobody chose is not a better answer
        /// than none.
        /// </para>
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(this SystemTemplate systemTemplate, string directory = null)
        {
            string resource = SystemEnergyCentreResource(systemTemplate, directory);

            if (string.IsNullOrWhiteSpace(resource))
            {
                return null;
            }

            string directory_Temp = string.IsNullOrWhiteSpace(directory) ? DefaultSystemEnergyCentreDirectory() : directory;

            if (string.IsNullOrWhiteSpace(directory_Temp))
            {
                return null;
            }

            return SystemEnergyCentre(Path.Combine(directory_Temp, resource));
        }

        /// <summary>
        /// Every resource file the capability index accounts for - the ventilation templates and the ones it
        /// explicitly records as not being ventilation templates.
        /// <para>
        /// Exists so a conformance test can prove the index and the shipped resources cover each other
        /// exactly. A template with no entry would be invisible to every selection; an entry with no
        /// template would resolve to a file that is not there.
        /// </para>
        /// </summary>
        public static List<string> SystemEnergyCentreResources_Accounted(string directory = null)
        {
            JsonObject jsonObject = SystemCapabilityIndex(directory);

            if (jsonObject == null)
            {
                return null;
            }

            List<string> result = new List<string>();

            foreach (string name in new[] { "Templates", "NonVentilationResources" })
            {
                if (!(jsonObject[name] is JsonArray jsonArray))
                {
                    continue;
                }

                foreach (JsonNode jsonNode in jsonArray)
                {
                    if (jsonNode is JsonObject jsonObject_Entry && jsonObject_Entry["Resource"] is JsonValue jsonValue && jsonValue.TryGetValue(out string text) && !string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(text);
                    }
                }
            }

            return result;
        }
    }
}
