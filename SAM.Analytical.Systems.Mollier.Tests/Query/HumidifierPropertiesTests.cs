// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;

namespace SAM.Analytical.Systems.Mollier.Tests.Query
{
    /// <summary>
    /// Proves the humidifier derivations: setpoint is the end-state RELATIVE HUMIDITY [%] (the Tas TPD
    /// controlled variable) for both spray and steam, spray effectiveness/water flow follow the humidity-ratio
    /// balance, and steam duty is the enthalpy rise with no isothermality gate.
    /// </summary>
    public class HumidifierPropertiesTests
    {
        private const double Pressure = 101325.0;
        private const double Airflow = 2.0;

        [Fact]
        public void Spray_Setpoint_Is_EndRelativeHumidity_NotDryBulb()
        {
            // Adiabatic humidification from 30 C / 30 %RH adding 3 g/kg: the air cools along the wet-bulb line to
            // roughly 22-23 C and rises to roughly 60 %RH. The setpoint must be the RH, not the ~22 C dry-bulb.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(30, 30, Pressure);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.003);

            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            Assert.Equal(process.End.RelativeHumidity, setpoint, 9);
            Assert.InRange(setpoint, 50.0, 75.0);
            // Guards against the old dry-bulb semantics (~22 C would fall in neither band).
            Assert.True(setpoint > 40.0, "Setpoint must be a relative humidity [%], not a dry-bulb temperature [C]");
        }

        [Fact]
        public void Spray_Effectiveness_MatchesHandCalculation()
        {
            // effectiveness = (w_end - w_start) / (w_sat(end) - w_start), clamped to [0, 1].
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(30, 30, Pressure);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.003);

            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            double expected = (process.End.HumidityRatio - start.HumidityRatio)
                / (process.End.SaturationMollierPoint().HumidityRatio - start.HumidityRatio);

            Assert.Equal(expected, effectiveness, 9);
            Assert.InRange(effectiveness, 0.0, 1.0);
            // Roughly 0.003 / 0.0095 ~ 0.32 for this duty.
            Assert.InRange(effectiveness, 0.20, 0.45);
        }

        [Fact]
        public void Spray_WaterFlowCapacity_MatchesMassBalance()
        {
            // waterFlowCapacity = massFlow * (w_end - w_start): ~2.32 kg/s dry air * 0.003 kg/kg ~ 0.007 kg/s.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(30, 30, Pressure);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.003);

            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            double expected = process.MassFlow(Airflow) * (process.End.HumidityRatio - start.HumidityRatio);

            Assert.Equal(expected, waterFlowCapacity, 9);
            Assert.InRange(waterFlowCapacity, 0.005, 0.009);
            Assert.True(double.IsNaN(duty), "Adiabatic humidification has no steam duty");
        }

        [Fact]
        public void Spray_Component_Carries_Setpoint_Effectiveness_And_WaterFlow()
        {
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(30, 30, Pressure);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.003);
            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            SystemSprayHumidifier humidifier = Assert.IsType<SystemSprayHumidifier>(process.SystemComponent(Airflow));

            Assert.NotNull(humidifier.Setpoint);
            Assert.Equal(setpoint, humidifier.Setpoint.Value, 9);
            Assert.NotNull(humidifier.Effectiveness);
            Assert.Equal(effectiveness, humidifier.Effectiveness.Value, 9);
            SizableValue waterFlow = Assert.IsType<SizableValue>(humidifier.WaterFlowCapacity);
            Assert.NotNull(waterFlow.ModifiableValue);
            Assert.Equal(waterFlowCapacity, waterFlow.ModifiableValue.Value, 9);
        }

        [Fact]
        public void Steam_Duty_IsSet_ForLibraryBuiltProcess()
        {
            // SteamHumidificationProcess is NOT exactly isothermal (the injected steam carries sensible heat,
            // dT ~ +0.3 K), so the old |dT| < 0.01 gate left Duty permanently unset. Duty = |massFlow * dh|.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 30, Pressure);
            SteamHumidificationProcess process = start.SteamHumidificationProcess_ByHumidityRatioDifference(0.004);

            Assert.True(System.Math.Abs(process.End.DryBulbTemperature - start.DryBulbTemperature) > 0.01,
                "Library steam humidification is not exactly isothermal - the removed gate would have suppressed Duty");

            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            double expected = System.Math.Abs(process.MassFlow(Airflow) * (process.End.Enthalpy - start.Enthalpy));
            Assert.False(double.IsNaN(duty), "Steam humidifier duty must be set");
            Assert.Equal(expected, duty, 6);
            Assert.InRange(duty, 15000.0, 35000.0); // ~25 kW for 0.004 kg/kg at 2 m3/s
            Assert.True(double.IsNaN(effectiveness), "Steam humidification has no adiabatic effectiveness");
        }

        [Fact]
        public void Steam_Setpoint_Is_EndRelativeHumidity_And_Component_Carries_Duty()
        {
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 30, Pressure);
            SteamHumidificationProcess process = start.SteamHumidificationProcess_ByHumidityRatioDifference(0.004);
            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            Assert.Equal(process.End.RelativeHumidity, setpoint, 9);
            Assert.InRange(setpoint, 40.0, 75.0);

            SystemSteamHumidifier humidifier = Assert.IsType<SystemSteamHumidifier>(process.SystemComponent(Airflow));
            Assert.NotNull(humidifier.Setpoint);
            Assert.Equal(setpoint, humidifier.Setpoint.Value, 9);
            SizableValue dutyValue = Assert.IsType<SizableValue>(humidifier.Duty);
            Assert.NotNull(dutyValue.ModifiableValue);
            Assert.Equal(duty, dutyValue.ModifiableValue.Value, 6);
        }

        [Fact]
        public void NegativeHumidityChange_ClampsEffectivenessToZero()
        {
            // Dehumidification expressed as an adiabatic process: the effectiveness ratio goes negative and is
            // clamped to 0; the water "flow" stays negative (it is a mass balance, not a capacity).
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(30, 60, Pressure);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(-0.002);

            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            Assert.Equal(0.0, effectiveness);
            Assert.True(waterFlowCapacity < 0);
        }

        [Fact]
        public void SaturatedStart_ZeroDenominator_LeavesEffectivenessNaN()
        {
            // At saturation w_sat - w_start ~ 0, so the effectiveness ratio is undefined rather than infinite.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 100, Pressure);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.0);

            process.HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            Assert.False(double.IsNaN(setpoint));
            Assert.True(double.IsNaN(effectiveness) || effectiveness == 0.0);
        }

        [Fact]
        public void NaNAirflow_LeavesFlowsNaN_ButStillSetsSetpoint()
        {
            // The setpoint is a control value derived from the psychrometrics alone, so it survives an unknown
            // design airflow; the flow-dependent quantities do not.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(30, 30, Pressure);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.003);

            process.HumidifierProperties(double.NaN, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            Assert.False(double.IsNaN(setpoint));
            Assert.Equal(process.End.RelativeHumidity, setpoint, 9);
            Assert.True(double.IsNaN(waterFlowCapacity));
            Assert.True(double.IsNaN(duty));
        }

        [Fact]
        public void NullProcess_LeavesEverythingNaN()
        {
            ((HumidificationProcess)null).HumidifierProperties(Airflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity);

            Assert.True(double.IsNaN(setpoint));
            Assert.True(double.IsNaN(effectiveness));
            Assert.True(double.IsNaN(duty));
            Assert.True(double.IsNaN(waterFlowCapacity));
        }
    }
}
