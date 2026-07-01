// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        // A SystemJunction defines its connectors In-then-Out (see SystemJunction<T>.SystemConnectorManager), so
        // the In connector is list index 0 and the Out connector is list index 1. This matches the connection
        // indexes in the reference HeatRecovery-Junctions plant room (fresh-air junction wired on Out=1, exhaust-air
        // junction wired on In=0).
        private const int JunctionInIndex = 0;
        private const int JunctionOutIndex = 1;

        /// <summary>
        /// Caps an open outside-air boundary of the air system with an explicit <see cref="SystemAirJunction"/> so
        /// the air path is not left dangling: a fresh-air junction feeds an open In (the supply intake), and an
        /// exhaust-air junction terminates an open Out (the extract discharge).
        /// </summary>
        /// <remarks>
        /// Because supply and extract share a single air system, the specific boundary component is passed in (the
        /// first supply component for the intake, the last extract component for the discharge) rather than found by
        /// an ambiguous open-connector query. The room-side boundaries (supply discharge, extract intake) are left
        /// open on purpose. Does nothing when the boundary component is already a junction or has no open connector
        /// in the requested direction.
        /// </remarks>
        private static void AddBoundaryJunction(SystemPlantRoom systemPlantRoom, AirSystem airSystem, ISystemComponent boundaryComponent, SAM.Core.Direction boundaryDirection, string junctionName)
        {
            if (systemPlantRoom == null || airSystem == null || boundaryComponent == null || boundaryComponent is ISystemConnection || boundaryComponent is SystemAirJunction)
            {
                return;
            }

            SystemType systemType = new SystemType(airSystem);

            int boundaryIndex = UnconnectedIndex(systemPlantRoom, boundaryComponent, systemType, boundaryDirection);
            if (boundaryIndex == -1)
            {
                return;
            }

            SystemAirJunction systemAirJunction = new SystemAirJunction(junctionName);

            if (boundaryDirection == SAM.Core.Direction.In)
            {
                // Outside air enters the system: junction.Out -> component.In.
                systemPlantRoom.Connect(systemAirJunction, boundaryComponent, out _, airSystem, JunctionOutIndex, boundaryIndex);
            }
            else
            {
                // Air leaves the system to exhaust: component.Out -> junction.In.
                systemPlantRoom.Connect(boundaryComponent, systemAirJunction, out _, airSystem, boundaryIndex, JunctionInIndex);
            }
        }
    }
}
