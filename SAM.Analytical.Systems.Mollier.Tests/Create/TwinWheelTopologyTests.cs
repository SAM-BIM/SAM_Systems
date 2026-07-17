// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Create
{
    /// <summary>
    /// Proves the twin-wheel topology built by Create.SystemPlantRoom(supply, extract, ...): a single shared
    /// SystemExchanger carries both air paths (confirmed by direct probe: connectors 0/2 are the two In sides,
    /// 1/3 the two Out sides, and all four are connected), both fans get a real pressure rise from the example's
    /// SpecificFanPower, and the whole thing survives a JSON round-trip.
    /// </summary>
    public class TwinWheelTopologyTests
    {
        private const double FanEfficiency = 0.7;

        private static SystemPlantRoom BuildPlantRoom()
        {
            TwinWheelExample.MollierProcesses(out List<IMollierProcess> supply, out List<IMollierProcess> extract);
            return Mollier.Create.SystemPlantRoom(supply, extract, TwinWheelExample.DefaultSupplyAirflow, TwinWheelExample.DefaultExtractAirflow, "PR");
        }

        [Fact]
        public void TwinWheel_HasExactlyOne_AirSystem()
        {
            SystemPlantRoom plantRoom = BuildPlantRoom();
            Assert.Single(plantRoom.GetSystems().OfType<AirSystem>());
        }

        [Fact]
        public void TwinWheel_HasExactlyOne_SharedExchanger()
        {
            SystemPlantRoom plantRoom = BuildPlantRoom();

            // Proves no accidental duplicate despite BOTH chains carrying a HeatRecoveryProcess: the extract
            // chain's heat-recovery process reuses the supply-side exchanger instance instead of creating a
            // second physical device.
            Assert.Single(plantRoom.GetSystemComponents<SystemExchanger>());
        }

        [Fact]
        public void TwinWheel_Exchanger_BothAirPaths_AreConnected()
        {
            SystemPlantRoom plantRoom = BuildPlantRoom();
            SystemExchanger exchanger = plantRoom.GetSystemComponents<SystemExchanger>().Single();
            SystemType airType = new SystemType(typeof(AirSystem));

            // Both air paths fully wired: all four air connectors connected (path 1 = In 0/Out 1 to the fresh-air
            // junction and cooling coil; path 2 = In 2/Out 3 to the room-side junction and the extract fan).
            List<int> connected = plantRoom.Indexes(exchanger, airType, ConnectorStatus.Connected, null);
            Assert.Contains(0, connected);
            Assert.Contains(1, connected);
            Assert.Contains(2, connected);
            Assert.Contains(3, connected);

            List<int> unconnected = plantRoom.Indexes(exchanger, airType, ConnectorStatus.Unconnected, null);
            Assert.DoesNotContain(0, unconnected);
            Assert.DoesNotContain(1, unconnected);
            Assert.DoesNotContain(2, unconnected);
            Assert.DoesNotContain(3, unconnected);

            // Direction split confirms which indexes are In vs Out on each path (confirmed by direct probe).
            List<int> connectedIn = plantRoom.Indexes(exchanger, airType, ConnectorStatus.Connected, SAM.Core.Direction.In);
            List<int> connectedOut = plantRoom.Indexes(exchanger, airType, ConnectorStatus.Connected, SAM.Core.Direction.Out);
            Assert.Equal(new List<int> { 0, 2 }, connectedIn.OrderBy(x => x).ToList());
            Assert.Equal(new List<int> { 1, 3 }, connectedOut.OrderBy(x => x).ToList());
        }

        [Fact]
        public void TwinWheel_Exchanger_HasSensibleAndLatentEfficiency()
        {
            SystemPlantRoom plantRoom = BuildPlantRoom();
            SystemExchanger exchanger = plantRoom.GetSystemComponents<SystemExchanger>().Single();

            Assert.Equal(0.75, exchanger.SensibleEfficiency.Value, 6);
            Assert.Equal(0.65, exchanger.LatentEfficiency.Value, 6);
            Assert.Equal(ExchangerLatentType.HumidityRatio, exchanger.ExchangerLatentType);
        }

        [Fact]
        public void TwinWheel_Fans_HavePressureAndEfficiency()
        {
            SystemPlantRoom plantRoom = BuildPlantRoom();
            List<SystemFan> fans = plantRoom.GetSystemComponents<SystemFan>();
            Assert.Equal(2, fans.Count);

            // Both fans are named "Fan"; distinguish supply from extract by their design flow rate (l/s), which
            // is independent ground truth tied to TwinWheelExample.DefaultSupplyAirflow/DefaultExtractAirflow.
            SystemFan supplyFan = fans.Single(f => System.Math.Abs(f.DesignFlowRate.Value - 2500.0) < 0.5);
            SystemFan extractFan = fans.Single(f => System.Math.Abs(f.DesignFlowRate.Value - 2300.0) < 0.5);

            double expectedPressure = FanEfficiency * TwinWheelExample.SpecificFanPower * 1000.0; // eta * SFP * 1000 = 560 Pa

            Assert.Equal(expectedPressure, supplyFan.Pressure, 6);
            Assert.Equal(FanEfficiency, supplyFan.OverallEfficiency.Value, 6);
            Assert.Equal(2500.0, supplyFan.DesignFlowRate.Value, 6);

            Assert.Equal(expectedPressure, extractFan.Pressure, 6);
            Assert.Equal(FanEfficiency, extractFan.OverallEfficiency.Value, 6);
            Assert.Equal(2300.0, extractFan.DesignFlowRate.Value, 6);
        }

        [Fact]
        public void TwinWheel_Exchanger_SurvivesJsonRoundTrip()
        {
            SystemEnergyCentre energyCentre = TwinWheelExample.Create();
            JsonObject json = energyCentre.ToJsonObject();
            SystemEnergyCentre roundTripped = new SystemEnergyCentre(json);

            SystemPlantRoom plantRoom = roundTripped.GetSystemPlantRooms().Single();
            SystemExchanger exchanger = plantRoom.GetSystemComponents<SystemExchanger>().Single();

            Assert.Equal(0.75, exchanger.SensibleEfficiency.Value, 6);
            Assert.Equal(0.65, exchanger.LatentEfficiency.Value, 6);
        }
    }
}
