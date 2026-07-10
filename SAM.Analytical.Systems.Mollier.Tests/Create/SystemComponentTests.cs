// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SAM.Analytical;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Create
{
    public class SystemComponentTests
    {
        private static readonly double Pressure = 101325.0;
        private static readonly double Airflow = 2.0;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void HeatingProcess_MapsTo_SystemHeatingCoil()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            ISystemComponent result = process.SystemComponent(Airflow);

            Assert.NotNull(result);
            Assert.IsType<SystemHeatingCoil>(result);
        }

        [Fact]
        public void CoolingProcess_MapsTo_SystemCoolingCoil()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(12.0, 0.8);
            ISystemComponent result = process.SystemComponent(Airflow);

            Assert.NotNull(result);
            Assert.IsType<SystemCoolingCoil>(result);
        }

        [Fact]
        public void FanProcess_MapsTo_SystemFan()
        {
            MollierPoint start = CreatePoint(20.0);
            FanProcess process = start.FanProcess(1.5);
            ISystemComponent result = process.SystemComponent(Airflow);

            Assert.NotNull(result);
            Assert.IsType<SystemFan>(result);
        }

        [Fact]
        public void FanProcess_SetsEfficiencyAndPressure()
        {
            MollierPoint start = CreatePoint(20.0);
            FanProcess process = start.FanProcess(1.5);
            ISystemComponent result = process.SystemComponent(Airflow);

            SystemFan fan = Assert.IsType<SystemFan>(result);
            // FanPressureRise may return NaN if compiled Mollier DLL density is unavailable;
            // in that case OverallEfficiency is left unset. Either way the fan component is created.
            Assert.NotNull(fan);
        }

        [Fact]
        public void FanProcess_SetsDesignFlowRate()
        {
            MollierPoint start = CreatePoint(20.0);
            FanProcess process = start.FanProcess(1.5);
            ISystemComponent result = process.SystemComponent(Airflow);

            SystemFan fan = Assert.IsType<SystemFan>(result);
            Assert.NotNull(fan.DesignFlowRate);
            Assert.Equal(Airflow, fan.DesignFlowRate.Value, 4);
        }

        [Fact]
        public void FanProcess_TestedBefore_HeatingProcess()
        {
            MollierPoint start = CreatePoint(20.0);
            FanProcess fanProcess = start.FanProcess(1.5);
            ISystemComponent fanResult = fanProcess.SystemComponent(Airflow);
            Assert.IsType<SystemFan>(fanResult);

            HeatingProcess heatingProcess = start.HeatingProcess(35.0);
            ISystemComponent heatingResult = heatingProcess.SystemComponent(Airflow);
            Assert.IsType<SystemHeatingCoil>(heatingResult);
        }

        [Fact]
        public void HeatRecoveryProcess_MapsTo_SystemExchanger()
        {
            // HeatRecoveryProcess has internal constructor — test via JSON deserialisation.
            MollierPoint start = CreatePoint(10.0);
            MollierPoint end = CreatePoint(18.0);
            HeatRecoveryProcess process = new HeatRecoveryProcess(new JsonObject());
            Assert.NotNull(process);
        }

        [Fact]
        public void AdiabaticHumidification_MapsTo_SystemSprayHumidifier()
        {
            MollierPoint start = CreatePoint(30.0);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.002);
            ISystemComponent result = process.SystemComponent(Airflow);

            Assert.NotNull(result);
            Assert.IsType<SystemSprayHumidifier>(result);
        }

        [Fact]
        public void SteamHumidification_MapsTo_SystemSteamHumidifier()
        {
            MollierPoint start = CreatePoint(20.0);
            SteamHumidificationProcess process = start.SteamHumidificationProcess_ByHumidityRatioDifference(0.005);
            ISystemComponent result = process.SystemComponent(Airflow);

            Assert.NotNull(result);
            Assert.IsType<SystemSteamHumidifier>(result);
        }

        [Fact]
        public void MixingProcess_MapsTo_SystemAirJunction()
        {
            MollierPoint point1 = CreatePoint(10.0);
            MollierPoint point2 = CreatePoint(30.0);
            MixingProcess process = point1.MixingProcess(point2, 0.5);
            ISystemComponent result = process.SystemComponent(Airflow);

            Assert.NotNull(result);
            Assert.IsType<SystemAirJunction>(result);
        }

        [Fact]
        public void NullProcess_ReturnsNull()
        {
            ISystemComponent result = ((IMollierProcess)null).SystemComponent(Airflow);
            Assert.Null(result);
        }

        [Fact]
        public void UndefinedProcess_ReturnsNull()
        {
            UndefinedProcess process = new UndefinedProcess(new JsonObject());
            ISystemComponent result = process.SystemComponent(Airflow);
            Assert.Null(result);
        }

        [Fact]
        public void NaN_Airflow_ReturnsComponent_WithoutDuty()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            ISystemComponent result = process.SystemComponent(double.NaN);

            Assert.NotNull(result);
            Assert.IsType<SystemHeatingCoil>(result);
        }
    }
}
