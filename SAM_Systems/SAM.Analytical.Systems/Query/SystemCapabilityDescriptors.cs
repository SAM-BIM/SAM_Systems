// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        /// <summary>The capability index that sits beside the shipped energy-centre templates.</summary>
        public const string CapabilityIndexFileName = "CapabilityIndex.JSON";

        /// <summary>
        /// What each shipped <c>SystemEnergyCentre</c> template can do, read from the small index beside
        /// them.
        /// <para>
        /// <b>The templates themselves are never opened.</b> Each is between one and two megabytes of plant
        /// rooms, energy sources and schematic, and none of it answers "can this boost?" any faster than
        /// the four flags in the index do. Loading ten of them to choose one would cost twelve megabytes of
        /// parsing to read forty bits.
        /// </para>
        /// <para>
        /// <b>This is where the capability values live, and it is deliberate.</b> Which of <i>these</i>
        /// files provides what is a fact about this repository's own resources. <c>SAM.Analytical</c> owns
        /// the vocabulary, the Approved Document F requirement rule and the selection rule, and is handed
        /// descriptors - so the core library carries no list of these ten files, and adding a template here
        /// needs no change there.
        /// </para>
        /// <para>
        /// Entries are keyed on the existing <c>SystemTemplate</c> identity, and only its ventilation part
        /// is stated: these are ventilation templates, and heating, cooling, plant room and controls are
        /// chosen elsewhere and do not select among them. An entry that names no ventilation system is not
        /// a candidate and is skipped - <c>Plantroom-Only.json</c> is a plant room, not a way of
        /// ventilating a dwelling.
        /// </para>
        /// </summary>
        /// <param name="directory">
        /// Where to read the index from. Null uses the shipped resources directory. Supplied so a test can
        /// point at the repository's own resources rather than a copy.
        /// </param>
        public static List<SystemCapabilityDescriptor> SystemCapabilityDescriptors(string directory = null)
        {
            JsonObject jsonObject = SystemCapabilityIndex(directory);
            if (jsonObject == null)
            {
                return null;
            }

            if (!(jsonObject["Templates"] is JsonArray jsonArray))
            {
                return null;
            }

            List<SystemCapabilityDescriptor> result = new List<SystemCapabilityDescriptor>();

            foreach (JsonNode jsonNode in jsonArray)
            {
                if (!(jsonNode is JsonObject jsonObject_Template))
                {
                    continue;
                }

                if (!(jsonObject_Template["SystemTemplate"] is JsonObject jsonObject_SystemTemplate))
                {
                    continue;
                }

                SystemTemplate systemTemplate = new SystemTemplate(jsonObject_SystemTemplate);
                if (!systemTemplate.IsValid)
                {
                    continue;
                }

                SystemCapability systemCapability = SystemCapability.None;

                if (Flag(jsonObject_Template, "ContinuousVentilation"))
                {
                    systemCapability |= SystemCapability.ContinuousVentilation;
                }

                if (Flag(jsonObject_Template, "Boost"))
                {
                    systemCapability |= SystemCapability.Boost;
                }

                if (Flag(jsonObject_Template, "SummerBypass"))
                {
                    systemCapability |= SystemCapability.SummerBypass;
                }

                if (Flag(jsonObject_Template, "HeatRecovery"))
                {
                    systemCapability |= SystemCapability.HeatRecovery;
                }

                //A missing rank is 0, which sorts before every stated one - so a half-edited index shows up
                //as a tie and is refused, rather than quietly preferring the entry somebody forgot.
                int rank = jsonObject_Template["Rank"] is JsonValue jsonValue && jsonValue.TryGetValue(out int rank_Temp) ? rank_Temp : 0;

                result.Add(new SystemCapabilityDescriptor(systemTemplate, systemCapability, rank));
            }

            return result;
        }

        /// <summary>
        /// The capability index as it stands on disk, for a test that needs more of it than the descriptors
        /// carry - which resource file each entry names, and which capabilities it claims were established
        /// by reading the template rather than declared from the system type.
        /// </summary>
        public static JsonObject SystemCapabilityIndex(string directory = null)
        {
            string directory_Temp = string.IsNullOrWhiteSpace(directory) ? DefaultSystemEnergyCentreDirectory() : directory;

            if (string.IsNullOrWhiteSpace(directory_Temp))
            {
                return null;
            }

            string path = Path.Combine(directory_Temp, CapabilityIndexFileName);

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
        /// A boolean flag from an index entry. <b>Absent means false</b>: a capability nobody stated is one
        /// the system is not credited with, which is the safe direction - an unstated capability produces a
        /// refusal, and a wrongly credited one produces an assessment of a building that was never
        /// designed.
        /// </summary>
        private static bool Flag(JsonObject jsonObject, string name)
        {
            return jsonObject[name] is JsonValue jsonValue && jsonValue.TryGetValue(out bool result) && result;
        }
    }
}
