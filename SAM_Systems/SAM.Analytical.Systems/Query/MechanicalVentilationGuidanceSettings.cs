// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Globalization;

namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        /// <summary>
        /// SAM#123: a selected product's manufacturer-guidance settings for one dwelling unit - its catalogue
        /// operating strategy resolved to an elevated operating airflow, with the product's own published
        /// table (which states the cooling capacity bound) and a traceable source identifier.
        /// <para>
        /// <b>The elevated airflow is an operating airflow, not a capacity.</b> Where the strategy states none,
        /// it is the midpoint of the range the manufacturer states for cooling operation - a declared default,
        /// recorded as such - and it must lie within what the unit can move on both sides. The design airflow
        /// stays the dwelling's own; nothing here changes it.
        /// </para>
        /// </summary>
        /// <param name="ventilationUnitTemplate">The selected product's catalogue entry.</param>
        /// <param name="refusal">Why no settings could be stated, or null.</param>
        /// <returns>The settings, or null where <paramref name="refusal"/> says why not.</returns>
        public static MechanicalVentilationGuidanceSettings MechanicalVentilationGuidanceSettings(this VentilationUnitTemplate ventilationUnitTemplate, out string refusal)
        {
            refusal = null;

            VentilationUnitOperatingStrategy strategy = ventilationUnitTemplate?.OperatingStrategy;
            if (strategy == null)
            {
                refusal = "states no manufacturer operating strategy.";
                return null;
            }

            string refusal_Template = strategy.TemplateRefusal();
            if (refusal_Template != null)
            {
                refusal = string.Format("has an operating strategy that {0}", refusal_Template);
                return null;
            }

            double elevated_Lps = strategy.ElevatedAirFlow_Lps;
            if (double.IsNaN(elevated_Lps))
            {
                if (double.IsNaN(strategy.MinimumElevatedAirFlow_Lps) || double.IsNaN(strategy.MaximumElevatedAirFlow_Lps))
                {
                    refusal = "states neither an elevated cooling airflow nor the range it is chosen from.";
                    return null;
                }

                elevated_Lps = (strategy.MinimumElevatedAirFlow_Lps + strategy.MaximumElevatedAirFlow_Lps) / 2.0;
            }

            if (!(elevated_Lps <= ventilationUnitTemplate.MaximumSupplyFlowRate_Lps) || !(elevated_Lps <= ventilationUnitTemplate.MaximumExtractFlowRate_Lps))
            {
                refusal = string.Format(
                    CultureInfo.InvariantCulture,
                    "would operate at {0:0.###} l/s while cooling, beyond the unit's {1:0.###} / {2:0.###} l/s supply / extract capacity.",
                    elevated_Lps,
                    ventilationUnitTemplate.MaximumSupplyFlowRate_Lps,
                    ventilationUnitTemplate.MaximumExtractFlowRate_Lps);

                return null;
            }

            MechanicalVentilationGuidanceSettings result = new MechanicalVentilationGuidanceSettings
            {
                OperatingStrategy = strategy.WithElevatedAirFlow(elevated_Lps),
                SupplyAirTemperatureTable = ventilationUnitTemplate.PerformanceTable,
                SourceIdentifier = string.Format("{0} | {1}", ventilationUnitTemplate.VentilationUnitReference, strategy.Source),
            };

            refusal = result.Refusal();

            return refusal == null ? result : null;
        }
    }
}
