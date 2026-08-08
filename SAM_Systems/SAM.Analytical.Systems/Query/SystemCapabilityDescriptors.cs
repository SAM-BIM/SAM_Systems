// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
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
        /// <param name="systemApplication">
        /// What kind of building is being assessed. <b>An eligibility constraint applied here, before
        /// anything is handed over</b>: asking for <see cref="SystemApplication.Domestic"/> returns the
        /// domestic templates and the ones marked <see cref="SystemApplication.Any"/>, and a commercial
        /// air-handling type is not offered at all rather than merely ranked behind them. Approved
        /// Document F selection must pass <c>Domestic</c>.
        /// <para>
        /// <see cref="SystemApplication.Undefined"/> applies no constraint and returns everything, which
        /// is the right answer for a caller that has not said what it is assessing and the wrong one for
        /// a dwelling.
        /// </para>
        /// </param>
        public static List<SystemCapabilityDescriptor> SystemCapabilityDescriptors(string directory = null, SystemApplication systemApplication = SystemApplication.Undefined)
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
                    return null;
                }

                if (!(jsonObject_Template["SystemTemplate"] is JsonObject jsonObject_SystemTemplate))
                {
                    return null;
                }

                //An ABSENT ventilation key means the entry is not a ventilation template - a plant room is
                //not a way of ventilating a dwelling - and is skipped. A key that is PRESENT but is not
                //usable text is a broken entry, and refuses the whole index: skipping it would make one
                //system quietly vanish from the library, which is the silent shortfall this reader exists to
                //avoid.
                if (jsonObject_SystemTemplate.ContainsKey("Ventilation") == false)
                {
                    continue;
                }

                //Eligibility, decided before the entry becomes a descriptor. An entry whose Application is
                //missing or unrecognised is a broken entry and refuses the index: defaulting it to eligible
                //would offer an unclassified template to a dwelling, and defaulting it to ineligible would
                //make a system quietly vanish. Neither is a thing to guess at.
                if (!Enum.TryParse(Text(jsonObject_Template, "Application"), out SystemApplication systemApplication_Template) || !Enum.IsDefined(typeof(SystemApplication), systemApplication_Template) || systemApplication_Template == SystemApplication.Undefined)
                {
                    return null;
                }

                if (!IsEligible(systemApplication_Template, systemApplication))
                {
                    continue;
                }

                //Read as text rather than through SystemTemplate's own JSON constructor, which calls
                //GetValue<string> and throws on a non-string - and built through the six-argument
                //constructor, so the property setters normalise it. SystemTemplate's JSON path assigns its
                //fields raw, so an entry reading "M V" would otherwise be offered as a selectable system
                //that then resolves to nothing, because a caller's constructor-built "M V" is "MV".
                string ventilation = Text(jsonObject_SystemTemplate, "Ventilation");

                if (string.IsNullOrWhiteSpace(ventilation))
                {
                    return null;
                }

                SystemTemplate systemTemplate = new SystemTemplate(ventilation, Text(jsonObject_SystemTemplate, "Heating"), Text(jsonObject_SystemTemplate, "Cooling"), Text(jsonObject_SystemTemplate, "PlantRoom"), Text(jsonObject_SystemTemplate, "Controls"), Text(jsonObject_SystemTemplate, "Version"));

                if (!systemTemplate.IsValid)
                {
                    return null;
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

                if (Flag(jsonObject_Template, "MechanicalSupply"))
                {
                    systemCapability |= SystemCapability.MechanicalSupply;
                }

                if (Flag(jsonObject_Template, "HeatRecovery"))
                {
                    systemCapability |= SystemCapability.HeatRecovery;
                }

                //A missing or non-integer Rank REFUSES THE WHOLE INDEX. An earlier revision defaulted it to
                //0 with a comment claiming that showed up as a tie and would be refused - which was simply
                //wrong: one missing rank is a unique 0, it sorts FIRST, and the entry somebody forgot to
                //rank becomes the preferred answer. A review found a case typo on VAV's "Rank" silently
                //selecting a commercial variable-air-volume unit in place of a dwelling extract fan.
                if (!(jsonObject_Template["Rank"] is JsonValue jsonValue_Rank) || !jsonValue_Rank.TryGetValue(out int rank))
                {
                    return null;
                }

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
        /// Whether a template of one application may be offered for an assessment of another.
        /// <para>
        /// <b>Only three things make a template eligible</b>: nothing was asked for, it is exactly what was
        /// asked for, or it suits either. Everything else is excluded - a commercial air-handling type is
        /// not a worse answer for a dwelling, it is not an answer.
        /// </para>
        /// </summary>
        private static bool IsEligible(SystemApplication systemApplication_Template, SystemApplication systemApplication_Requested)
        {
            if (systemApplication_Requested == SystemApplication.Undefined)
            {
                return true;
            }

            return systemApplication_Template == systemApplication_Requested || systemApplication_Template == SystemApplication.Any;
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

        /// <summary>
        /// A string property of an index entry, or null where it is absent or is not a string. Never throws:
        /// a hand-edited index must degrade rather than take a whole model down with it.
        /// </summary>
        private static string Text(JsonObject jsonObject, string name)
        {
            return jsonObject[name] is JsonValue jsonValue && jsonValue.TryGetValue(out string result) ? result : null;
        }
    }
}
