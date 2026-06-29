// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        /// <summary>
        /// Creates the SAM_Systems air-handling component that realises a single Mollier process.
        /// </summary>
        /// <remarks>
        /// The process end-state and the design airflow are used to derive component duties and control
        /// values: off-coil setpoint from the process End dry-bulb temperature, cooling bypass factor from
        /// <see cref="CoolingProcess.Efficiency"/>/Apparatus Dew Point, and duty from the enthalpy change
        /// scaled by the design mass flow. Returns null for processes that map to no component
        /// (<see cref="UndefinedProcess"/>, <see cref="SpecificProcess"/>).
        /// </remarks>
        /// <param name="mollierProcess">Psychrometric process.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s]. Pass double.NaN to leave duties unset.</param>
        /// <returns>An <see cref="ISystemComponent"/>, or null.</returns>
        public static ISystemComponent SystemComponent(this IMollierProcess mollierProcess, double designAirflow = double.NaN)
        {
            if (mollierProcess == null)
            {
                return null;
            }

            if (mollierProcess is UndefinedProcess || mollierProcess is SpecificProcess)
            {
                return null;
            }

            MollierPoint end = mollierProcess.End;
            bool hasEnd = end != null && end.IsValid();

            // FanProcess derives from HeatingProcess, so it must be tested first.
            if (mollierProcess is FanProcess)
            {
                // Fan duty is governed by pressure rise and efficiency; airflow is carried by the air system.
                return new SystemFan("Fan");
            }

            if (mollierProcess is HeatingProcess)
            {
                SystemHeatingCoil systemHeatingCoil = new SystemHeatingCoil("Heating Coil");
                if (hasEnd)
                {
                    systemHeatingCoil.Setpoint = end.DryBulbTemperature;
                }

                double duty = mollierProcess.Duty(designAirflow);
                if (!double.IsNaN(duty))
                {
                    systemHeatingCoil.Duty = new SizableValue(duty);
                }

                return systemHeatingCoil;
            }

            if (mollierProcess is CoolingProcess coolingProcess)
            {
                SystemCoolingCoil systemCoolingCoil = new SystemCoolingCoil("Cooling Coil");
                if (hasEnd)
                {
                    systemCoolingCoil.Setpoint = end.DryBulbTemperature;
                }

                // The air cannot leave the coil colder than the coil surface (~the Apparatus Dew Point),
                // so the ADP dry-bulb temperature is a physical floor on the off-coil temperature.
                MollierPoint apparatusDewPoint = coolingProcess.ApparatusDewPoint();
                if (apparatusDewPoint != null && apparatusDewPoint.IsValid())
                {
                    systemCoolingCoil.MinimumOffcoil = apparatusDewPoint.DryBulbTemperature;
                }

                double bypassFactor = coolingProcess.BypassFactor();
                if (!double.IsNaN(bypassFactor))
                {
                    systemCoolingCoil.BypassFactor = bypassFactor;
                }

                double duty = coolingProcess.Duty(designAirflow);
                if (!double.IsNaN(duty))
                {
                    systemCoolingCoil.Duty = new SizableValue(duty);
                }

                return systemCoolingCoil;
            }

            if (mollierProcess is HeatRecoveryProcess)
            {
                SystemExchanger systemExchanger = new SystemExchanger("Heat Recovery");

                // Effectiveness-based exchanger: the SensibleEfficiency/LatentEfficiency fields are used directly.
                // Efficiencies need both air paths, so they are set later by Create.SystemPlantRoom(supply, extract)
                // when an extract chain is available (see Query.HeatRecoveryEfficiencies).
                systemExchanger.ExchangerCalculationMethod = ExchangerCalculationMethod.Simple;
                systemExchanger.ExchangerType = ExchangerType.Simple;

                if (hasEnd)
                {
                    systemExchanger.Setpoint = end.DryBulbTemperature;
                }

                // Twin-wheel / latent recovery: a humidity-ratio shift across the process implies
                // moisture transfer, so flag the exchanger as latent-capable.
                MollierPoint start = mollierProcess.Start;
                if (start != null && start.IsValid() && hasEnd
                    && System.Math.Abs(start.HumidityRatio - end.HumidityRatio) > 1e-6)
                {
                    systemExchanger.ExchangerLatentType = ExchangerLatentType.HumidityRatio;
                }

                return systemExchanger;
            }

            if (mollierProcess is HumidificationProcess)
            {
                // SystemHumidifier is abstract, so emit the concrete humidifier whose physics (and drawing
                // symbol) matches the process: adiabatic (constant-enthalpy) humidification is spray/evaporative;
                // isothermal (constant-temperature, incl. steam) humidification is a steam humidifier.
                if (mollierProcess is AdiabaticHumidificationProcess)
                {
                    return new SystemSprayHumidifier("Humidifier");
                }

                return new SystemSteamHumidifier("Humidifier");
            }

            if (mollierProcess is MixingProcess)
            {
                return new SystemAirJunction("Mixing");
            }

            return null;
        }
    }
}
