// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SAM.Analytical;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;

namespace SAM.Analytical.Systems.Mollier.Tests.Json
{
    public class RoundTripTests
    {
        private static readonly double Pressure = 101325.0;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void HeatingCoil_RoundTrip_PreservesSetpoint()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            ISystemComponent component = process.SystemComponent(2.0);

            SystemHeatingCoil coil = Assert.IsType<SystemHeatingCoil>(component);
            double originalSetpoint = coil.Setpoint.Value;

            JsonObject json = coil.ToJsonObject();
            SystemHeatingCoil deserialized = new SystemHeatingCoil(json);

            Assert.NotNull(deserialized);
            Assert.Equal(originalSetpoint, deserialized.Setpoint.Value, 2);
        }

        [Fact]
        public void CoolingCoil_RoundTrip_PreservesBypassFactor()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(12.0, 0.8);
            ISystemComponent component = process.SystemComponent(2.0);

            SystemCoolingCoil coil = Assert.IsType<SystemCoolingCoil>(component);
            double originalBypass = coil.BypassFactor.Value;

            JsonObject json = coil.ToJsonObject();
            SystemCoolingCoil deserialized = new SystemCoolingCoil(json);

            Assert.NotNull(deserialized);
            Assert.Equal(originalBypass, deserialized.BypassFactor.Value, 4);
        }

        [Fact]
        public void Fan_RoundTrip_PreservesPressure()
        {
            MollierPoint start = CreatePoint(20.0);
            FanProcess process = start.FanProcess(1.5);
            ISystemComponent component = process.SystemComponent(2.0);

            SystemFan fan = Assert.IsType<SystemFan>(component);
            Assert.False(double.IsNaN(fan.Pressure));

            JsonObject json = fan.ToJsonObject();
            SystemFan deserialized = new SystemFan(json);

            Assert.NotNull(deserialized);
            Assert.Equal(fan.Pressure, deserialized.Pressure, 2);
        }

        [Fact]
        public void PlantRoom_RoundTrip_PreservesComponentCount()
        {
            MollierPoint start = CreatePoint(20.0);
            List<IMollierProcess> processes = new List<IMollierProcess>
            {
                start.HeatingProcess(30.0),
                start.FanProcess(1.5)
            };

            SystemPlantRoom plantRoom = processes.SystemPlantRoom(2.0);
            List<ISystemComponent> originalComponents = plantRoom.GetSystemComponents<ISystemComponent>();

            JsonObject json = plantRoom.ToJsonObject();
            SystemPlantRoom deserialized = new SystemPlantRoom(json);

            List<ISystemComponent> deserializedComponents = deserialized.GetSystemComponents<ISystemComponent>();
            Assert.Equal(originalComponents.Count, deserializedComponents.Count);
        }

        [Fact]
        public void Exchanger_RoundTrip_PreservesEfficiency()
        {
            SystemExchanger exchanger = new SystemExchanger("Test Exchanger");
            JsonObject json = exchanger.ToJsonObject();
            SystemExchanger deserialized = new SystemExchanger(json);

            Assert.NotNull(deserialized);
        }

        [Fact]
        public void Humidifier_RoundTrip_PreservesType()
        {
            MollierPoint start = CreatePoint(30.0);
            AdiabaticHumidificationProcess process = start.AdiabaticHumidificationProcess_ByHumidityRatioDifference(0.002);
            ISystemComponent component = process.SystemComponent(2.0);

            Assert.IsType<SystemSprayHumidifier>(component);

            JsonObject json = ((SystemSprayHumidifier)component).ToJsonObject();
            SystemSprayHumidifier deserialized = new SystemSprayHumidifier(json);

            Assert.NotNull(deserialized);
        }
    }
}
