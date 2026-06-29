// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
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
        /// Caps the open outside-air boundaries of an air-handling plant room with explicit air junctions so no air
        /// path is left dangling at an outside-air condition: a "Junction Fresh Air" feeds the supply intake (the
        /// supply system's open In), and - when an extract system is present - a "Junction Exhaust Air" terminates
        /// the extract discharge (the extract system's open Out).
        /// </summary>
        /// <remarks>
        /// Only the two outside-air connectors are capped. The supply discharge (supply -> room) and the extract
        /// intake (room -> extract) are left open on purpose: they are room-side, not outside-air, boundaries.
        /// In Mollier diagrams the outside air condition is shown but the full extract-to-exhaust path often is not;
        /// adding the junctions keeps the system definition complete even when the chart does not draw it.
        /// </remarks>
        private static void AddOutsideAirJunctions(SystemPlantRoom systemPlantRoom, string supplyAirSystemName, string extractAirSystemName)
        {
            if (systemPlantRoom == null)
            {
                return;
            }

            AddBoundaryJunction(systemPlantRoom, supplyAirSystemName, SAM.Core.Direction.In, "Junction Fresh Air");

            if (!string.IsNullOrWhiteSpace(extractAirSystemName))
            {
                AddBoundaryJunction(systemPlantRoom, extractAirSystemName, SAM.Core.Direction.Out, "Junction Exhaust Air");
            }
        }

        /// <summary>
        /// Adds a single boundary <see cref="SystemAirJunction"/> at the open <paramref name="boundaryDirection"/>
        /// connector of the named air system (the chain head for In, the chain tail for Out), unless that boundary
        /// component is already a junction.
        /// </summary>
        private static void AddBoundaryJunction(SystemPlantRoom systemPlantRoom, string airSystemName, SAM.Core.Direction boundaryDirection, string junctionName)
        {
            if (systemPlantRoom == null || string.IsNullOrWhiteSpace(airSystemName))
            {
                return;
            }

            AirSystem airSystem = systemPlantRoom.GetSystem<AirSystem>(x => x.Name == airSystemName);
            if (airSystem == null)
            {
                return;
            }

            SystemType systemType = new SystemType(airSystem);

            // The boundary component carries an open connector in the boundary direction (the supply intake has an
            // open In; the extract discharge an open Out). A linear chain has exactly one such component.
            List<ISystemComponent> boundaryComponents = systemPlantRoom.GetSystemComponents<ISystemComponent>(airSystem, ConnectorStatus.Unconnected, boundaryDirection);
            boundaryComponents?.RemoveAll(x => x is ISystemConnection || x is SystemAirJunction);
            if (boundaryComponents == null || boundaryComponents.Count == 0)
            {
                return;
            }

            ISystemComponent boundaryComponent = boundaryComponents[0];
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
