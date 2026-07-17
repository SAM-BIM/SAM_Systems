// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Query
    {
        /// <summary>
        /// Recovers the fan total pressure rise [Pa] from the air temperature pickup across a
        /// <see cref="FanProcess"/>.
        /// </summary>
        /// <remarks>
        /// Derivation (inverse of SAM.Core.Mollier <c>Query.PickupTemperature</c>): all fan input power ends up
        /// as heat in the air stream, so the pickup is dT = SFP / (rho * cp) with the Specific Fan Power
        /// SFP = dP / eta [W/(l/s) = kJ/m3 = kPa]. Substituting and solving for the pressure:
        /// <code>dP = eta * rho * cp * dT</code>
        /// where eta is the fan total (overall) efficiency [0..1], rho the inlet air density [kg/m3] and cp the
        /// moist-air specific heat capacity at the inlet state [J/kgK] (SAM.Core.Mollier
        /// <c>Query.SpecificHeatCapacity_Air</c>, converted from kJ/kgK). Note the efficiency MULTIPLIES: a less
        /// efficient fan needs more input power - and therefore heats the air more - for the same pressure rise,
        /// so a given temperature pickup corresponds to a SMALLER pressure rise at lower efficiency.
        /// </remarks>
        /// <param name="fanProcess">Fan process carrying the inlet (Start) and outlet (End) states.</param>
        /// <param name="fanEfficiency">Fan total (overall) efficiency [0..1]; the value stored on
        /// <c>SystemFan.OverallEfficiency</c> so the pair round-trips through the SFP relation.</param>
        /// <returns>Fan total pressure rise [Pa], or <see cref="double.NaN"/> when the process, its states, the
        /// efficiency, the temperature rise (must be positive - a fan always heats the air) or the inlet density
        /// or specific heat capacity are unavailable.</returns>
        public static double FanPressureRise(this FanProcess fanProcess, double fanEfficiency = 0.7)
        {
            if (fanProcess == null || double.IsNaN(fanEfficiency) || fanEfficiency <= 0)
            {
                return double.NaN;
            }

            MollierPoint start = fanProcess.Start;
            MollierPoint end = fanProcess.End;

            if (start == null || end == null || !start.IsValid() || !end.IsValid())
            {
                return double.NaN;
            }

            double deltaT = end.DryBulbTemperature - start.DryBulbTemperature;
            if (deltaT <= 0)
            {
                return double.NaN;
            }

            double density = start[MollierPointProperty.Density];
            if (double.IsNaN(density))
            {
                return double.NaN;
            }

            // SAM.Core.Mollier returns cp in kJ/kgK; the pressure balance needs J/kgK.
            double specificHeatCapacity = 1000.0 * start.SpecificHeatCapacity_Air();
            if (double.IsNaN(specificHeatCapacity))
            {
                return double.NaN;
            }

            return fanEfficiency * density * specificHeatCapacity * deltaT;
        }
    }
}
