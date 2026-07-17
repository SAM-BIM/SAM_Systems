// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using SAM.Geometry.Systems;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Create
{
    /// <summary>
    /// Proves the display bridge's two-row auto-layout (SAM.Analytical.Systems.Create.DisplaySystemPlantRoom) for
    /// a single-AirSystem twin-wheel plant room. Confirmed by direct probe, the full flow-ordered component list
    /// is: Junction Fresh Air, Heat Recovery (exchanger), Cooling Coil, Heating Coil, Fan (supply), Group
    /// Junction, Damper 1, Room - then TrySplitChains splits at Room (index 7) and lays the remaining extract-side
    /// components (Group Junction, Fan (extract), Junction Exhaust Air) on a second, reversed row. Resulting
    /// coordinates (column step 1.0, row step 0.8): supply row y=0 at x=0..7 in that order; extract row y=-0.8
    /// with Junction Exhaust Air x=5, Fan (extract) x=6, Group Junction x=7.
    /// </summary>
    public class TwoRowLayoutTests
    {
        private const double Tolerance = 1e-9;

        private static SystemPlantRoom BuildTwinWheelPlantRoom()
        {
            TwinWheelExample.MollierProcesses(out List<IMollierProcess> supply, out List<IMollierProcess> extract);
            return Mollier.Create.SystemPlantRoom(supply, extract, TwinWheelExample.DefaultSupplyAirflow, TwinWheelExample.DefaultExtractAirflow, "PR");
        }

        private static DisplaySystemManager LoadManager()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"resources\Analytical\Systems\SAM_DisplaySystemManager.JSON");
            Assert.True(File.Exists(path), "SAM_DisplaySystemManager.JSON fixture missing from test output - the csproj <None CopyToOutputDirectory> item must copy files\\resources\\Analytical\\Systems\\SAM_DisplaySystemManager.JSON.");
            return SAM.Core.Create.IJSAMObject<DisplaySystemManager>(File.ReadAllText(path));
        }

        private static Point2D Origin(ISystemComponent component)
        {
            IDisplaySystemObject<SystemGeometryInstance> displayObject = Assert.IsAssignableFrom<IDisplaySystemObject<SystemGeometryInstance>>(component);
            Point2D origin = displayObject.SystemGeometry?.CoordinateSystem2D?.Origin;
            Assert.NotNull(origin);
            return origin;
        }

        private static ISystemComponent NamedOnRow(SystemPlantRoom plantRoom, string name, double y)
        {
            return plantRoom.GetSystemComponents<ISystemComponent>()
                .Single(c => (c as SystemObject)?.Name == name
                    && c is IDisplaySystemObject<SystemGeometryInstance> dgo
                    && System.Math.Abs(dgo.SystemGeometry.CoordinateSystem2D.Origin.Y - y) < Tolerance);
        }

        [Fact]
        public void TwinWheel_TwoRow_UsesExactlyTwoRows()
        {
            SystemPlantRoom plantRoom = BuildTwinWheelPlantRoom();
            Assert.True(plantRoom is DisplaySystemPlantRoom, "Bridge output is not display-native in this environment (DefaultDisplaySystemManager unavailable) - coordinate assertions cannot run.");

            List<double> distinctYs = plantRoom.GetSystemComponents<ISystemComponent>()
                .OfType<IDisplaySystemObject<SystemGeometryInstance>>()
                .Select(d => d.SystemGeometry.CoordinateSystem2D.Origin.Y)
                .Distinct()
                .OrderBy(y => y)
                .ToList();

            Assert.Equal(2, distinctYs.Count);
            Assert.Equal(-0.8, distinctYs[0], 9);
            Assert.Equal(0.0, distinctYs[1], 9);
        }

        [Fact]
        public void TwinWheel_TwoRow_SupplyRow_IsLeftToRight()
        {
            SystemPlantRoom plantRoom = BuildTwinWheelPlantRoom();

            SystemExchanger exchanger = plantRoom.GetSystemComponents<SystemExchanger>().Single();
            SystemCoolingCoil coolingCoil = plantRoom.GetSystemComponents<SystemCoolingCoil>().Single();
            SystemHeatingCoil heatingCoil = plantRoom.GetSystemComponents<SystemHeatingCoil>().Single();
            SystemFan supplyFan = plantRoom.GetSystemComponents<SystemFan>().Single(f => System.Math.Abs(f.DesignFlowRate.Value - 2500.0) < 0.5);
            SystemSpace room = plantRoom.GetSystemComponents<SystemSpace>().Single();

            Point2D exchangerOrigin = Origin(exchanger);
            Point2D coolingOrigin = Origin(coolingCoil);
            Point2D heatingOrigin = Origin(heatingCoil);
            Point2D fanOrigin = Origin(supplyFan);
            Point2D roomOrigin = Origin(room);

            // All sit on the supply row.
            Assert.Equal(0.0, exchangerOrigin.Y, 9);
            Assert.Equal(0.0, coolingOrigin.Y, 9);
            Assert.Equal(0.0, heatingOrigin.Y, 9);
            Assert.Equal(0.0, fanOrigin.Y, 9);
            Assert.Equal(0.0, roomOrigin.Y, 9);

            // Chain-consecutive components (exchanger -> cooling coil -> heating coil -> supply fan) step by
            // exactly one column.
            Assert.Equal(exchangerOrigin.X + 1.0, coolingOrigin.X, 9);
            Assert.Equal(coolingOrigin.X + 1.0, heatingOrigin.X, 9);
            Assert.Equal(heatingOrigin.X + 1.0, fanOrigin.X, 9);

            // The room is reached via a Group Junction and a Damper (not chain-adjacent to the fan), so only its
            // relative position - strictly further right, same row - is asserted rather than a fixed step.
            Assert.True(roomOrigin.X > fanOrigin.X, string.Format("Room X ({0}) must be greater than supply fan X ({1})", roomOrigin.X, fanOrigin.X));
        }

        [Fact]
        public void TwinWheel_ExtractRow_IsPlacedInReverseDirection()
        {
            SystemPlantRoom plantRoom = BuildTwinWheelPlantRoom();

            SystemFan extractFan = plantRoom.GetSystemComponents<SystemFan>().Single(f => System.Math.Abs(f.DesignFlowRate.Value - 2300.0) < 0.5);
            ISystemComponent exhaustJunction = NamedOnRow(plantRoom, "Junction Exhaust Air", -0.8);
            ISystemComponent extractGroupJunction = NamedOnRow(plantRoom, "Group Junction", -0.8);

            Point2D fanOrigin = Origin(extractFan);
            Point2D exhaustOrigin = Origin(exhaustJunction);
            Point2D groupOrigin = Origin(extractGroupJunction);

            Assert.Equal(-0.8, fanOrigin.Y, 9);
            Assert.Equal(-0.8, exhaustOrigin.Y, 9);
            Assert.Equal(-0.8, groupOrigin.Y, 9);

            // Airflow on the extract row runs Room -> Group Junction -> (shared exchanger, on the supply row) ->
            // extract fan -> exhaust junction, so X must DECREASE along that direction: a right-to-left mirror of
            // the supply row. The component nearest the room (Group Junction) has the largest X; the component
            // nearest the exhaust (Junction Exhaust Air) has the smallest.
            Assert.True(groupOrigin.X > fanOrigin.X, string.Format("Group Junction X ({0}) must be greater than extract fan X ({1}) - nearer the room", groupOrigin.X, fanOrigin.X));
            Assert.True(fanOrigin.X > exhaustOrigin.X, string.Format("Extract fan X ({0}) must be greater than exhaust junction X ({1}) - nearer the exhaust", fanOrigin.X, exhaustOrigin.X));
        }

        [Fact]
        public void TwinWheel_SharedExchanger_IsDrawnOnce_OnSupplyRow()
        {
            SystemPlantRoom plantRoom = BuildTwinWheelPlantRoom();

            // Exactly one display object for the shared exchanger: its second air path reaches the extract row
            // through connections, not a second symbol.
            SystemExchanger exchanger = Assert.Single(plantRoom.GetSystemComponents<SystemExchanger>());

            Point2D origin = Origin(exchanger);
            Assert.Equal(0.0, origin.Y, 9);
        }

        [Fact]
        public void TwinWheel_AirConnections_AreRouted()
        {
            SystemPlantRoom plantRoom = BuildTwinWheelPlantRoom();
            DisplaySystemManager manager = LoadManager();

            DisplaySystemPlantRoom displayRoom = SAM.Analytical.Systems.Create.DisplaySystemPlantRoom(plantRoom, out List<string> report, manager);
            Assert.NotNull(displayRoom);

            Assert.DoesNotContain(report, line => line != null && line.StartsWith("No symbol"));

            List<ISystemConnection> connections = displayRoom.GetSystemConnections();
            Assert.NotEmpty(connections);

            List<ISystemConnection> airConnections = connections.FindAll(c =>
            {
                SystemType systemType = c.SystemType;
                return systemType?.Type != null && typeof(AirSystem).IsAssignableFrom(systemType.Type);
            });
            Assert.NotEmpty(airConnections);

            foreach (ISystemConnection connection in airConnections)
            {
                IDisplaySystemObject displayObject = Assert.IsAssignableFrom<IDisplaySystemObject>(connection);
                object geometry = SAM.Analytical.Systems.Query.SAMGeometry2Dobject(displayObject);
                Assert.NotNull(geometry);
            }
        }

        [Fact]
        public void SupplyOnly_ShortChain_FallsBackTo_SingleRow()
        {
            // TrySplitChains needs >= 4 ordered components AND a SystemSpace among them; a short supply-only
            // chain (2 processes, no room) satisfies neither, so the layout must fall back to a single row.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 50, 101325);
            List<IMollierProcess> supply = new List<IMollierProcess>
            {
                start.HeatingProcess(30.0),
                start.FanProcess(1.5)
            };

            SystemPlantRoom plantRoom = Mollier.Create.SystemPlantRoom(supply, 2.0);
            Assert.NotNull(plantRoom);

            List<IDisplaySystemObject<SystemGeometryInstance>> displayComponents = plantRoom.GetSystemComponents<ISystemComponent>()
                .OfType<IDisplaySystemObject<SystemGeometryInstance>>()
                .ToList();

            Assert.NotEmpty(displayComponents);
            Assert.All(displayComponents, d => Assert.Equal(0.0, d.SystemGeometry.CoordinateSystem2D.Origin.Y, 9));
        }
    }
}
