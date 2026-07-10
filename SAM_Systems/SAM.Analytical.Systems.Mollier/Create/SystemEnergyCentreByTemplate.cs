// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using SAM.Geometry.Systems;

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

            if (template == null)
            {
                InjectLiquidSystems(airSystemPlantRoom, out _);
            }

            return MergeAirSystems(template, airSystemPlantRoom, name);
        }

        /// <summary>
        /// Supply-only overload of
        /// <see cref="SystemEnergyCentre(IEnumerable{IMollierProcess}, IEnumerable{IMollierProcess}, double, double, string, SystemEnergyCentre)"/>.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, string name, SystemEnergyCentre template)
        {
            SystemPlantRoom airSystemPlantRoom = Create.SystemPlantRoom(mollierProcesses, designAirflow);

            if (template == null)
            {
                InjectLiquidSystems(airSystemPlantRoom, out _);
            }

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

            // Place the air side clear of the template's existing plant so the two schematics do not overlap in
            // the viewport (the bridge lays the air side out from the origin, which is where the template plant
            // also sits). Only applies when both carry display geometry; otherwise the layout is left untouched.
            OffsetAirSideClearOfTemplate(targetSystemPlantRoom, airSystemPlantRoom);

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

        // Vertical gap (in symbol units) left between the template plant and the air side placed above it.
        private const double TemplateAirSideMargin = 2.0;

        /// <summary>
        /// Translates every display object of <paramref name="airSystemPlantRoom"/> so its laid-out schematic sits
        /// directly above the display extent of <paramref name="templateSystemPlantRoom"/> (left edges aligned),
        /// leaving a clear margin. No-ops when either side has no display geometry, so the logical fallback is
        /// untouched.
        /// </summary>
        private static void OffsetAirSideClearOfTemplate(SystemPlantRoom templateSystemPlantRoom, SystemPlantRoom airSystemPlantRoom)
        {
            BoundingBox2D templateBoundingBox2D = DisplayBoundingBox2D(templateSystemPlantRoom);
            BoundingBox2D airBoundingBox2D = DisplayBoundingBox2D(airSystemPlantRoom);
            if (templateBoundingBox2D == null || airBoundingBox2D == null)
            {
                return;
            }

            double offsetX = templateBoundingBox2D.Min.X - airBoundingBox2D.Min.X;
            double offsetY = (templateBoundingBox2D.Max.Y + TemplateAirSideMargin) - airBoundingBox2D.Min.Y;

            OffsetDisplayObjects(airSystemPlantRoom, new Vector2D(offsetX, offsetY));
        }

        /// <summary>
        /// Returns the combined 2D extent of every display object (components and connections) held by
        /// <paramref name="systemPlantRoom"/>, or null when none carry display geometry.
        /// </summary>
        private static BoundingBox2D DisplayBoundingBox2D(SystemPlantRoom systemPlantRoom)
        {
            List<ISystemComponent> systemComponents = systemPlantRoom?.GetSystemComponents<ISystemComponent>();
            if (systemComponents == null)
            {
                return null;
            }

            List<BoundingBox2D> boundingBox2Ds = new List<BoundingBox2D>();
            foreach (ISystemComponent systemComponent in systemComponents)
            {
                if (systemComponent is IDisplaySystemObject displaySystemObject)
                {
                    BoundingBox2D boundingBox2D = displaySystemObject.BoundingBox2D;
                    if (boundingBox2D != null)
                    {
                        boundingBox2Ds.Add(boundingBox2D);
                    }
                }
            }

            return boundingBox2Ds.Count == 0 ? null : new BoundingBox2D(boundingBox2Ds);
        }

        /// <summary>
        /// Moves every display object (components and connections) of <paramref name="systemPlantRoom"/> by
        /// <paramref name="vector2D"/>, persisting each moved object by re-adding it under its existing Guid so all
        /// relations are preserved (ISystemConnection is an ISystemComponent, so connections move with their
        /// components).
        /// </summary>
        private static void OffsetDisplayObjects(SystemPlantRoom systemPlantRoom, Vector2D vector2D)
        {
            if (systemPlantRoom == null || vector2D == null)
            {
                return;
            }

            List<ISystemComponent> systemComponents = systemPlantRoom.GetSystemComponents<ISystemComponent>();
            if (systemComponents == null)
            {
                return;
            }

            foreach (ISystemComponent systemComponent in systemComponents)
            {
                if (systemComponent is IDisplaySystemObject displaySystemObject && displaySystemObject.Move(vector2D))
                {
                    systemPlantRoom.Add(systemComponent);
                }
            }
        }
    }
}
