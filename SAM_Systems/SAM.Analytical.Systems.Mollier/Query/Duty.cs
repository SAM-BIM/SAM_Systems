// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Query
    {
        /// <summary>
        /// Total duty [W] of a Mollier process for a given design airflow.
        /// </summary>
        /// <remarks>
        /// Mollier processes describe intensive air states only, so a design airflow is required to
        /// obtain an extensive duty. The volumetric airflow is converted to a mass flow using the
        /// moist-air density at the process inlet (Start), and the duty is the magnitude of the
        /// enthalpy change across the process: |m_dot * (h_end - h_start)|. The realising component
        /// type (heating vs cooling coil) carries the direction, so a magnitude is returned.
        /// </remarks>
        /// <param name="mollierProcess">Psychrometric process.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <returns>Duty [W], or double.NaN when it cannot be evaluated.</returns>
        public static double Duty(this IMollierProcess mollierProcess, double designAirflow)
        {
            if (mollierProcess == null || double.IsNaN(designAirflow))
            {
                return double.NaN;
            }

            MollierPoint start = mollierProcess.Start;
            MollierPoint end = mollierProcess.End;

            if (start == null || end == null || !start.IsValid() || !end.IsValid())
            {
                return double.NaN;
            }

            double massFlow = MassFlow(mollierProcess, designAirflow);
            if (double.IsNaN(massFlow))
            {
                return double.NaN;
            }

            return System.Math.Abs(massFlow * (end.Enthalpy - start.Enthalpy));
        }

        /// <summary>
        /// Mass flow [kg/s] from a design volumetric airflow [m3/s] using the moist-air density at the process inlet.
        /// </summary>
        public static double MassFlow(this IMollierProcess mollierProcess, double designAirflow)
        {
            if (mollierProcess == null || double.IsNaN(designAirflow))
            {
                return double.NaN;
            }

            MollierPoint start = mollierProcess.Start;
            if (start == null || !start.IsValid())
            {
                return double.NaN;
            }

            double density = start[MollierPointProperty.Density];
            if (double.IsNaN(density))
            {
                return double.NaN;
            }

            return designAirflow * density;
        }
    }
}
