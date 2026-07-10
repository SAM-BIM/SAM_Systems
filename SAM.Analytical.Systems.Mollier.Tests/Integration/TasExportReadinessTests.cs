// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SAM.Core;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Systems;
using Xunit;

namespace SAM.Analytical.Systems.Mollier.Tests.Integration
{
    public class TasExportReadinessTests
    {
        private static SystemEnergyCentre CreateFixture()
        {
            return TwinWheelExample.Create();
        }

        [Fact]
        public void EnergyCentre_Has_PlantRoom_With_AirSystem()
        {
            SystemEnergyCentre energyCentre = CreateFixture();
            Assert.NotNull(energyCentre);

            List<SystemPlantRoom> plantRooms = energyCentre.GetSystemPlantRooms();
            Assert.NotNull(plantRooms);
            Assert.Single(plantRooms);

            SystemPlantRoom plantRoom = plantRooms[0];
            List<SAM.Core.Systems.ISystem> systems = plantRoom.GetSystems();
            Assert.NotNull(systems);
            Assert.Contains(systems, s => s is AirSystem);
        }

        [Fact]
        public void All_AirComponents_Have_In_And_Out_Connectors()
        {
            SystemEnergyCentre energyCentre = CreateFixture();
            SystemPlantRoom plantRoom = energyCentre.GetSystemPlantRooms()[0];
            List<ISystemComponent> components = plantRoom.GetSystemComponents<ISystemComponent>();

            Assert.NotNull(components);
            Assert.NotEmpty(components);

            SystemType airSystemType = new SystemType(typeof(AirSystem));

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
                    string.Format("Air component '{0}' has no AirSystem In connectors", name));

                // Check the component has at least one Out connector
                List<int> outIndexes = plantRoom.Indexes(systemComponent, airSystemType,
                    ConnectorStatus.Undefined, SAM.Core.Direction.Out);
                Assert.NotNull(outIndexes);
                Assert.True(outIndexes.Count > 0,
                    string.Format("Air component '{0}' has no AirSystem Out connectors", name));
            }
        }

        [Fact]
        public void No_Dangling_Connectors_Except_Boundaries()
        {
            SystemEnergyCentre energyCentre = CreateFixture();
            SystemPlantRoom plantRoom = energyCentre.GetSystemPlantRooms()[0];
            List<ISystemComponent> components = plantRoom.GetSystemComponents<ISystemComponent>();

            SystemType airSystemType = new SystemType(typeof(AirSystem));

            foreach (ISystemComponent component in components)
            {
                SystemComponent systemComponent = component as SystemComponent;
                if (systemComponent == null)
                {
                    continue;
                }

                if (!(component is IAirSystemComponent))
                {
                    continue;
                }

                string typeName = systemComponent.GetType().Name;

                // Dangling In connectors are acceptable on fresh-air / outside-air junctions
                List<int> unconnectedIns = plantRoom.Indexes(systemComponent, airSystemType,
                    ConnectorStatus.Unconnected, SAM.Core.Direction.In);
                if (unconnectedIns != null && unconnectedIns.Count > 0)
                {
                    Assert.True(
                        typeName.Contains("Junction") || typeName.Contains("Outside"),
                        string.Format("Component '{0}' has unconnected In connectors (not a boundary)", typeName));
                }

                // Dangling Out connectors are acceptable on exhaust-air junctions or room space
                List<int> unconnectedOuts = plantRoom.Indexes(systemComponent, airSystemType,
                    ConnectorStatus.Unconnected, SAM.Core.Direction.Out);
                if (unconnectedOuts != null && unconnectedOuts.Count > 0)
                {
                    Assert.True(
                        typeName.Contains("Junction") || typeName.Contains("Space"),
                        string.Format("Component '{0}' has unconnected Out connectors (not a boundary/room)", typeName));
                }
            }
        }

        [Fact]
        public void Serializes_To_Valid_JSON_Without_Exceptions()
        {
            SystemEnergyCentre energyCentre = CreateFixture();

            JsonObject json = null;
            Exception ex = Record.Exception(() => { json = energyCentre.ToJsonObject(); });

            Assert.Null(ex);
            Assert.NotNull(json);

            // Round-trip: deserialize back
            SystemEnergyCentre roundTripped = new SystemEnergyCentre(json);
            Assert.NotNull(roundTripped);
            Assert.NotNull(roundTripped.GetSystemPlantRooms());
        }

        [Fact]
        public void JSON_Contains_Expected_Types()
        {
            SystemEnergyCentre energyCentre = CreateFixture();
            JsonObject json = energyCentre.ToJsonObject();
            string jsonText = json.ToString();

            Assert.NotNull(jsonText);

            // Check for key type identifiers in the JSON
            Assert.Contains("SystemEnergyCentre", jsonText);
            Assert.Contains("SystemPlantRoom", jsonText);
            Assert.Contains("AirSystem", jsonText);
            Assert.Contains("SystemConnection", jsonText);
            Assert.Contains("SystemFan", jsonText);
        }

        [Fact]
        public void All_Connections_Have_Both_Endpoints()
        {
            SystemEnergyCentre energyCentre = CreateFixture();
            SystemPlantRoom plantRoom = energyCentre.GetSystemPlantRooms()[0];
            List<ISystemConnection> connections = plantRoom.GetSystemConnections();

            Assert.NotNull(connections);

            foreach (ISystemConnection connection in connections)
            {
                if (connection == null)
                {
                    continue;
                }

                List<ObjectReference> objectReferences = connection.ObjectReferences;
                Assert.NotNull(objectReferences);
                Assert.True(objectReferences.Count >= 2,
                    "Connection has fewer than 2 endpoints");
            }
        }

        [Fact]
        public void Connections_Use_AirSystem_Type()
        {
            SystemEnergyCentre energyCentre = CreateFixture();
            SystemPlantRoom plantRoom = energyCentre.GetSystemPlantRooms()[0];
            List<ISystemConnection> connections = plantRoom.GetSystemConnections();

            Assert.NotNull(connections);
            Assert.NotEmpty(connections);

            // At least one connection should reference AirSystem
            bool foundAirConnection = false;
            foreach (ISystemConnection connection in connections)
            {
                if (connection == null)
                {
                    continue;
                }

                SystemType systemType = connection.SystemType;
                if (systemType != null)
                {
                    System.Type type = systemType.Type;
                    if (type != null && typeof(AirSystem).IsAssignableFrom(type))
                    {
                        foundAirConnection = true;
                        break;
                    }
                }
            }

            Assert.True(foundAirConnection, "No connections reference AirSystem");
        }
    }
}
