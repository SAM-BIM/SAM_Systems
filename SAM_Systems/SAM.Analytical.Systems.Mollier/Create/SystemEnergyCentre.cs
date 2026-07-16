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
            List<ConversionDiagnostic> _;
            return SystemEnergyCentre(mollierProcesses, designAirflow, name, out _);
        }

        /// <summary>
        /// Builds a simulation-ready <see cref="SystemEnergyCentre"/> from an ordered chain of Mollier processes
        /// and collects structured diagnostics.
        /// </summary>
        /// <param name="mollierProcesses">Ordered psychrometric process chain.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="name">Energy centre name.</param>
        /// <param name="diagnostics">Receives any diagnostics generated during conversion.</param>
        /// <returns>A <see cref="SystemEnergyCentre"/>, or null when no plant room could be built.</returns>
        public static SystemEnergyCentre SystemEnergyCentre(this IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, string name, out List<ConversionDiagnostic> diagnostics)
        {
            SystemPlantRoom systemPlantRoom = Create.SystemPlantRoom(mollierProcesses, designAirflow, "Plant Room", "Air System", out diagnostics);
            if (systemPlantRoom == null)
            {
                return null;
            }

            InjectLiquidSystems(systemPlantRoom, out List<SystemEnergySource> systemEnergySources, out List<ConversionDiagnostic> liquidDiagnostics);
            if (liquidDiagnostics != null && liquidDiagnostics.Count > 0)
            {
                diagnostics.AddRange(liquidDiagnostics);
            }

            SystemEnergyCentre systemEnergyCentre = new SystemEnergyCentre(name);
            systemEnergyCentre.Add(systemPlantRoom);
            AddSystemEnergySources(systemEnergyCentre, systemEnergySources);

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
            List<ConversionDiagnostic> _;
            return SystemEnergyCentre(mollierGroup, designAirflow, name, out _);
        }

        /// <summary>
        /// Builds a simulation-ready <see cref="SystemEnergyCentre"/> from a <see cref="MollierGroup"/>
        /// and collects structured diagnostics.
        /// </summary>
        /// <param name="mollierGroup">Group holding the ordered psychrometric process chain.</param>
        /// <param name="designAirflow">Design volumetric airflow [m3/s].</param>
        /// <param name="name">Energy centre name. Defaults to the group name when available.</param>
        /// <param name="diagnostics">Receives any diagnostics generated during conversion.</param>
        /// <returns>A <see cref="SystemEnergyCentre"/>, or null.</returns>
        public static SystemEnergyCentre SystemEnergyCentre(this MollierGroup mollierGroup, double designAirflow, string name, out List<ConversionDiagnostic> diagnostics)
        {
            SystemPlantRoom systemPlantRoom = Create.SystemPlantRoom(mollierGroup, designAirflow, null, out diagnostics);
            if (systemPlantRoom == null)
            {
                return null;
            }

            InjectLiquidSystems(systemPlantRoom, out List<SystemEnergySource> systemEnergySources, out List<ConversionDiagnostic> liquidDiagnostics);
            if (liquidDiagnostics != null && liquidDiagnostics.Count > 0)
            {
                diagnostics.AddRange(liquidDiagnostics);
            }

            string energyCentreName = name ?? (string.IsNullOrWhiteSpace(mollierGroup.Name) ? "Energy Centre" : mollierGroup.Name);

            SystemEnergyCentre systemEnergyCentre = new SystemEnergyCentre(energyCentreName);
            systemEnergyCentre.Add(systemPlantRoom);
            AddSystemEnergySources(systemEnergyCentre, systemEnergySources);

            return systemEnergyCentre;
        }

        /// <summary>
        /// Adds the default energy sources produced by liquid-system injection to the energy centre. Sources are
        /// energy-centre-level objects (SAM_Tas iterates GetSystemEnergySources() to create TPD FuelSources), so
        /// they cannot be added by the plant-room-level injector itself.
        /// </summary>
        internal static void AddSystemEnergySources(SystemEnergyCentre systemEnergyCentre, IEnumerable<SystemEnergySource> systemEnergySources)
        {
            if (systemEnergyCentre == null || systemEnergySources == null)
            {
                return;
            }

            foreach (SystemEnergySource systemEnergySource in systemEnergySources)
            {
                if (systemEnergySource != null)
                {
                    systemEnergyCentre.Add(systemEnergySource);
                }
            }
        }
    }
}
