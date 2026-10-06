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
        private const string MissingFixtureMessage =
            "MVRE.json fixture missing from test output - the csproj <None CopyToOutputDirectory> item must copy files\\resources\\Analytical\\Systems\\SystemEnergyCentre\\MVRE.json.";

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
            Assert.False(mvRePath == null, MissingFixtureMessage);

            _output.WriteLine("Loading MVRE.json from: " + mvRePath);

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            Assert.NotNull(mvReCentre);

            // MVRE.json contains exactly one SystemPlantRoom (verified against the source JSON).
            List<SystemPlantRoom> plantRooms = mvReCentre.GetSystemPlantRooms();
            Assert.NotNull(plantRooms);
            Assert.Single(plantRooms);

            // MVRE.json declares exactly two SystemEnergySources: an ElectricalEnergySource named
            // "Grid Supplied Electricity" and a SystemEnergySource named "Natural Gas".
            List<SystemEnergySource> energySources = mvReCentre.GetSystemEnergySources();
            Assert.NotNull(energySources);
            Assert.Equal(2, energySources.Count);
            Assert.Contains(energySources, x => x != null && x.Name == "Grid Supplied Electricity");
            Assert.Contains(energySources, x => x != null && x.Name == "Natural Gas");
        }

        [Fact]
        public void Compare_PlantRoom_Counts()
        {
            string mvRePath = GetMVREPath();
            Assert.False(mvRePath == null, MissingFixtureMessage);

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            Assert.NotNull(mvReCentre);
            Assert.NotNull(bridgeCentre);

            List<SystemPlantRoom> mvRePlantRooms = mvReCentre.GetSystemPlantRooms();
            List<SystemPlantRoom> bridgePlantRooms = bridgeCentre.GetSystemPlantRooms();

            _output.WriteLine(string.Format("MVRE plant rooms: {0}", mvRePlantRooms != null ? mvRePlantRooms.Count : 0));
            _output.WriteLine(string.Format("Bridge plant rooms: {0}", bridgePlantRooms != null ? bridgePlantRooms.Count : 0));

            Assert.NotNull(mvRePlantRooms);
            Assert.NotNull(bridgePlantRooms);

            // Both the MVRE reference model and the Mollier bridge model a single-AHU plant room: the
            // whole air-handling system (and, for MVRE, its liquid plant) lives in one SystemPlantRoom.
            Assert.Single(mvRePlantRooms);
            Assert.Single(bridgePlantRooms);
            Assert.Equal(mvRePlantRooms.Count, bridgePlantRooms.Count);
        }

        [Fact]
        public void Compare_Component_Type_Counts()
        {
            string mvRePath = GetMVREPath();
            Assert.False(mvRePath == null, MissingFixtureMessage);

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            Assert.NotNull(mvReCentre);
            Assert.NotNull(bridgeCentre);

            SystemPlantRoom mvReRoom = mvReCentre.GetSystemPlantRooms()?[0];
            SystemPlantRoom bridgeRoom = bridgeCentre.GetSystemPlantRooms()?[0];

            Assert.NotNull(mvReRoom);
            Assert.NotNull(bridgeRoom);

            List<ISystemComponent> mvReComps = mvReRoom.GetSystemComponents<ISystemComponent>();
            List<ISystemComponent> bridgeComps = bridgeRoom.GetSystemComponents<ISystemComponent>();

            Assert.NotNull(mvReComps);
            Assert.NotNull(bridgeComps);

            _output.WriteLine(string.Format("MVRE components: {0}, Bridge components: {1}", mvReComps.Count, bridgeComps.Count));

            // MVRE air-side census (verified against the source JSON): a twin-fan MVHR AHU with one
            // heat-recovery exchanger, one damper and one room space.
            Assert.Equal(2, mvReRoom.GetSystemComponents<SystemFan>()?.Count ?? 0);
            Assert.Equal(1, mvReRoom.GetSystemComponents<SystemExchanger>()?.Count ?? 0);
            Assert.Equal(1, mvReRoom.GetSystemComponents<SystemDamper>()?.Count ?? 0);
            Assert.Equal(1, mvReRoom.GetSystemComponents<SystemSpace>()?.Count ?? 0);

            // Bridge output: the twin-wheel example always creates exactly one shared exchanger, a
            // supply and an extract fan (>=2), one room space and one room damper.
            Assert.Single(bridgeRoom.GetSystemComponents<SystemExchanger>());
            Assert.True((bridgeRoom.GetSystemComponents<SystemFan>()?.Count ?? 0) >= 2, "Bridge should have at least a supply and extract fan");
            Assert.Equal(1, bridgeRoom.GetSystemComponents<SystemSpace>()?.Count ?? 0);
            Assert.Equal(1, bridgeRoom.GetSystemComponents<SystemDamper>()?.Count ?? 0);

            // Shared air-side component types: both models must contain at least one of each of these
            // types. The allowed asymmetry is NOT compared here: MVRE has a full liquid plant (2
            // DisplaySystemMultiBoiler, 1 DisplaySystemMultiChiller, 1 DisplaySystemAirSourceHeatPump,
            // pumps, controllers, sensors, a PV panel) that the bridge does not generate, and the bridge
            // would instead generate SystemBoiler/SystemAirSourceChiller for equivalent plant - neither
            // side's liquid-plant shape is asserted.
            Assert.NotEmpty(mvReRoom.GetSystemComponents<SystemFan>());
            Assert.NotEmpty(bridgeRoom.GetSystemComponents<SystemFan>());

            Assert.NotEmpty(mvReRoom.GetSystemComponents<SystemExchanger>());
            Assert.NotEmpty(bridgeRoom.GetSystemComponents<SystemExchanger>());

            Assert.NotEmpty(mvReRoom.GetSystemComponents<SystemDamper>());
            Assert.NotEmpty(bridgeRoom.GetSystemComponents<SystemDamper>());

            Assert.NotEmpty(mvReRoom.GetSystemComponents<SystemSpace>());
            Assert.NotEmpty(bridgeRoom.GetSystemComponents<SystemSpace>());

            Assert.NotEmpty(mvReRoom.GetSystemComponents<SystemAirJunction>());
            Assert.NotEmpty(bridgeRoom.GetSystemComponents<SystemAirJunction>());
        }

        [Fact]
        public void Compare_Connector_Type_Counts()
        {
            string mvRePath = GetMVREPath();
            Assert.False(mvRePath == null, MissingFixtureMessage);

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            Assert.NotNull(mvReCentre);
            Assert.NotNull(bridgeCentre);

            SystemPlantRoom mvReRoom = mvReCentre.GetSystemPlantRooms()?[0];
            SystemPlantRoom bridgeRoom = bridgeCentre.GetSystemPlantRooms()?[0];

            Assert.NotNull(mvReRoom);
            Assert.NotNull(bridgeRoom);

            List<ISystemConnection> mvReConns = mvReRoom.GetSystemConnections();
            List<ISystemConnection> bridgeConns = bridgeRoom.GetSystemConnections();

            _output.WriteLine("=== Connection counts ===");
            _output.WriteLine(string.Format("MVRE connections:    {0}", mvReConns != null ? mvReConns.Count : 0));
            _output.WriteLine(string.Format("Bridge connections:  {0}", bridgeConns != null ? bridgeConns.Count : 0));

            Assert.NotNull(mvReConns);
            Assert.NotNull(bridgeConns);

            // MVRE JSON census: 25 DisplaySystemConnection objects in the plant room's relation
            // cluster; GetSystemConnections() on the plant room returns the same count at runtime.
            Assert.Equal(25, mvReConns.Count);

            Assert.True(bridgeConns.Count > 0, "Bridge output must have connections");

            // Every connection on both sides must resolve at least two endpoints (its two connected
            // components), otherwise it is a dangling/malformed connection.
            foreach (ISystemConnection connection in mvReConns)
            {
                Assert.NotNull(connection);
                List<ObjectReference> objectReferences = connection.ObjectReferences;
                Assert.NotNull(objectReferences);
                Assert.True(objectReferences.Count >= 2, "MVRE connection has fewer than 2 endpoints");
            }

            foreach (ISystemConnection connection in bridgeConns)
            {
                Assert.NotNull(connection);
                List<ObjectReference> objectReferences = connection.ObjectReferences;
                Assert.NotNull(objectReferences);
                Assert.True(objectReferences.Count >= 2, "Bridge connection has fewer than 2 endpoints");
            }

            // Both sides must contain at least one AirSystem-typed connection.
            bool mvReHasAirConnection = mvReConns.Any(c => c?.SystemType?.Type != null && typeof(AirSystem).IsAssignableFrom(c.SystemType.Type));
            bool bridgeHasAirConnection = bridgeConns.Any(c => c?.SystemType?.Type != null && typeof(AirSystem).IsAssignableFrom(c.SystemType.Type));

            Assert.True(mvReHasAirConnection, "MVRE must contain at least one AirSystem-typed connection");
            Assert.True(bridgeHasAirConnection, "Bridge must contain at least one AirSystem-typed connection");
        }

        [Fact]
        public void AirSide_Topology_Matches_MVRE_Shape()
        {
            string mvRePath = GetMVREPath();
            Assert.False(mvRePath == null, MissingFixtureMessage);

            SystemEnergyCentre mvReCentre = SAM.Analytical.Systems.Query.SystemEnergyCentre(mvRePath);
            SystemEnergyCentre bridgeCentre = TwinWheelExample.Create();

            Assert.NotNull(mvReCentre);
            Assert.NotNull(bridgeCentre);

            List<SystemPlantRoom> mvRePlantRooms = mvReCentre.GetSystemPlantRooms();
            List<SystemPlantRoom> bridgePlantRooms = bridgeCentre.GetSystemPlantRooms();

            Assert.NotNull(mvRePlantRooms);
            Assert.NotNull(bridgePlantRooms);
            Assert.Single(mvRePlantRooms);
            Assert.Single(bridgePlantRooms);

            SystemPlantRoom mvReRoom = mvRePlantRooms[0];
            SystemPlantRoom bridgeRoom = bridgePlantRooms[0];

            _output.WriteLine("=== Air-side topology comparison ===");

            // Both models are served by exactly one AirSystem.
            int mvReAirSystemCount = mvReRoom.GetSystems()?.Count(s => s is AirSystem) ?? 0;
            int bridgeAirSystemCount = bridgeRoom.GetSystems()?.Count(s => s is AirSystem) ?? 0;
            _output.WriteLine(string.Format("AirSystem count:  MVRE={0}  Bridge={1}", mvReAirSystemCount, bridgeAirSystemCount));
            Assert.Equal(1, mvReAirSystemCount);
            Assert.Equal(1, bridgeAirSystemCount);

            // Both models share exactly one heat-recovery exchanger (MVRE: 1 DisplaySystemExchanger;
            // bridge: the shared twin-wheel exchanger).
            int mvReExchangerCount = mvReRoom.GetSystemComponents<SystemExchanger>()?.Count ?? 0;
            int bridgeExchangerCount = bridgeRoom.GetSystemComponents<SystemExchanger>()?.Count ?? 0;
            _output.WriteLine(string.Format("Exchanger count:  MVRE={0}  Bridge={1}", mvReExchangerCount, bridgeExchangerCount));
            Assert.Equal(1, mvReExchangerCount);
            Assert.Equal(1, bridgeExchangerCount);
            Assert.Equal(mvReExchangerCount, bridgeExchangerCount);

            // Both models have at least a supply and an extract fan.
            int mvReFanCount = mvReRoom.GetSystemComponents<SystemFan>()?.Count ?? 0;
            int bridgeFanCount = bridgeRoom.GetSystemComponents<SystemFan>()?.Count ?? 0;
            _output.WriteLine(string.Format("Fan count:        MVRE={0}  Bridge={1}", mvReFanCount, bridgeFanCount));
            Assert.True(mvReFanCount >= 2, "MVRE should have at least a supply and extract fan");
            Assert.True(bridgeFanCount >= 2, "Bridge should have at least a supply and extract fan");

            // Every air-side component in both plant rooms must be wired with at least one In and one
            // Out AirSystem connector (mirrors TasExportReadinessTests.All_AirComponents_Have_In_And_Out_Connectors).
            SystemType airSystemType = new SystemType(typeof(AirSystem));
            AssertAllAirComponentsWired(mvReRoom, airSystemType, "MVRE");
            AssertAllAirComponentsWired(bridgeRoom, airSystemType, "Bridge");
        }

        private static void AssertAllAirComponentsWired(SystemPlantRoom plantRoom, SystemType airSystemType, string label)
        {
            List<ISystemComponent> components = plantRoom.GetSystemComponents<ISystemComponent>();
            Assert.NotNull(components);
            Assert.NotEmpty(components);

            foreach (ISystemComponent component in components)
            {
                SystemComponent systemComponent = component as SystemComponent;
                if (systemComponent == null)
                {
                    continue;
                }

                // Only check air-side components
                if (!(component is IAirSystemComponent))
                {
                    continue;
                }

                string name = systemComponent.Name ?? systemComponent.GetType().Name;

                // Check the component has at least one In connector
                List<int> inIndexes = plantRoom.Indexes(systemComponent, airSystemType,
                    ConnectorStatus.Undefined, SAM.Core.Direction.In);
                Assert.NotNull(inIndexes);
                Assert.True(inIndexes.Count > 0,
                    string.Format("{0} air component '{1}' has no AirSystem In connectors", label, name));

                // Check the component has at least one Out connector
                List<int> outIndexes = plantRoom.Indexes(systemComponent, airSystemType,
                    ConnectorStatus.Undefined, SAM.Core.Direction.Out);
                Assert.NotNull(outIndexes);
                Assert.True(outIndexes.Count > 0,
                    string.Format("{0} air component '{1}' has no AirSystem Out connectors", label, name));
            }
        }
    }
}
