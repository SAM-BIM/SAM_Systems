// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Query
    {
        /// <summary>
        /// Supply-side sensible and latent effectiveness of a heat-recovery device, derived from its supply and
        /// extract (exhaust) Mollier processes.
        /// </summary>
        /// <remarks>
        /// Both effectiveness values use the standard supply-side definition relative to the difference between
        /// the two entering air streams:
        /// <code>
        ///   sensible = (supplyOut.DryBulbTemperature - supplyIn.DryBulbTemperature)
        ///            / (extractIn.DryBulbTemperature - supplyIn.DryBulbTemperature)
        ///   latent   = (supplyOut.HumidityRatio - supplyIn.HumidityRatio)
        ///            / (extractIn.HumidityRatio - supplyIn.HumidityRatio)
        /// </code>
        /// where supplyIn/supplyOut are the supply process Start/End and extractIn is the extract process Start.
        /// Both are returned as fractions in [0, 1]. Latent effectiveness is only evaluated when the supply
        /// process actually changes humidity ratio (i.e. a latent-capable device such as a twin-wheel);
        /// otherwise it is returned as double.NaN.
        /// </remarks>
        /// <param name="supplyHeatRecoveryProcess">The supply-side heat-recovery process (Start = fresh air on, End = after recovery).</param>
        /// <param name="extractHeatRecoveryProcess">The extract-side heat-recovery process (Start = room/exhaust air on).</param>
        /// <param name="sensibleEfficiency">Sensible effectiveness [0..1], or NaN when it cannot be evaluated.</param>
        /// <param name="latentEfficiency">Latent effectiveness [0..1], or NaN when there is no moisture transfer.</param>
        public static void HeatRecoveryEfficiencies(this HeatRecoveryProcess supplyHeatRecoveryProcess, HeatRecoveryProcess extractHeatRecoveryProcess, out double sensibleEfficiency, out double latentEfficiency)
        {
            sensibleEfficiency = double.NaN;
            latentEfficiency = double.NaN;

            if (supplyHeatRecoveryProcess == null || extractHeatRecoveryProcess == null)
            {
                return;
            }

            MollierPoint supplyIn = supplyHeatRecoveryProcess.Start;
            MollierPoint supplyOut = supplyHeatRecoveryProcess.End;
            MollierPoint extractIn = extractHeatRecoveryProcess.Start;

            if (supplyIn == null || supplyOut == null || extractIn == null
                || !supplyIn.IsValid() || !supplyOut.IsValid() || !extractIn.IsValid())
            {
                return;
            }

            double temperatureDifference = extractIn.DryBulbTemperature - supplyIn.DryBulbTemperature;
            if (System.Math.Abs(temperatureDifference) > 1e-9)
            {
                sensibleEfficiency = Clamp((supplyOut.DryBulbTemperature - supplyIn.DryBulbTemperature) / temperatureDifference);
            }

            // Only a latent-capable device shifts the supply humidity ratio.
            if (System.Math.Abs(supplyOut.HumidityRatio - supplyIn.HumidityRatio) > 1e-6)
            {
                double humidityRatioDifference = extractIn.HumidityRatio - supplyIn.HumidityRatio;
                if (System.Math.Abs(humidityRatioDifference) > 1e-12)
                {
                    latentEfficiency = Clamp((supplyOut.HumidityRatio - supplyIn.HumidityRatio) / humidityRatioDifference);
                }
            }
        }

        private static double Clamp(double value)
        {
            if (double.IsNaN(value))
            {
                return double.NaN;
            }

            if (value < 0)
            {
                return 0;
            }

            if (value > 1)
            {
                return 1;
            }

            return value;
        }
    }
}
