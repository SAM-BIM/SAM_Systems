// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Core.Mollier;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        /// <summary>
        /// Builds a connected air-handling <see cref="SystemPlantRoom"/> from an ordered chain of Mollier processes.
        /// </summary>
        /// <remarks>
        /// Each process is mapped to a <see cref="ISystemComponent"/> via
        /// <see cref="Create.SystemComponent(IMollierProcess, double)"/>; processes that map to no component are
        /// skipped. The components are wired sequentially along a single <see cref="AirSystem"/> in process order,
        /// so the air leaves one component's Out connector and enters the next component's In connector.
        /// </remarks>
        /// <param name="mollierProcesses">Ordered psychrometric process chain.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="name">Plant room name.</param>
        /// <param name="airSystemName">Name of the air system the components are wired onto.</param>
        /// <returns>A connected <see cref="SystemPlantRoom"/>, or null when no components could be created.</returns>
        public static SystemPlantRoom SystemPlantRoom(this IEnumerable<IMollierProcess> mollierProcesses, double designAirflow = double.NaN, string name = "Plant Room", string airSystemName = "Air System")
        {
            if (mollierProcesses == null)
            {
                return null;
            }

            SystemPlantRoom systemPlantRoom = new SystemPlantRoom(name);
            AirSystem airSystem = new AirSystem(airSystemName);
            systemPlantRoom.Add(airSystem);

            ISystemComponent previous = null;
            int count = 0;
            foreach (IMollierProcess mollierProcess in mollierProcesses)
            {
                ISystemComponent current = mollierProcess.SystemComponent(designAirflow);
                if (current == null)
                {
                    continue;
                }

                systemPlantRoom.Add(current);

                if (previous != null)
                {
                    systemPlantRoom.Connect(previous, current, out _, airSystem);
                }

                previous = current;
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            return systemPlantRoom;
        }

        /// <summary>
        /// Builds a connected air-handling <see cref="SystemPlantRoom"/> from a <see cref="MollierGroup"/>.
        /// </summary>
        /// <param name="mollierGroup">Group holding the ordered psychrometric process chain.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="name">Plant room name. Defaults to the group name when available.</param>
        /// <returns>A connected <see cref="SystemPlantRoom"/>, or null.</returns>
        public static SystemPlantRoom SystemPlantRoom(this MollierGroup mollierGroup, double designAirflow = double.NaN, string name = null)
        {
            if (mollierGroup == null)
            {
                return null;
            }

            List<IMollierProcess> mollierProcesses = mollierGroup.GetObjects<IMollierProcess>();
            if (mollierProcesses == null || mollierProcesses.Count == 0)
            {
                return null;
            }

            string plantRoomName = name ?? (string.IsNullOrWhiteSpace(mollierGroup.Name) ? "Plant Room" : mollierGroup.Name);

            return SystemPlantRoom(mollierProcesses, designAirflow, plantRoomName);
        }
    }
}
