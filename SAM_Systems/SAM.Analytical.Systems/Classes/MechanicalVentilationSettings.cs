// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// The generic options of a mechanical-ventilation materialisation.
    /// <para>
    /// <b>Only what is generic belongs here.</b> A fan's heat gain factor, whether there is heat
    /// recovery, and what a coil is configured to do are properties of the topology template the caller
    /// supplies - the caller loads its own template instance and sets them there. What is left is the
    /// operating schedule, the result's name, and whether the rooms carry the template prototype's
    /// in-room components at all.
    /// </para>
    /// <para>
    /// <b>Copied in and copied out.</b> Both copy paths clone the schedule, so a caller cannot reach into
    /// a materialised graph by mutating the settings object it passed, and cannot have its own schedule
    /// changed by anything the materialisation does.
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

            return result;
        }
    }
}
