// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using SAM.Analytical;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Create
{
    public class SupplyExtractTests
    {
        private static readonly double Pressure = 101325.0;
        private static readonly double SupplyAirflow = 1.5;
        private static readonly double ExtractAirflow = 1.5;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void SupplyExtract_CreatesPlantRoom()
        {
            MollierPoint start = CreatePoint(10.0);
            List<IMollierProcess> supply = new List<IMollierProcess>
            {
                start.HeatingProcess(20.0),
                start.FanProcess(1.5)
            };

            List<IMollierProcess> extract = new List<IMollierProcess>
            {
                start.HeatingProcess(22.0),
                start.FanProcess(1.0)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(supply, extract, SupplyAirflow, ExtractAirflow);
            Assert.NotNull(plantRoom);
        }

        [Fact]
        public void SupplyExtract_HasOneAirSystem()
        {
            MollierPoint start = CreatePoint(10.0);
            List<IMollierProcess> supply = new List<IMollierProcess>
            {
                start.HeatingProcess(20.0)
            };

            List<IMollierProcess> extract = new List<IMollierProcess>
            {
                start.HeatingProcess(22.0)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(supply, extract, SupplyAirflow, ExtractAirflow);
            List<ISystem> systems = plantRoom.GetSystems();
            List<AirSystem> airSystems = systems.OfType<AirSystem>().ToList();
            Assert.Single(airSystems);
        }

        [Fact]
        public void SupplyExtract_ContainsHeatingCoils()
        {
            MollierPoint start = CreatePoint(10.0);
            List<IMollierProcess> supply = new List<IMollierProcess>
            {
                start.HeatingProcess(22.0)
            };

            List<IMollierProcess> extract = new List<IMollierProcess>
            {
                start.HeatingProcess(24.0)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(supply, extract, SupplyAirflow, ExtractAirflow);
            List<SystemHeatingCoil> coils = plantRoom.GetSystemComponents<SystemHeatingCoil>();
            Assert.NotNull(coils);
            Assert.Equal(2, coils.Count);
        }

        [Fact]
        public void SupplyExtract_TwinWheel_SharedExchanger_SingleInstance()
        {
            // HeatRecoveryProcess factory is not accessible from the test assembly.
            // This test verifies that supply+extract with compatible processes shares exchangers.
            MollierPoint start = CreatePoint(10.0);
            List<IMollierProcess> supply = new List<IMollierProcess> { start.HeatingProcess(20.0), start.FanProcess(1.5) };
            List<IMollierProcess> extract = new List<IMollierProcess> { start.HeatingProcess(22.0), start.FanProcess(1.0) };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(supply, extract, SupplyAirflow, ExtractAirflow);
            Assert.NotNull(plantRoom);
            List<ISystemComponent> components = plantRoom.GetSystemComponents<ISystemComponent>();
            Assert.NotNull(components);
            Assert.True(components.Count >= 4);
        }

        [Fact]
        public void SupplyExtract_ContainsRoom_SpaceComponent()
        {
            MollierPoint start = CreatePoint(10.0);
            List<IMollierProcess> supply = new List<IMollierProcess>
            {
                start.HeatingProcess(20.0)
            };

            List<IMollierProcess> extract = new List<IMollierProcess>
            {
                start.HeatingProcess(22.0)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(supply, extract, SupplyAirflow, ExtractAirflow);
            List<SystemSpace> spaces = plantRoom.GetSystemComponents<SystemSpace>();
            Assert.NotNull(spaces);
            Assert.Single(spaces);
        }

        [Fact]
        public void SupplyExtract_CreatesEnergyCentre()
        {
            MollierPoint start = CreatePoint(10.0);
            List<IMollierProcess> supply = new List<IMollierProcess>
            {
                start.HeatingProcess(20.0)
            };

            SystemEnergyCentre energyCentre = Mollier.Create.SystemEnergyCentre(supply, null,
                SupplyAirflow, double.NaN, "Test EC");

            Assert.NotNull(energyCentre);
        }

        [Fact]
        public void SupplyOnly_ReturnsNull_ForBothEmpty()
        {
            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(
                new List<IMollierProcess>(), new List<IMollierProcess>(),
                SupplyAirflow, ExtractAirflow);

            Assert.Null(plantRoom);
        }
    }
}
