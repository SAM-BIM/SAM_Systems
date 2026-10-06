// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Query
    {
        /// <summary>
        /// Coil bypass factor [0..1] derived from a cooling process and its Apparatus Dew Point (ADP).
        /// </summary>
        /// <remarks>
        /// BF = (T_offcoil - T_adp) / (T_oncoil - T_adp), evaluated on dry-bulb temperature, where the
        /// ADP is obtained from <see cref="CoolingProcess.ApparatusDewPoint"/> (which itself accounts for
        /// the process Efficiency). The result is clamped to [0, 1].
        /// </remarks>
        /// <param name="coolingProcess">Cooling process.</param>
        /// <returns>Bypass factor [0..1], or double.NaN when it cannot be evaluated.</returns>
        public static double BypassFactor(this CoolingProcess coolingProcess)
        {
            if (coolingProcess == null)
            {
                return double.NaN;
            }

            MollierPoint start = coolingProcess.Start;
            MollierPoint end = coolingProcess.End;
            MollierPoint apparatusDewPoint = coolingProcess.ApparatusDewPoint();

            if (start == null || end == null || apparatusDewPoint == null
                || !start.IsValid() || !end.IsValid() || !apparatusDewPoint.IsValid())
            {
                return double.NaN;
            }

            double denominator = start.DryBulbTemperature - apparatusDewPoint.DryBulbTemperature;
            if (System.Math.Abs(denominator) < 1e-9)
            {
                return double.NaN;
            }

            double bypassFactor = (end.DryBulbTemperature - apparatusDewPoint.DryBulbTemperature) / denominator;

            if (bypassFactor < 0)
            {
                bypassFactor = 0;
            }
            else if (bypassFactor > 1)
            {
                bypassFactor = 1;
            }

            return bypassFactor;
        }
    }
}
