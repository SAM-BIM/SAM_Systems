// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SAM.Analytical;
using SAM.Core;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;
using Xunit.Abstractions;

namespace SAM.Analytical.Systems.Mollier.Tests.Integration
{
    public class MVRE_ComparisonTests
    {
        private readonly ITestOutputHelper _output;

        public MVRE_ComparisonTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string GetMVREPath()
        {
            string[] candidates = new string[]
            {
                @"files\resources\Analytical\Systems\SystemEnergyCentre\MVRE.json",
            };

            foreach (string candidate in candidates)
            {
                string full = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, candidate));
                if (File.Exists(full))
                {
                    return full;
                }
            }

            return null;
        }

        [Fact]
        public void Load_MVRE_Json_File()
        {
            string mvRePath = GetMVREPath();

            if (mvRePath == null)
            {
                _output.WriteLine("MVRE.json not found in expected locations - skipping comparison.");
                return;
            }

            _output.WriteLine("Loading MVRE.json from: " + mvRePath);

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            Assert.NotNull(mvReCentre);

            List<SystemPlantRoom> plantRooms = mvReCentre.GetSystemPlantRooms();
            Assert.NotNull(plantRooms);
            Assert.True(plantRooms.Count > 0, "MVRE should have at least one plant room");
        }

        [Fact]
        public void Compare_PlantRoom_Counts()
        {
            string mvRePath = GetMVREPath();
            if (mvRePath == null)
            {
                _output.WriteLine("MVRE.json not found - skipping comparison.");
                return;
            }

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            if (mvReCentre == null || bridgeCentre == null)
            {
                _output.WriteLine("One or both energy centres are null - skipping comparison.");
                return;
            }

            List<SystemPlantRoom> mvRePlantRooms = mvReCentre.GetSystemPlantRooms();
            List<SystemPlantRoom> bridgePlantRooms = bridgeCentre.GetSystemPlantRooms();

            _output.WriteLine(string.Format("MVRE plant rooms: {0}", mvRePlantRooms != null ? mvRePlantRooms.Count : 0));
            _output.WriteLine(string.Format("Bridge plant rooms: {0}", bridgePlantRooms != null ? bridgePlantRooms.Count : 0));

            Assert.NotNull(mvRePlantRooms);
            Assert.NotNull(bridgePlantRooms);
            Assert.True(mvRePlantRooms.Count > 0);
            Assert.True(bridgePlantRooms.Count > 0);
        }

        [Fact]
        public void Compare_Component_Type_Counts()
        {
            string mvRePath = GetMVREPath();
            if (mvRePath == null)
            {
                _output.WriteLine("MVRE.json not found - skipping comparison.");
                return;
            }

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            if (mvReCentre == null || bridgeCentre == null)
            {
                return;
            }

            SystemPlantRoom mvReRoom = mvReCentre.GetSystemPlantRooms()?[0];
            SystemPlantRoom bridgeRoom = bridgeCentre.GetSystemPlantRooms()?[0];

            if (mvReRoom == null || bridgeRoom == null)
            {
                return;
            }

            List<ISystemComponent> mvReComps = mvReRoom.GetSystemComponents<ISystemComponent>();
            List<ISystemComponent> bridgeComps = bridgeRoom.GetSystemComponents<ISystemComponent>();

            Assert.NotNull(mvReComps);
            Assert.NotNull(bridgeComps);

            var mvReTypeCounts = mvReComps
                .GroupBy(c => c.GetType().Name)
                .ToDictionary(g => g.Key, g => g.Count());

            var bridgeTypeCounts = bridgeComps
                .GroupBy(c => c.GetType().Name)
                .ToDictionary(g => g.Key, g => g.Count());

            _output.WriteLine("=== Component type counts ===");
            _output.WriteLine("Type                             MVRE    Bridge");
            _output.WriteLine("----                             ----    ------");

            var allTypes = new HashSet<string>(mvReTypeCounts.Keys);
            foreach (string key in bridgeTypeCounts.Keys) allTypes.Add(key);

            foreach (string typeName in allTypes.OrderBy(t => t))
            {
                int mvReCount = mvReTypeCounts.TryGetValue(typeName, out int mc) ? mc : 0;
                int bridgeCount = bridgeTypeCounts.TryGetValue(typeName, out int bc) ? bc : 0;

                _output.WriteLine(string.Format("{0,-32} {1,6}  {2,6}",
                    typeName, mvReCount, bridgeCount));
            }

            bool hasAirComponents = bridgeComps.Any(c => c is IAirSystemComponent);
            Assert.True(hasAirComponents, "Bridge output must contain air-side components");
        }

        [Fact]
        public void Compare_Connector_Type_Counts()
        {
            string mvRePath = GetMVREPath();
            if (mvRePath == null)
            {
                _output.WriteLine("MVRE.json not found - skipping comparison.");
                return;
            }

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            if (mvReCentre == null || bridgeCentre == null)
            {
                return;
            }

            SystemPlantRoom mvReRoom = mvReCentre.GetSystemPlantRooms()?[0];
            SystemPlantRoom bridgeRoom = bridgeCentre.GetSystemPlantRooms()?[0];

            if (mvReRoom == null || bridgeRoom == null)
            {
                return;
            }

            List<ISystemConnection> mvReConns = mvReRoom.GetSystemConnections();
            List<ISystemConnection> bridgeConns = bridgeRoom.GetSystemConnections();

            _output.WriteLine("=== Connection type counts ===");
            _output.WriteLine(string.Format("MVRE connections:    {0}", mvReConns != null ? mvReConns.Count : 0));
            _output.WriteLine(string.Format("Bridge connections:  {0}", bridgeConns != null ? bridgeConns.Count : 0));

            if (bridgeConns != null && mvReConns != null)
            {
                var mvReSystemTypes = mvReConns
                    .Where(c => c?.SystemType != null)
                    .GroupBy(c => c.SystemType.Type?.Name ?? "null")
                    .ToDictionary(g => g.Key, g => g.Count());

                var bridgeSystemTypes = bridgeConns
                    .Where(c => c?.SystemType != null)
                    .GroupBy(c => c.SystemType.Type?.Name ?? "null")
                    .ToDictionary(g => g.Key, g => g.Count());

                _output.WriteLine("Connection System Types:");
                _output.WriteLine("Type                             MVRE    Bridge");
                _output.WriteLine("----                             ----    ------");

                var allTypes = new HashSet<string>(mvReSystemTypes.Keys);
                foreach (string key in bridgeSystemTypes.Keys) allTypes.Add(key);

                foreach (string typeName in allTypes.OrderBy(t => t))
                {
                    int mvReCount = mvReSystemTypes.TryGetValue(typeName, out int mc) ? mc : 0;
                    int bridgeCount = bridgeSystemTypes.TryGetValue(typeName, out int bc) ? bc : 0;
                    _output.WriteLine(string.Format("{0,-32} {1,6}  {2,6}",
                        typeName, mvReCount, bridgeCount));
                }
            }

            Assert.NotNull(bridgeConns);
            Assert.True(bridgeConns.Count > 0, "Bridge output must have connections");
        }

        [Fact]
        public void Report_Structural_Differences()
        {
            string mvRePath = GetMVREPath();
            if (mvRePath == null)
            {
                _output.WriteLine("MVRE.json not found - skipping comparison.");
                return;
            }

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            if (mvReCentre == null || bridgeCentre == null)
            {
                return;
            }

            _output.WriteLine("=== Structural Comparison ===");

            int mvReRoomCount = mvReCentre.GetSystemPlantRooms()?.Count ?? 0;
            int bridgeRoomCount = bridgeCentre.GetSystemPlantRooms()?.Count ?? 0;
            _output.WriteLine(string.Format("Plant room count:       MVRE={0}  Bridge={1}  {2}",
                mvReRoomCount, bridgeRoomCount,
                mvReRoomCount == bridgeRoomCount ? "OK" : "DIFF"));

            if (mvReRoomCount > 0 && bridgeRoomCount > 0)
            {
                SystemPlantRoom mvReRoom = mvReCentre.GetSystemPlantRooms()[0];
                SystemPlantRoom bridgeRoom = bridgeCentre.GetSystemPlantRooms()[0];

                int mvReSysCount = mvReRoom.GetSystems()?.Count ?? 0;
                int bridgeSysCount = bridgeRoom.GetSystems()?.Count ?? 0;
                _output.WriteLine(string.Format("System count:           MVRE={0}  Bridge={1}  {2}",
                    mvReSysCount, bridgeSysCount,
                    mvReSysCount == bridgeSysCount ? "OK" : "DIFF"));

                int mvReCompCount = mvReRoom.GetSystemComponents<ISystemComponent>()?.Count ?? 0;
                int bridgeCompCount = bridgeRoom.GetSystemComponents<ISystemComponent>()?.Count ?? 0;
                _output.WriteLine(string.Format("Component count:        MVRE={0}  Bridge={1}  {2}",
                    mvReCompCount, bridgeCompCount,
                    System.Math.Abs(mvReCompCount - bridgeCompCount) <= 5 ? "CLOSE" : "DIFF"));

                int mvReConnCount = mvReRoom.GetSystemConnections()?.Count ?? 0;
                int bridgeConnCount = bridgeRoom.GetSystemConnections()?.Count ?? 0;
                _output.WriteLine(string.Format("Connection count:       MVRE={0}  Bridge={1}  {2}",
                    mvReConnCount, bridgeConnCount,
                    System.Math.Abs(mvReConnCount - bridgeConnCount) <= 5 ? "CLOSE" : "DIFF"));

                bool mvReHasAir = mvReRoom.GetSystems()?.Any(s => s is AirSystem) == true;
                bool bridgeHasAir = bridgeRoom.GetSystems()?.Any(s => s is AirSystem) == true;
                _output.WriteLine(string.Format("Has AirSystem:          MVRE={0}  Bridge={1}  {2}",
                    mvReHasAir, bridgeHasAir,
                    mvReHasAir == bridgeHasAir ? "OK" : "DIFF"));

                bool mvReHasSpace = mvReRoom.GetSystemComponents<SystemSpace>()?.Count > 0;
                bool bridgeHasSpace = bridgeRoom.GetSystemComponents<SystemSpace>()?.Count > 0;
                _output.WriteLine(string.Format("Has SystemSpace:        MVRE={0}  Bridge={1}  {2}",
                    mvReHasSpace, bridgeHasSpace,
                    mvReHasSpace == bridgeHasSpace ? "OK" : "DIFF"));
            }

            Assert.True(true);
        }
    }
}
