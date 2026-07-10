// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System;
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Query
    {
        /// <summary>
        /// Derives humidifier properties from a Mollier humidification process psychrometrics.
        /// </summary>
        /// <remarks>
        /// For adiabatic (spray/evaporative) processes the humidification effectiveness is the achieved
        /// humidity-ratio rise relative to the maximum possible rise at the end-state dry-bulb temperature.
        /// Water flow capacity is the mass flow of water evaporated. For isothermal/steam processes the
        /// humidifier duty is the enthalpy rise, provided the start and end dry-bulb temperatures are within
        /// 0.01 C (true isothermal).
        /// </remarks>
        /// <param name="humidificationProcess">Psychrometric humidification process.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="setpoint">Off-coil/off-humidifier dry-bulb temperature setpoint [C].</param>
        /// <param name="effectiveness">Adiabatic humidification effectiveness [0..1], NaN for isothermal.</param>
        /// <param name="duty">Isothermal humidifier duty [W], NaN for adiabatic.</param>
        /// <param name="waterFlowCapacity">Water mass flow evaporated [kg/s], NaN for isothermal.</param>
        public static void HumidifierProperties(this HumidificationProcess humidificationProcess, double designAirflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity)
        {
            setpoint = double.NaN;
            effectiveness = double.NaN;
            duty = double.NaN;
            waterFlowCapacity = double.NaN;

            if (humidificationProcess == null)
            {
                return;
            }

            MollierPoint start = humidificationProcess.Start;
            MollierPoint end = humidificationProcess.End;
            if (start == null || end == null || !start.IsValid() || !end.IsValid())
            {
                return;
            }

            setpoint = end.DryBulbTemperature;

            double massFlow = humidificationProcess.MassFlow(designAirflow);
            if (double.IsNaN(massFlow))
            {
                return;
            }

            if (humidificationProcess is AdiabaticHumidificationProcess)
            {
                double w_start = start.HumidityRatio;
                double w_end = end.HumidityRatio;

                MollierPoint saturationPoint = end.SaturationMollierPoint();
                if (saturationPoint != null && saturationPoint.IsValid())
                {
                    double w_sat = saturationPoint.HumidityRatio;
                    double denominator = w_sat - w_start;
                        if (System.Math.Abs(denominator) > 1e-12)
                    {
                        effectiveness = (w_end - w_start) / denominator;
                        if (effectiveness < 0)
                        {
                            effectiveness = 0;
                        }
                        if (effectiveness > 1)
                        {
                            effectiveness = 1;
                        }
                    }
                }

                waterFlowCapacity = massFlow * (w_end - w_start);
            }
            else
            {
                if (System.Math.Abs(end.DryBulbTemperature - start.DryBulbTemperature) < 0.01)
                {
                    duty = System.Math.Abs(massFlow * (end.Enthalpy - start.Enthalpy));
                }
            }
        }
    }
}
