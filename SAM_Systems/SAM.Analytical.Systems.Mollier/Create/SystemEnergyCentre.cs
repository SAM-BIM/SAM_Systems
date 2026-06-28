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
        /// Builds a simulation-ready <see cref="SystemEnergyCentre"/> from an ordered chain of Mollier processes.
        /// </summary>
        /// <remarks>
        /// This is the top-level entry point of the Mollier to SAM_Systems bridge: the psychrometric process
        /// chain becomes a connected air-handling plant room wrapped in an energy centre, which can then be
        /// serialised to JSON and handed to the existing Tas TPD export path for annual simulation.
        /// </remarks>
        /// <param name="mollierProcesses">Ordered psychrometric process chain.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="name">Energy centre name.</param>
        /// <returns>A <see cref="SystemEnergyCentre"/>, or null when no plant room could be built.</returns>
        public static SystemEnergyCentre SystemEnergyCentre(this IEnumerable<IMollierProcess> mollierProcesses, double designAirflow = double.NaN, string name = "Energy Centre")
        {
            SystemPlantRoom systemPlantRoom = SystemPlantRoom(mollierProcesses, designAirflow);
            if (systemPlantRoom == null)
            {
                return null;
            }

            SystemEnergyCentre systemEnergyCentre = new SystemEnergyCentre(name);
            systemEnergyCentre.Add(systemPlantRoom);

            return systemEnergyCentre;
        }

        /// <summary>
        /// Builds a simulation-ready <see cref="SystemEnergyCentre"/> from a <see cref="MollierGroup"/>.
        /// </summary>
        /// <param name="mollierGroup">Group holding the ordered psychrometric process chain.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="name">Energy centre name. Defaults to the group name when available.</param>
        /// <returns>A <see cref="SystemEnergyCentre"/>, or null.</returns>
        public static SystemEnergyCentre SystemEnergyCentre(this MollierGroup mollierGroup, double designAirflow = double.NaN, string name = null)
        {
            if (mollierGroup == null)
            {
                return null;
            }

            SystemPlantRoom systemPlantRoom = SystemPlantRoom(mollierGroup, designAirflow);
            if (systemPlantRoom == null)
            {
                return null;
            }

            string energyCentreName = name ?? (string.IsNullOrWhiteSpace(mollierGroup.Name) ? "Energy Centre" : mollierGroup.Name);

            SystemEnergyCentre systemEnergyCentre = new SystemEnergyCentre(energyCentreName);
            systemEnergyCentre.Add(systemPlantRoom);

            return systemEnergyCentre;
        }
    }
}
