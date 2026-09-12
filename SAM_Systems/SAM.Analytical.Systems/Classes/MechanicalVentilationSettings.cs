// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// The generic options of a mechanical-ventilation materialisation.
    /// <para>
    /// <b>Only what is generic belongs here.</b> A fan's heat gain factor, whether there is heat
    /// recovery, and what a coil is configured to do are properties of the topology template the caller
    /// supplies - the caller loads its own template instance and sets them there. What is left is the
    /// operating schedule, the result's name, whether the rooms carry the template prototype's in-room
    /// components at all, and - PR5A (SAM#111 plan §C) - each physical unit's own resolved manufacturer
    /// behaviour, which cannot live on the one shared template because two units in the same design can
    /// carry two different selected products.
    /// </para>
    /// <para>
    /// <b>Copied in and copied out.</b> Both copy paths clone the schedule and every entry of
    /// <see cref="UnitSettings"/>, so a caller cannot reach into a materialised graph by mutating the
    /// settings object it passed, and cannot have its own schedule or unit settings changed by anything
    /// the materialisation does.
    /// </para>
    /// </summary>
    public class MechanicalVentilationSettings : ISystemJSAMObject
    {
        /// <summary>
        /// The operating schedule assigned to the materialised air-side fans, which reference it by name.
        /// Null leaves the template's own schedule assignment untouched.
        /// <para>
        /// <c>ISchedule</c> rather than <c>YearlySchedule</c>: a generic materialisation is not restricted
        /// to annual or constant schedules. What is required of whatever is supplied is that it states a
        /// name - components reference schedules by name, so an unnamed one is unreachable - and that it
        /// is complete and finite over the period being materialised.
        /// </para>
        /// </summary>
        public ISchedule Schedule { get; set; }

        /// <summary>The name given to the materialised energy centre. Null keeps the template's name.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the materialised spaces carry the template prototype's in-room components - radiators,
        /// fan coil units, chilled beams. False materialises air distribution only.
        /// </summary>
        public bool MaterialiseSystemSpaceComponents { get; set; }

        private Dictionary<Guid, MechanicalVentilationUnitSettings> dictionary_UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>();

        /// <summary>
        /// Each physical air handling unit's resolved manufacturer-aware behaviour, keyed by the
        /// analytical <c>AirHandlingUnit.Guid</c> - PR5A (SAM#111 plan §C).
        /// <para>
        /// <b>All-or-nothing per materialisation call.</b> An empty dictionary (the default) materialises
        /// exactly as PR1 did - the B0 control - and is never touched by anything below. A non-empty
        /// dictionary has to name every air handling unit the call materialises: a unit left out would be a
        /// partially configured graph, which <c>Create.MechanicalVentilation</c> refuses rather than
        /// silently defaulting to parity for the units nobody mentioned. A key naming a unit the call does
        /// not materialise is refused the same way.
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<Guid, MechanicalVentilationUnitSettings> UnitSettings
        {
            get
            {
                return dictionary_UnitSettings;
            }
            set
            {
                Dictionary<Guid, MechanicalVentilationUnitSettings> dictionary = new Dictionary<Guid, MechanicalVentilationUnitSettings>();

                if (value != null)
                {
                    foreach (KeyValuePair<Guid, MechanicalVentilationUnitSettings> keyValuePair in value)
                    {
                        dictionary[keyValuePair.Key] = keyValuePair.Value == null ? null : new MechanicalVentilationUnitSettings(keyValuePair.Value);
                    }
                }

                dictionary_UnitSettings = dictionary;
            }
        }

        public MechanicalVentilationSettings()
        {

        }

        public MechanicalVentilationSettings(MechanicalVentilationSettings mechanicalVentilationSettings)
        {
            if (mechanicalVentilationSettings != null)
            {
                Schedule = Core.Query.Clone(mechanicalVentilationSettings.Schedule);
                Name = mechanicalVentilationSettings.Name;
                MaterialiseSystemSpaceComponents = mechanicalVentilationSettings.MaterialiseSystemSpaceComponents;
                UnitSettings = mechanicalVentilationSettings.UnitSettings;
            }
        }

        public MechanicalVentilationSettings(JsonObject jsonObject)
        {
            FromJsonObject(jsonObject);
        }

        public bool FromJsonObject(JsonObject jsonObject)
        {
            if (jsonObject == null)
            {
                return false;
            }

            if (jsonObject.ContainsKey("Name"))
            {
                Name = jsonObject["Name"]?.GetValue<string>();
            }

            if (jsonObject.ContainsKey("MaterialiseSystemSpaceComponents"))
            {
                MaterialiseSystemSpaceComponents = jsonObject["MaterialiseSystemSpaceComponents"]?.GetValue<bool>() ?? default;
            }

            if (jsonObject.ContainsKey("Schedule"))
            {
                Schedule = Core.Query.IJSAMObject<ISchedule>(jsonObject["Schedule"] as JsonObject);
            }

            if (jsonObject["UnitSettings"] is JsonObject jsonObject_UnitSettings)
            {
                Dictionary<Guid, MechanicalVentilationUnitSettings> dictionary = new Dictionary<Guid, MechanicalVentilationUnitSettings>();

                foreach (KeyValuePair<string, JsonNode> keyValuePair in jsonObject_UnitSettings)
                {
                    if (Guid.TryParse(keyValuePair.Key, out Guid guid) && keyValuePair.Value is JsonObject jsonObject_Value)
                    {
                        dictionary[guid] = new MechanicalVentilationUnitSettings(jsonObject_Value);
                    }
                }

                dictionary_UnitSettings = dictionary;
            }

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject result = new JsonObject
            {
                { "_type", Core.Query.FullTypeName(this) },
                { "MaterialiseSystemSpaceComponents", MaterialiseSystemSpaceComponents }
            };

            if (Name != null)
            {
                result.Add("Name", Name);
            }

            if (Schedule != null)
            {
                result.Add("Schedule", Schedule.ToJsonObject());
            }

            if (dictionary_UnitSettings.Count != 0)
            {
                JsonObject jsonObject_UnitSettings = new JsonObject();

                foreach (KeyValuePair<Guid, MechanicalVentilationUnitSettings> keyValuePair in dictionary_UnitSettings)
                {
                    jsonObject_UnitSettings.Add(keyValuePair.Key.ToString(), keyValuePair.Value?.ToJsonObject());
                }

                result.Add("UnitSettings", jsonObject_UnitSettings);
            }

            return result;
        }
    }
}
