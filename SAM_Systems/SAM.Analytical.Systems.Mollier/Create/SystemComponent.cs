// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Core;
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
            List<ConversionDiagnostic> _;
            return SystemComponent(mollierProcess, designAirflow, out _);
        }

        public static ISystemComponent SystemComponent(this IMollierProcess mollierProcess, double designAirflow, out List<ConversionDiagnostic> diagnostics)
        {
            diagnostics = new List<ConversionDiagnostic>();

            if (mollierProcess == null)
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.NullProcess, "Process is null; no system component created.", null));
                return null;
            }

            if (double.IsNaN(designAirflow))
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.AirflowNaN, "Design airflow is NaN; duties will not be set.", mollierProcess));
            }

            if (mollierProcess is UndefinedProcess || mollierProcess is SpecificProcess)
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.UnsupportedProcess, $"Process type '{mollierProcess.GetType().Name}' is not supported for system component conversion.", mollierProcess));
                return null;
            }

            MollierPoint end = mollierProcess.End;
            bool hasEnd = end != null && end.IsValid();

            MollierPoint start = mollierProcess.Start;
            bool hasStart = start != null && start.IsValid();

            if (!hasStart || !hasEnd)
            {
                string invalidState = !hasStart && !hasEnd ? "start and end" : (!hasStart ? "start" : "end");
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.InvalidProcessState, $"Process {invalidState} state is null or invalid; derived setpoints and duties will not be set.", mollierProcess));
            }

            // FanProcess derives from HeatingProcess, so it must be tested first.
            if (mollierProcess is FanProcess fanProcess)
            {
                SystemFan systemFan = new SystemFan("Fan");

                const double fanEfficiency = 0.7;
                double pressure = fanProcess.FanPressureRise(fanEfficiency);
                if (!double.IsNaN(pressure))
                {
                    systemFan.Pressure = pressure;
                    systemFan.OverallEfficiency = fanEfficiency;
                }

                if (!double.IsNaN(designAirflow))
                {
                    // Tas TPD fan flow values are litres per second (SFP itself is W/(l/s); TPD template code
                    // uses values like DesignFlowRate.Value = 150), so convert from the bridge's m3/s.
                    systemFan.DesignFlowRate = new SizedFlowValue(designAirflow * 1000, double.NaN);
                    systemFan.DesignFlowType = FlowRateType.Value;
                }

                return systemFan;
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
                else
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Info, DiagnosticCodes.ApparatusDewPointNotAvailable, "Apparatus dew point not available; MinimumOffcoil not set.", mollierProcess));
                }

                double bypassFactor = coolingProcess.BypassFactor();
                if (!double.IsNaN(bypassFactor))
                {
                    systemCoolingCoil.BypassFactor = bypassFactor;
                }
                else
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Info, DiagnosticCodes.BypassFactorInvalid, "Bypass factor is NaN; not set.", mollierProcess));
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
                if (hasStart && hasEnd
                    && System.Math.Abs(start.HumidityRatio - end.HumidityRatio) > 1e-6)
                {
                    systemExchanger.ExchangerLatentType = ExchangerLatentType.HumidityRatio;
                }

                return systemExchanger;
            }

            if (mollierProcess is HumidificationProcess humidificationProcess)
            {
                humidificationProcess.HumidifierProperties(designAirflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

                if (humidificationProcess is AdiabaticHumidificationProcess)
                {
                    SystemSprayHumidifier systemSprayHumidifier = new SystemSprayHumidifier("Humidifier");
                    if (!double.IsNaN(setpoint))
                    {
                        systemSprayHumidifier.Setpoint = setpoint;
                    }
                    if (!double.IsNaN(effectiveness))
                    {
                        systemSprayHumidifier.Effectiveness = effectiveness;
                    }
                    if (!double.IsNaN(waterFlowCapacity))
                    {
                        systemSprayHumidifier.WaterFlowCapacity = new SizableValue(waterFlowCapacity);
                    }
                    return systemSprayHumidifier;
                }

                SystemSteamHumidifier systemSteamHumidifier = new SystemSteamHumidifier("Humidifier");
                if (!double.IsNaN(setpoint))
                {
                    systemSteamHumidifier.Setpoint = setpoint;
                }
                if (!double.IsNaN(duty))
                {
                    systemSteamHumidifier.Duty = new SizableValue(duty);
                }
                return systemSteamHumidifier;
            }

            if (mollierProcess is MixingProcess)
            {
                return new SystemAirJunction("Mixing");
            }

            diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.UnsupportedProcess, $"Process type '{mollierProcess.GetType().Name}' is not supported for system component conversion.", mollierProcess));
            return null;
        }
    }
}
