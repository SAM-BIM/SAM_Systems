// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Core;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using SAM.Geometry.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        // Air-side components (junction, damper, space) all order their connectors In-then-Out, so In = 0 and
        // Out = 1. Matches the reference room/group plant room (Group Junction -> Damper -> Space -> Group Junction).
        private const int RoomInIndex = 0;
        private const int RoomOutIndex = 1;

        /// <summary>
        /// Adds the room-side arrangement that closes the air loop between the supply discharge and the extract
        /// intake: <c>supplyDischarge -> Group Junction -> Damper -> Room (Space) -> Group Junction -> extractIntake</c>,
        /// all on the single <paramref name="airSystem"/>. The room is created from the Mollier room condition
        /// point. Mirrors the object graph of the reference room/group plant room.
        /// </summary>
        private static void AddRoom(SystemPlantRoom systemPlantRoom, AirSystem airSystem, ISystemComponent supplyDischarge, ISystemComponent extractIntake, MollierPoint roomPoint, List<ConversionDiagnostic> diagnostics)
        {
            if (systemPlantRoom == null || airSystem == null || supplyDischarge == null || extractIntake == null)
            {
                return;
            }

            SystemType systemType = new SystemType(airSystem);

            int supplyOutIndex = UnconnectedIndex(systemPlantRoom, supplyDischarge, systemType, SAM.Core.Direction.Out);
            int extractInIndex = UnconnectedIndex(systemPlantRoom, extractIntake, systemType, SAM.Core.Direction.In);
            if (supplyOutIndex == -1 || extractInIndex == -1)
            {
                return;
            }

            SystemAirJunction groupJunctionSupply = new SystemAirJunction("Group Junction");
            SystemDamper systemDamper = new SystemDamper("Damper 1");
            SystemSpace systemSpace = CreateRoom(roomPoint);
            SystemAirJunction groupJunctionExtract = new SystemAirJunction("Group Junction");

            // supplyDischarge.Out -> Group Junction.In
            CheckConnect(systemPlantRoom.Connect(supplyDischarge, groupJunctionSupply, out _, airSystem, supplyOutIndex, RoomInIndex), supplyDischarge, groupJunctionSupply, diagnostics);
            // Group Junction.Out -> Damper.In
            CheckConnect(systemPlantRoom.Connect(groupJunctionSupply, systemDamper, out _, airSystem, RoomOutIndex, RoomInIndex), groupJunctionSupply, systemDamper, diagnostics);
            // Damper.Out -> Room.In
            CheckConnect(systemPlantRoom.Connect(systemDamper, systemSpace, out _, airSystem, RoomOutIndex, RoomInIndex), systemDamper, systemSpace, diagnostics);
            // Room.Out -> Group Junction.In
            CheckConnect(systemPlantRoom.Connect(systemSpace, groupJunctionExtract, out _, airSystem, RoomOutIndex, RoomInIndex), systemSpace, groupJunctionExtract, diagnostics);
            // Group Junction.Out -> extractIntake.In
            CheckConnect(systemPlantRoom.Connect(groupJunctionExtract, extractIntake, out _, airSystem, RoomOutIndex, extractInIndex), groupJunctionExtract, extractIntake, diagnostics);
        }

        /// <summary>
        /// Builds the room <see cref="SystemSpace"/> from the Mollier room condition point. The dry-bulb temperature
        /// and relative humidity become the room setpoints; area, volume and flow sizing are left as placeholders
        /// (the Mollier processes carry no space geometry or design-condition flows).
        /// </summary>
        private static SystemSpace CreateRoom(MollierPoint roomPoint)
        {
            double temperature = roomPoint == null ? double.NaN : roomPoint.DryBulbTemperature;
            double relativeHumidity = roomPoint == null ? double.NaN : roomPoint.RelativeHumidity;

            ModifiableValue temperatureSetpoint = double.IsNaN(temperature) ? null : new ModifiableValue(temperature);
            ModifiableValue relativeHumiditySetpoint = double.IsNaN(relativeHumidity) ? null : new ModifiableValue(relativeHumidity);

            return new SystemSpace("Room", 0, 0, temperatureSetpoint, relativeHumiditySetpoint, null, false, false, true, null, null, 0);
        }

        /// <summary>
        /// Creates a <see cref="DisplayAirSystemGroup"/> around the room-side items (Room, Damper and the two Group
        /// Junctions) once they have been laid out, and relates it to the air system and those items. No-ops when the
        /// room-side items carry no display geometry (logical fallback, e.g. no symbol library).
        /// </summary>
        private static void AddDisplayAirSystemGroup(SystemPlantRoom systemPlantRoom, AirSystem airSystem, List<ConversionDiagnostic> diagnostics)
        {
            if (systemPlantRoom == null || airSystem == null)
            {
                return;
            }

            List<ISystemComponent> members = new List<ISystemComponent>();

            foreach (SystemSpace systemSpace in systemPlantRoom.GetSystemComponents<SystemSpace>() ?? new List<SystemSpace>())
            {
                members.Add(systemSpace);
            }

            foreach (SystemDamper systemDamper in systemPlantRoom.GetSystemComponents<SystemDamper>() ?? new List<SystemDamper>())
            {
                members.Add(systemDamper);
            }

            foreach (SystemAirJunction systemAirJunction in systemPlantRoom.GetSystemComponents<SystemAirJunction>() ?? new List<SystemAirJunction>())
            {
                if (systemAirJunction?.Name == "Group Junction")
                {
                    members.Add(systemAirJunction);
                }
            }

            List<BoundingBox2D> boundingBox2Ds = new List<BoundingBox2D>();
            foreach (ISystemComponent member in members)
            {
                if (member is IDisplaySystemObject displaySystemObject)
                {
                    BoundingBox2D boundingBox2D = displaySystemObject.BoundingBox2D;
                    if (boundingBox2D != null)
                    {
                        boundingBox2Ds.Add(boundingBox2D);
                    }
                }
            }

            if (boundingBox2Ds.Count == 0)
            {
                return;
            }

            // A small margin so the outline sits around the items rather than exactly on them.
            BoundingBox2D unionBoundingBox2D = new BoundingBox2D(boundingBox2Ds);
            BoundingBox2D groupBoundingBox2D = new BoundingBox2D(unionBoundingBox2D.Min, unionBoundingBox2D.Max, 0.2);

            DisplayAirSystemGroup displayAirSystemGroup = new DisplayAirSystemGroup(new AirSystemGroup(airSystem.Name), groupBoundingBox2D);

            systemPlantRoom.Add(displayAirSystemGroup);

            bool groupConnected = systemPlantRoom.Connect(airSystem, displayAirSystemGroup);
            if (!groupConnected && diagnostics != null)
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.ConnectionFailed, $"Failed to connect air system '{airSystem.Name}' to display group '{displayAirSystemGroup.Name}'.", null));
            }

            List<bool> memberConnections = systemPlantRoom.Connect(displayAirSystemGroup, members);
            if (memberConnections != null && diagnostics != null)
            {
                for (int i = 0; i < memberConnections.Count && i < members.Count; i++)
                {
                    if (!memberConnections[i])
                    {
                        diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.ConnectionFailed, $"Failed to connect display group '{displayAirSystemGroup.Name}' to '{ComponentName(members[i])}'.", null));
                    }
                }
            }
        }

        /// <summary>
        /// Returns the start (room-side) Mollier point of the first process in the chain, or null.
        /// </summary>
        private static MollierPoint FirstStart(IEnumerable<IMollierProcess> mollierProcesses)
        {
            if (mollierProcesses == null)
            {
                return null;
            }

            foreach (IMollierProcess mollierProcess in mollierProcesses)
            {
                if (mollierProcess != null)
                {
                    return mollierProcess.Start;
                }
            }

            return null;
        }
    }
}
