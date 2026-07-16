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
        /// The setpoint is the end-state RELATIVE HUMIDITY in percent for both spray (adiabatic) and steam
        /// humidifiers: SAM humidifier setpoints pass straight through to Tas TPD, whose humidifiers control
        /// downstream relative humidity under their default flags (see SAM_Tas
        /// <c>Convert.ToTPD(DisplaySystemSprayHumidifier ...)</c> and the TPD template code, e.g.
        /// <c>sprayHumidifier.Setpoint.Value = 90</c>).
        /// For adiabatic processes the humidification effectiveness is the achieved humidity-ratio rise relative
        /// to the maximum possible rise at the end-state dry-bulb temperature, and the water flow capacity is the
        /// mass flow of water evaporated. For steam processes the duty is the enthalpy rise; library-built steam
        /// processes are near-isothermal but not exactly so (the injected steam carries sensible heat), so no
        /// isothermality gate is applied.
        /// </remarks>
        /// <param name="humidificationProcess">Psychrometric humidification process.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="setpoint">Off-humidifier (end-state) relative humidity setpoint [%, 0..100].</param>
        /// <param name="effectiveness">Adiabatic humidification effectiveness [0..1], NaN for steam.</param>
        /// <param name="duty">Steam humidifier duty [W], NaN for adiabatic.</param>
        /// <param name="waterFlowCapacity">Water mass flow evaporated [kg/s], NaN for steam.</param>
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

            // Tas TPD humidifier setpoints are relative humidity [%] under the default (RH) control flags.
            setpoint = end.RelativeHumidity;

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
                // Steam humidification: duty is the full enthalpy rise. Library-built steam processes raise the
                // dry-bulb slightly (the steam's sensible heat), so an isothermality gate would never fire and
                // would leave the duty unset - the gate was removed deliberately.
                duty = System.Math.Abs(massFlow * (end.Enthalpy - start.Enthalpy));
            }
        }
    }
}
