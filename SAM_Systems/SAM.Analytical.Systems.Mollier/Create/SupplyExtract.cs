// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        /// <summary>
        /// Builds a <see cref="SystemPlantRoom"/> from a supply and an extract chain of Mollier processes,
        /// sharing a single heat-recovery exchanger across both air paths (e.g. a twin-wheel unit).
        /// </summary>
        /// <remarks>
        /// Both the supply chain and the extract chain are wired onto a single, shared <see cref="AirSystem"/> —
        /// one combined <see cref="AirSystem"/> carries both sides of the AHU (supply and extract), not two
        /// separate supply/extract systems. A <see cref="SystemExchanger"/> exposes two air paths (connection
        /// indexes 1 and 2); when both chains contain heat-recovery processes, the supply-side exchanger
        /// instances are reused on the extract side, paired in order. Because <see cref="SystemPlantRoom.Connect"/>
        /// auto-selects the first unconnected connector pair, the supply chain consumes air path 1 and the
        /// extract chain then consumes air path 2 of the same device — modelling sensible + latent recovery as a
        /// single exchanger rather than two. If the heat-recovery counts differ between the chains, surplus
        /// extract heat-recovery processes fall back to their own exchanger.
        /// </remarks>
        /// <param name="supplyMollierProcesses">Ordered supply-side process chain.</param>
        /// <param name="extractMollierProcesses">Ordered extract-side process chain.</param>
        /// <param name="designSupplyAirflow">Design supply volumetric airflow [m3/s].</param>
        /// <param name="designExtractAirflow">Design extract volumetric airflow [m3/s].</param>
        /// <param name="name">Plant room name.</param>
        /// <returns>A connected <see cref="SystemPlantRoom"/>, or null when no components could be created.</returns>
        public static SystemPlantRoom SystemPlantRoom(IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, double designSupplyAirflow = double.NaN, double designExtractAirflow = double.NaN, string name = "Plant Room")
        {
            List<ConversionDiagnostic> _;
            return SystemPlantRoom(supplyMollierProcesses, extractMollierProcesses, designSupplyAirflow, designExtractAirflow, name, out _);
        }

        /// <summary>
        /// Builds a <see cref="SystemPlantRoom"/> from a supply and an extract chain of Mollier processes
        /// and collects structured diagnostics.
        /// </summary>
        public static SystemPlantRoom SystemPlantRoom(IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, double designSupplyAirflow, double designExtractAirflow, string name, out List<ConversionDiagnostic> diagnostics)
        {
            diagnostics = new List<ConversionDiagnostic>();

            if (supplyMollierProcesses == null && extractMollierProcesses == null)
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error, DiagnosticCodes.NullProcessChain, "Both supply and extract process chains are null.", null));
                return null;
            }

            SystemPlantRoom systemPlantRoom = new SystemPlantRoom(name);

            // Supply and extract streams belong to a single air system (both sides of one AHU), not two separate
            // air systems.
            AirSystem airSystem = new AirSystem("Air System");
            systemPlantRoom.Add(airSystem);

            // Supply chain.
            List<SystemExchanger> supplyExchangers = new List<SystemExchanger>();
            int supplyCount = AddChain(systemPlantRoom, supplyMollierProcesses, designSupplyAirflow, airSystem, null, supplyExchangers, out ISystemComponent supplyFirst, out ISystemComponent supplyLast, diagnostics);

            // Extract chain on the same air system, reusing the supply-side exchangers (in order) so a twin-wheel is
            // one device.
            int extractCount = AddChain(systemPlantRoom, extractMollierProcesses, designExtractAirflow, airSystem, supplyExchangers, null, out ISystemComponent extractFirst, out ISystemComponent extractLast, diagnostics);

            // With both air paths known, derive each shared exchanger's sensible/latent effectiveness.
            ApplyHeatRecoveryEfficiencies(systemPlantRoom, supplyMollierProcesses, extractMollierProcesses, supplyExchangers, diagnostics);

            if (supplyCount == 0 && extractCount == 0)
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error, DiagnosticCodes.ChainEmpty, "Process chain is empty; no components were created.", null));
                return null;
            }

            // Cap the outside-air boundaries with explicit junctions: fresh air feeds the supply intake (the first
            // supply component's open In); exhaust air terminates the extract discharge (the last extract
            // component's open Out). The room-side boundaries (supply discharge, extract intake) are left open.
            AddBoundaryJunction(systemPlantRoom, airSystem, supplyFirst, SAM.Core.Direction.In, "Junction Fresh Air", diagnostics);
            AddBoundaryJunction(systemPlantRoom, airSystem, extractLast, SAM.Core.Direction.Out, "Junction Exhaust Air", diagnostics);

            // Room-side arrangement: close the loop supply discharge -> Group Junction -> Damper -> Room -> Group
            // Junction -> extract intake. The room condition is the start (room-side) point of the extract chain.
            if (supplyLast != null && extractFirst != null)
            {
                AddRoom(systemPlantRoom, airSystem, supplyLast, extractFirst, FirstStart(extractMollierProcesses), diagnostics);
            }

            // Promote the logical plant room to a display (drawable) plant room so the bridge output can be
            // previewed/baked directly: each component becomes its DisplaySystem* equivalent (symbol + auto
            // layout) and each connection a routed polyline. Falls back to the logical room if no symbol library
            // is available. DisplaySystem* are subclasses of their System* types, so simulation/export is
            // unaffected.
            SystemPlantRoom displaySystemPlantRoom = ToDisplaySystemPlantRoom(systemPlantRoom, diagnostics);

            // Wrap the laid-out room-side items (Room, Damper, Group Junctions) in a DisplayAirSystemGroup.
            AddDisplayAirSystemGroup(displaySystemPlantRoom, airSystem, diagnostics);

            return displaySystemPlantRoom;
        }

        /// <summary>
        /// Converts a logical <see cref="SystemPlantRoom"/> into a drawable <see cref="DisplaySystemPlantRoom"/>
        /// using the bundled default symbol library, so the bridge output is viewable out of the box. Returns the
        /// original logical plant room unchanged when no symbol library is available.
        /// </summary>
        private static SystemPlantRoom ToDisplaySystemPlantRoom(SystemPlantRoom systemPlantRoom, List<ConversionDiagnostic> diagnostics)
        {
            if (systemPlantRoom == null)
            {
                return null;
            }

            DisplaySystemManager displaySystemManager = SAM.Analytical.Systems.Query.DefaultDisplaySystemManager();
            if (displaySystemManager == null)
            {
                if (diagnostics != null)
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Info, DiagnosticCodes.DisplaySymbolMissing, "No DisplaySystemManager available (default symbol library could not be loaded); logical plant room returned.", null));
                }
                return systemPlantRoom;
            }

            DisplaySystemPlantRoom displaySystemPlantRoom = SAM.Analytical.Systems.Create.DisplaySystemPlantRoom(systemPlantRoom, out List<string> report, displaySystemManager);
            if (diagnostics != null && report != null)
            {
                foreach (string line in report)
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Info, DiagnosticCodes.DisplaySymbolMissing, line, null));
                }
            }

            return displaySystemPlantRoom ?? systemPlantRoom;
        }

        /// <summary>
        /// Builds a <see cref="SystemEnergyCentre"/> from a supply and extract Mollier process chain
        /// (twin-wheel aware). See <see cref="SystemPlantRoom(IEnumerable{IMollierProcess}, IEnumerable{IMollierProcess}, double, double, string)"/>.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, double designSupplyAirflow = double.NaN, double designExtractAirflow = double.NaN, string name = "Energy Centre")
        {
            List<ConversionDiagnostic> _;
            return SystemEnergyCentre(supplyMollierProcesses, extractMollierProcesses, designSupplyAirflow, designExtractAirflow, name, out _);
        }

        /// <summary>
        /// Builds a <see cref="SystemEnergyCentre"/> from a supply and extract Mollier process chain
        /// and collects structured diagnostics.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, double designSupplyAirflow, double designExtractAirflow, string name, out List<ConversionDiagnostic> diagnostics)
        {
            SystemPlantRoom systemPlantRoom = Create.SystemPlantRoom(supplyMollierProcesses, extractMollierProcesses, designSupplyAirflow, designExtractAirflow, "Plant Room", out diagnostics);
            if (systemPlantRoom == null)
            {
                return null;
            }

            InjectLiquidSystems(systemPlantRoom, out List<SystemEnergySource> systemEnergySources, out List<ConversionDiagnostic> liquidDiagnostics);
            if (liquidDiagnostics != null && liquidDiagnostics.Count > 0)
            {
                diagnostics.AddRange(liquidDiagnostics);
            }

            SystemEnergyCentre systemEnergyCentre = new SystemEnergyCentre(string.IsNullOrWhiteSpace(name) ? "Energy Centre" : name);
            systemEnergyCentre.Add(systemPlantRoom);
            AddSystemEnergySources(systemEnergyCentre, systemEnergySources);

            return systemEnergyCentre;
        }

        /// <summary>
        /// Maps and wires one ordered process chain onto its own air system inside the plant room.
        /// When <paramref name="reusableExchangers"/> is supplied, heat-recovery processes reuse those exchanger
        /// instances (in order) instead of creating new ones; when <paramref name="createdExchangers"/> is supplied,
        /// newly created exchangers are appended to it.
        /// </summary>
        private static int AddChain(SystemPlantRoom systemPlantRoom, IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, AirSystem airSystem, List<SystemExchanger> reusableExchangers, List<SystemExchanger> createdExchangers, out ISystemComponent firstComponent, out ISystemComponent lastComponent, List<ConversionDiagnostic> diagnostics)
        {
            firstComponent = null;
            lastComponent = null;

            if (mollierProcesses == null || airSystem == null)
            {
                return 0;
            }

            // Idempotent: Add stores/replaces by Guid, so re-adding the shared air system on the second chain is a
            // no-op. Both the supply and extract chains are wired onto this single air system.
            systemPlantRoom.Add(airSystem);

            ISystemComponent previous = null;
            int count = 0;
            int reuseIndex = 0;
            foreach (IMollierProcess mollierProcess in mollierProcesses)
            {
                ISystemComponent current;

                if (mollierProcess is HeatRecoveryProcess && reusableExchangers != null && reuseIndex < reusableExchangers.Count)
                {
                    // Reuse the physical exchanger from the other chain; its second air path is wired here.
                    current = reusableExchangers[reuseIndex];
                    reuseIndex++;
                }
                else
                {
                    current = mollierProcess.SystemComponent(designAirflow, out List<ConversionDiagnostic> processDiagnostics);
                    if (diagnostics != null && processDiagnostics != null)
                    {
                        diagnostics.AddRange(processDiagnostics);
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    systemPlantRoom.Add(current);

                    if (current is SystemExchanger systemExchanger)
                    {
                        createdExchangers?.Add(systemExchanger);
                    }
                }

                if (previous != null)
                {
                    // Air flows previous.Out -> current.In. Resolve those connectors explicitly: auto-selection
                    // (-1) takes each component's first unconnected connector, and because air components define
                    // their In connector before Out it would wire previous.In -> current.Out, leaving the
                    // upstream Out connector free so Direction.Out ordering (GetOrderedSystemComponents) and the
                    // export/conversion paths cannot follow the generated chain. Picking the first UNCONNECTED
                    // Out/In also routes the shared twin-wheel exchanger's second air path onto its second
                    // connector pair instead of colliding with the first. If a directional connector cannot be
                    // resolved, fall back to auto-selection rather than skip the link.
                    SystemType systemType = new SystemType(airSystem);
                    int index_Out = UnconnectedIndex(systemPlantRoom, previous, systemType, SAM.Core.Direction.Out);
                    int index_In = UnconnectedIndex(systemPlantRoom, current, systemType, SAM.Core.Direction.In);
                    if (index_Out != -1 && index_In != -1)
                    {
                        bool connected = systemPlantRoom.Connect(previous, current, out _, airSystem, index_Out, index_In);
                        if (!connected && diagnostics != null)
                        {
                            diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.ConnectionFailed, $"Failed to connect '{ComponentName(previous)}' to '{ComponentName(current)}' (Out {index_Out} -> In {index_In}).", mollierProcess));
                        }
                    }
                    else
                    {
                        if (diagnostics != null)
                        {
                            diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.ConnectionFailed, $"Directional connector could not be resolved between '{ComponentName(previous)}' and '{ComponentName(current)}'; fell back to automatic connector selection.", mollierProcess));
                        }

                        bool connected = systemPlantRoom.Connect(previous, current, out _, airSystem);
                        if (!connected && diagnostics != null)
                        {
                            diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.ConnectionFailed, $"Automatic connector fallback failed to connect '{ComponentName(previous)}' to '{ComponentName(current)}'.", mollierProcess));
                        }
                    }
                }
                else
                {
                    // Relate the first component to its air system explicitly. For multi-component chains the
                    // pairwise Connect above also relates each component to the system, but a single-component
                    // chain never reaches that call, leaving the lone component unrelated to the air system so
                    // GetSystemComponents<T>(ISystem) (and the export/conversion paths built on it) see an empty
                    // plant room.
                    bool related = systemPlantRoom.Connect(airSystem, current);
                    if (!related && diagnostics != null)
                    {
                        diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.ConnectionFailed, $"Failed to relate '{ComponentName(current)}' to air system '{airSystem.Name}'.", mollierProcess));
                    }
                }

                if (firstComponent == null)
                {
                    firstComponent = current;
                }
                lastComponent = current;

                previous = current;
                count++;
            }

            return count;
        }

        /// <summary>
        /// Returns the first currently-unconnected connector index of the given <paramref name="direction"/> for
        /// the air <paramref name="systemType"/> on <paramref name="systemComponent"/>, or -1 if there is none.
        /// </summary>
        private static int UnconnectedIndex(SystemPlantRoom systemPlantRoom, ISystemComponent systemComponent, SystemType systemType, SAM.Core.Direction direction)
        {
            List<int> indexes = systemPlantRoom?.Indexes(systemComponent, systemType, ConnectorStatus.Unconnected, direction);
            return indexes != null && indexes.Count > 0 ? indexes[0] : -1;
        }

        /// <summary>
        /// Best-effort display name for a component, for diagnostic messages: the object's own <c>Name</c> when
        /// available, otherwise its runtime type name.
        /// </summary>
        private static string ComponentName(ISystemComponent systemComponent)
        {
            return (systemComponent as SystemObject)?.Name ?? systemComponent?.GetType()?.Name ?? "?";
        }

        /// <summary>
        /// Emits a <see cref="DiagnosticCodes.ConnectionFailed"/> warning naming both endpoints when
        /// <paramref name="connected"/> is false. No-ops when <paramref name="diagnostics"/> is null.
        /// </summary>
        private static void CheckConnect(bool connected, ISystemComponent from, ISystemComponent to, List<ConversionDiagnostic> diagnostics)
        {
            if (connected || diagnostics == null)
            {
                return;
            }

            diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.ConnectionFailed, $"Failed to connect '{ComponentName(from)}' to '{ComponentName(to)}'.", null));
        }

        /// <summary>
        /// Sets sensible (and, where moisture is transferred, latent) effectiveness on each shared heat-recovery
        /// exchanger, by pairing the supply and extract heat-recovery processes in order.
        /// </summary>
        /// <remarks>
        /// The supply-side exchangers were created in supply-chain order, one per HeatRecoveryProcess, so they
        /// correspond index-for-index with the ordered supply heat-recovery processes; the extract heat-recovery
        /// processes provide the second (exhaust) air path. See <see cref="Query.HeatRecoveryEfficiencies"/>.
        /// <para>
        /// <paramref name="exchangers"/> holds the pre-Add instances, but <see cref="SystemPlantRoom.Add(ISystemComponent)"/>
        /// stores a clone, so the efficiencies are written back to the stored exchanger (looked up by Guid) rather
        /// than to the detached original — mutating the original alone would never reach the plant room, the JSON
        /// or the energy centre. Re-adding the corrected exchanger replaces it in place under the same Guid,
        /// leaving its connections intact.
        /// </para>
        /// </remarks>
        private static void ApplyHeatRecoveryEfficiencies(SystemPlantRoom systemPlantRoom, IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, List<SystemExchanger> exchangers, List<ConversionDiagnostic> diagnostics)
        {
            if (systemPlantRoom == null || exchangers == null || exchangers.Count == 0 || supplyMollierProcesses == null || extractMollierProcesses == null)
            {
                return;
            }

            List<HeatRecoveryProcess> supplyHeatRecoveries = supplyMollierProcesses.OfType<HeatRecoveryProcess>().ToList();
            List<HeatRecoveryProcess> extractHeatRecoveries = extractMollierProcesses.OfType<HeatRecoveryProcess>().ToList();

            if (diagnostics != null && supplyHeatRecoveries.Count != extractHeatRecoveries.Count)
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.HeatRecoveryCountMismatch, $"Supply chain has {supplyHeatRecoveries.Count} heat-recovery processes but extract chain has {extractHeatRecoveries.Count}; surplus processes get their own exchanger.", null));
            }

            int count = System.Math.Min(exchangers.Count, System.Math.Min(supplyHeatRecoveries.Count, extractHeatRecoveries.Count));
            for (int i = 0; i < count; i++)
            {
                SystemExchanger systemExchanger = exchangers[i];
                if (systemExchanger == null)
                {
                    continue;
                }

                System.Guid guid = systemExchanger.Guid;
                SystemExchanger systemExchanger_Stored = systemPlantRoom.GetSystemComponent<SystemExchanger>(x => x.Guid == guid);
                if (systemExchanger_Stored == null)
                {
                    continue;
                }

                supplyHeatRecoveries[i].HeatRecoveryEfficiencies(extractHeatRecoveries[i], out double sensibleEfficiency, out double latentEfficiency);

                if (double.IsNaN(sensibleEfficiency) && double.IsNaN(latentEfficiency) && diagnostics != null)
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.HeatRecoveryEfficiencyNotAvailable, $"Heat-recovery exchanger '{systemExchanger_Stored.Name}' has no usable sensible or latent effectiveness.", null));
                }

                bool modified = false;
                if (!double.IsNaN(sensibleEfficiency))
                {
                    systemExchanger_Stored.SensibleEfficiency = sensibleEfficiency;
                    modified = true;
                }

                if (!double.IsNaN(latentEfficiency))
                {
                    systemExchanger_Stored.LatentEfficiency = latentEfficiency;
                    systemExchanger_Stored.ExchangerLatentType = ExchangerLatentType.HumidityRatio;
                    modified = true;
                }

                if (modified)
                {
                    // Re-add under the same Guid: replaces the stored exchanger in place, relations preserved.
                    systemPlantRoom.Add(systemExchanger_Stored);
                }
            }
        }
    }
}
