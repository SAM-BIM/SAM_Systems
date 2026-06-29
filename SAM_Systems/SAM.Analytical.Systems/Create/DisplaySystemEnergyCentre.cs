// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using SAM.Geometry.Systems;

namespace SAM.Analytical.Systems
{
    public static partial class Create
    {
        // Auto-layout spacing for the generated schematic, in symbol units. Symbols in the bundled library are
        // roughly 0.4-0.6 wide and their two air paths sit ~0.8 apart vertically, so a column step of 1.0 keeps a
        // visible gap between devices and a row step of 0.8 lines the supply and extract rows up with the two air
        // paths of a shared (twin-wheel) exchanger.
        private const double DisplaySystemColumnStep = 1.0;
        private const double DisplaySystemRowStep = 0.8;

        /// <summary>
        /// Converts a logical <see cref="SystemEnergyCentre"/> (e.g. the output of the Mollier bridge) into a
        /// previewable <see cref="DisplaySystemEnergyCentre"/>: every air-handling component is laid out and given
        /// a drawing symbol, and every connection is re-created as a routed <see cref="DisplaySystemConnection"/>.
        /// </summary>
        /// <param name="systemEnergyCentre">The logical energy centre to convert.</param>
        /// <param name="report">One line per component that could not be drawn (no symbol), for diagnostics.</param>
        /// <param name="displaySystemManager">Symbol library; when null the bundled default library is used.</param>
        /// <returns>A laid-out <see cref="DisplaySystemEnergyCentre"/>, or null when none could be built.</returns>
        public static DisplaySystemEnergyCentre DisplaySystemEnergyCentre(this SystemEnergyCentre systemEnergyCentre, out List<string> report, DisplaySystemManager displaySystemManager = null)
        {
            report = new List<string>();

            if (systemEnergyCentre == null)
            {
                return null;
            }

            if (displaySystemManager == null)
            {
                displaySystemManager = Query.DefaultDisplaySystemManager();
            }

            if (displaySystemManager == null)
            {
                report.Add("No DisplaySystemManager available (default symbol library could not be loaded).");
                return null;
            }

            DisplaySystemEnergyCentre result = new DisplaySystemEnergyCentre(systemEnergyCentre.Name);

            List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre.GetSystemPlantRooms();
            if (systemPlantRooms != null)
            {
                foreach (SystemPlantRoom systemPlantRoom in systemPlantRooms)
                {
                    DisplaySystemPlantRoom displaySystemPlantRoom = systemPlantRoom.DisplaySystemPlantRoom(out List<string> report_PlantRoom, displaySystemManager);
                    if (report_PlantRoom != null)
                    {
                        report.AddRange(report_PlantRoom);
                    }

                    if (displaySystemPlantRoom != null)
                    {
                        result.Add(displaySystemPlantRoom);
                    }
                }
            }

            // Energy sources carry no air-handling display geometry; preserve them unchanged.
            List<SystemEnergySource> systemEnergySources = systemEnergyCentre.GetSystemEnergySources();
            if (systemEnergySources != null)
            {
                foreach (SystemEnergySource systemEnergySource in systemEnergySources)
                {
                    result.Add(systemEnergySource);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a logical <see cref="SystemPlantRoom"/> into a previewable <see cref="DisplaySystemPlantRoom"/>:
        /// components are laid out one row per air system (in flow order) and connections are routed between the
        /// resulting symbols. Components whose type has no symbol are skipped and reported.
        /// </summary>
        public static DisplaySystemPlantRoom DisplaySystemPlantRoom(this SystemPlantRoom systemPlantRoom, out List<string> report, DisplaySystemManager displaySystemManager = null)
        {
            report = new List<string>();

            if (systemPlantRoom == null)
            {
                return null;
            }

            if (displaySystemManager == null)
            {
                displaySystemManager = Query.DefaultDisplaySystemManager();
            }

            if (displaySystemManager == null)
            {
                report.Add("No DisplaySystemManager available (default symbol library could not be loaded).");
                return null;
            }

            DisplaySystemPlantRoom result = new DisplaySystemPlantRoom(systemPlantRoom.Name);

            // 1. Lay out and convert each component. The map is keyed by the source component Guid so a component
            //    shared between air systems (e.g. a twin-wheel exchanger) is placed and converted exactly once.
            Dictionary<System.Guid, ISystemComponent> dictionary = new Dictionary<System.Guid, ISystemComponent>();

            List<ISystem> systems = systemPlantRoom.GetSystems();
            int row = 0;
            if (systems != null)
            {
                foreach (ISystem system in systems)
                {
                    List<ISystemComponent> orderedSystemComponents = OrderedSystemComponents(systemPlantRoom, system);
                    int column = 0;
                    foreach (ISystemComponent systemComponent in orderedSystemComponents)
                    {
                        System.Guid guid = Guid(systemComponent);
                        if (dictionary.ContainsKey(guid))
                        {
                            // Already placed on an earlier row (shared component); keep its position but advance the
                            // column so the rest of this row stays aligned with it.
                            column++;
                            continue;
                        }

                        SystemComponent systemComponent_Temp = systemComponent as SystemComponent;
                        if (systemComponent_Temp == null)
                        {
                            column++;
                            continue;
                        }

                        Point2D location = new Point2D(column * DisplaySystemColumnStep, -row * DisplaySystemRowStep);
                        IDisplaySystemObject displaySystemObject = Create.DisplayObject<IDisplaySystemObject>(systemComponent_Temp, location, displaySystemManager);
                        if (displaySystemObject == null)
                        {
                            report.Add($"No symbol for {systemComponent_Temp.GetType().Name} '{systemComponent_Temp.Name}' - component skipped (it will not be drawn).");
                            column++;
                            continue;
                        }

                        dictionary[guid] = (ISystemComponent)displaySystemObject;
                        column++;
                    }

                    row++;
                }
            }

            // 2. Re-create each logical connection as a routed display connection between the converted components.
            //    Membership is tracked per (air system, component) pair - not globally - so a component shared
            //    across air systems (e.g. a twin-wheel exchanger) is still related to every system it belongs to.
            HashSet<string> relatedToSystem = new HashSet<string>();
            List<ISystemConnection> systemConnections = systemPlantRoom.GetSystemConnections();
            if (systemConnections != null)
            {
                foreach (ISystemConnection systemConnection in systemConnections)
                {
                    List<ISystemComponent> endpoints = systemPlantRoom.GetRelatedObjects<ISystemComponent>(systemConnection);
                    endpoints?.RemoveAll(x => x is ISystemConnection);
                    if (endpoints == null || endpoints.Count < 2)
                    {
                        continue;
                    }

                    ISystemComponent systemComponent_1 = endpoints[0];
                    ISystemComponent systemComponent_2 = endpoints[1];

                    if (!dictionary.TryGetValue(Guid(systemComponent_1), out ISystemComponent displaySystemComponent_1) || !dictionary.TryGetValue(Guid(systemComponent_2), out ISystemComponent displaySystemComponent_2))
                    {
                        continue;
                    }

                    ISystem system = systemPlantRoom.GetRelatedObjects<ISystem>(systemConnection)?.FirstOrDefault();

                    int index_1 = -1;
                    int index_2 = -1;
                    systemConnection.TryGetIndex(systemComponent_1, out index_1);
                    systemConnection.TryGetIndex(systemComponent_2, out index_2);

                    if (result.Connect(displaySystemComponent_1, displaySystemComponent_2, out _, system, index_1, index_2) && system != null)
                    {
                        // Connect relates both endpoints to this system; record those pairs.
                        System.Guid systemGuid = SystemGuid(system);
                        relatedToSystem.Add(RelationKey(systemGuid, Guid(systemComponent_1)));
                        relatedToSystem.Add(RelationKey(systemGuid, Guid(systemComponent_2)));
                    }
                }
            }

            // 3. Relate each component to every air system it belongs to but is not yet related to (e.g. a
            //    single-component air system, or a shared component that is the lone component on one chain), so
            //    the display plant room mirrors the logical one's per-system membership.
            if (systems != null)
            {
                foreach (ISystem system in systems)
                {
                    List<ISystemComponent> systemComponents = systemPlantRoom.GetSystemComponents<ISystemComponent>(system);
                    if (systemComponents == null)
                    {
                        continue;
                    }

                    System.Guid systemGuid = SystemGuid(system);
                    foreach (ISystemComponent systemComponent in systemComponents)
                    {
                        if (systemComponent is ISystemConnection)
                        {
                            continue;
                        }

                        System.Guid guid = Guid(systemComponent);
                        if (relatedToSystem.Contains(RelationKey(systemGuid, guid)))
                        {
                            continue;
                        }

                        if (dictionary.TryGetValue(guid, out ISystemComponent displaySystemComponent))
                        {
                            result.Connect(system, displaySystemComponent);
                            relatedToSystem.Add(RelationKey(systemGuid, guid));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the components of <paramref name="system"/> in airflow (flow) order: the chain head (the
        /// component with an unconnected In connector) followed by its downstream Out-walk, with any unreached
        /// components appended. Connections are excluded.
        /// </summary>
        private static List<ISystemComponent> OrderedSystemComponents(SystemPlantRoom systemPlantRoom, ISystem system)
        {
            List<ISystemComponent> result = new List<ISystemComponent>();

            List<ISystemComponent> systemComponents = systemPlantRoom.GetSystemComponents<ISystemComponent>(system);
            systemComponents?.RemoveAll(x => x is ISystemConnection);
            if (systemComponents == null || systemComponents.Count == 0)
            {
                return result;
            }

            // Chain head: a component whose In connector for this air system is still unconnected.
            List<ISystemComponent> heads = systemPlantRoom.GetSystemComponents<ISystemComponent>(system, ConnectorStatus.Unconnected, SAM.Core.Direction.In);
            heads?.RemoveAll(x => x is ISystemConnection);
            ISystemComponent head = heads != null && heads.Count > 0 ? heads[0] : systemComponents[0];
            result.Add(head);

            List<ISystemComponent> orderedSystemComponents = systemPlantRoom.GetOrderedSystemComponents(head, system, SAM.Core.Direction.Out);
            if (orderedSystemComponents != null)
            {
                foreach (ISystemComponent systemComponent in orderedSystemComponents)
                {
                    if (!Contains(result, systemComponent))
                    {
                        result.Add(systemComponent);
                    }
                }
            }

            // Append anything the Out-walk did not reach (defensive: disconnected stubs).
            foreach (ISystemComponent systemComponent in systemComponents)
            {
                if (!Contains(result, systemComponent))
                {
                    result.Add(systemComponent);
                }
            }

            return result;
        }

        private static bool Contains(List<ISystemComponent> systemComponents, ISystemComponent systemComponent)
        {
            System.Guid guid = Guid(systemComponent);
            return systemComponents.Exists(x => Guid(x) == guid);
        }

        private static System.Guid Guid(ISystemComponent systemComponent)
        {
            return systemComponent is SAM.Core.SAMObject sAMObject ? sAMObject.Guid : System.Guid.Empty;
        }

        private static System.Guid SystemGuid(ISystem system)
        {
            return system is SAM.Core.SAMObject sAMObject ? sAMObject.Guid : System.Guid.Empty;
        }

        private static string RelationKey(System.Guid systemGuid, System.Guid componentGuid)
        {
            return systemGuid.ToString() + "|" + componentGuid.ToString();
        }
    }
}
