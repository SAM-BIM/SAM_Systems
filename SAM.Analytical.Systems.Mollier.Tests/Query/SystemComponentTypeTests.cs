using System;
using System.Text.Json.Nodes;
using SAM.Analytical;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using Xunit;

namespace SAM.Analytical.Systems.Mollier.Tests.Query
{
    public class SystemComponentTypeTests
    {
        private static readonly double Pressure = 101325.0;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void HeatingProcess_Returns_SystemHeatingCoilType()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            Type type = process.SystemComponentType();

            Assert.NotNull(type);
            Assert.Equal(typeof(SystemHeatingCoil), type);
        }

        [Fact]
        public void CoolingProcess_Returns_SystemCoolingCoilType()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(12.0, 0.8);
            Type type = process.SystemComponentType();

            Assert.NotNull(type);
            Assert.Equal(typeof(SystemCoolingCoil), type);
        }

        [Fact]
        public void FanProcess_Returns_SystemFanType()
        {
            MollierPoint start = CreatePoint(20.0);
            FanProcess process = start.FanProcess(1.5);
            Type type = process.SystemComponentType();

            Assert.NotNull(type);
            Assert.Equal(typeof(SystemFan), type);
        }

        [Fact]
        public void HeatRecoveryProcess_Returns_SystemExchangerType()
        {
            HeatRecoveryProcess process = new HeatRecoveryProcess(new JsonObject());
            // Internal constructor prevents valid process creation; type still resolves.
            Type type = process.SystemComponentType();

            Assert.NotNull(type);
            Assert.Equal(typeof(SystemExchanger), type);
        }

        [Fact]
        public void AdiabaticHumidification_Returns_SprayHumidifierType()
        {
            MollierPoint start = CreatePoint(30.0);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.002);
            Type type = process.SystemComponentType();

            Assert.NotNull(type);
            Assert.Equal(typeof(SystemSprayHumidifier), type);
        }

        [Fact]
        public void SteamHumidification_Returns_SteamHumidifierType()
        {
            MollierPoint start = CreatePoint(20.0);
            SteamHumidificationProcess process = start.SteamHumidificationProcess_ByHumidityRatioDifference(0.005);
            Type type = process.SystemComponentType();

            Assert.NotNull(type);
            Assert.Equal(typeof(SystemSteamHumidifier), type);
        }

        [Fact]
        public void MixingProcess_Returns_SystemAirJunctionType()
        {
            MollierPoint point1 = CreatePoint(10.0);
            MollierPoint point2 = CreatePoint(30.0);
            MixingProcess process = point1.MixingProcess(point2, 0.5);
            Type type = process.SystemComponentType();

            Assert.NotNull(type);
            Assert.Equal(typeof(SystemAirJunction), type);
        }

        [Fact]
        public void NullProcess_ReturnsNull()
        {
            Type type = ((IMollierProcess)null).SystemComponentType();
            Assert.Null(type);
        }

        [Fact]
        public void UndefinedProcess_ReturnsNull()
        {
            UndefinedProcess process = new UndefinedProcess(new JsonObject());
            Type type = process.SystemComponentType();
            Assert.Null(type);
        }
    }
}
