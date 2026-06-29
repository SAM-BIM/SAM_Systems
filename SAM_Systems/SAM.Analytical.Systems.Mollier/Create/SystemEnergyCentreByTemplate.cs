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
        /// Builds a <see cref="SystemEnergyCentre"/> by adding the air systems derived from the supply and extract
        /// Mollier chains into an existing plant-room <paramref name="template"/> (for example the
        /// Plantroom-Only.json energy centre).
        /// </summary>
        /// <remarks>
        /// The template supplies the simulation-ready plant: liquid systems, energy sources, boilers, chillers,
        /// pumps, controllers and so on. This bridge only creates the air side from the Mollier processes and
        /// copies it - air systems, their components and their (display) connections - into the template's plant
        /// room, so the result is a single energy centre that carries both. The supplied <paramref name="template"/>
        /// is never mutated (a copy is returned). When <paramref name="template"/> is null this degrades to wrapping
        /// the air-side plant room on its own, i.e. the original template-less behaviour.
        /// </remarks>
        /// <param name="supplyMollierProcesses">Ordered supply-side process chain.</param>
        /// <param name="extractMollierProcesses">Ordered extract-side process chain (twin-wheel), or null.</param>
        /// <param name="designSupplyAirflow">Design supply volumetric airflow [m3/s].</param>
        /// <param name="designExtractAirflow">Design extract volumetric airflow [m3/s].</param>
        /// <param name="name">Energy centre name; when empty the template's name is kept.</param>
        /// <param name="template">Base energy centre to merge the air systems into.</param>
        public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, double designSupplyAirflow, double designExtractAirflow, string name, SystemEnergyCentre template)
        {
            SystemPlantRoom airSystemPlantRoom = Create.SystemPlantRoom(supplyMollierProcesses, extractMollierProcesses, designSupplyAirflow, designExtractAirflow);

            return MergeAirSystems(template, airSystemPlantRoom, name);
        }

        /// <summary>
        /// Supply-only overload of
        /// <see cref="SystemEnergyCentre(IEnumerable{IMollierProcess}, IEnumerable{IMollierProcess}, double, double, string, SystemEnergyCentre)"/>.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, string name, SystemEnergyCentre template)
        {
            SystemPlantRoom airSystemPlantRoom = Create.SystemPlantRoom(mollierProcesses, designAirflow);

            return MergeAirSystems(template, airSystemPlantRoom, name);
        }

        /// <summary>
        /// Copies every air system (and its components and connections) from <paramref name="airSystemPlantRoom"/>
        /// into the first plant room of a copy of <paramref name="template"/>, leaving the template's existing
        /// plant equipment untouched. The supplied template is not mutated.
        /// </summary>
        private static SystemEnergyCentre MergeAirSystems(SystemEnergyCentre template, SystemPlantRoom airSystemPlantRoom, string name)
        {
            if (airSystemPlantRoom == null)
            {
                return null;
            }

            // No template: keep the original behaviour of wrapping the air-side plant room on its own.
            if (template == null)
            {
                SystemEnergyCentre result_NoTemplate = new SystemEnergyCentre(string.IsNullOrWhiteSpace(name) ? "Energy Centre" : name);
                result_NoTemplate.Add(airSystemPlantRoom);
                return result_NoTemplate;
            }

            // Work on a copy so the caller's template object is never changed.
            SystemEnergyCentre result = new SystemEnergyCentre(template);
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Name = name;
            }

            // Merge into the template's (first) plant room - that is where the liquid systems and plant equipment
            // live. GetSystemPlantRooms returns a detached clone, so CopyFrom mutates the clone and the corrected
            // plant room is re-Added (which replaces it in place under the same Guid).
            List<SystemPlantRoom> systemPlantRooms = result.GetSystemPlantRooms();
            SystemPlantRoom targetSystemPlantRoom = systemPlantRooms != null && systemPlantRooms.Count != 0 ? systemPlantRooms[0] : null;
            if (targetSystemPlantRoom == null)
            {
                // Template carries no plant room: just add the air-side plant room alongside.
                result.Add(airSystemPlantRoom);
                return result;
            }

            // CopyFrom transitively copies the seed object and its whole related sub-graph, so seeding from each
            // air system Guid pulls in that system together with its components and connections.
            List<ISystem> systems = airSystemPlantRoom.GetSystems();
            if (systems != null)
            {
                foreach (ISystem system in systems)
                {
                    if (system is SAM.Core.SAMObject sAMObject)
                    {
                        targetSystemPlantRoom.CopyFrom(airSystemPlantRoom, sAMObject.Guid);
                    }
                }
            }

            result.Add(targetSystemPlantRoom);

            return result;
        }
    }
}
