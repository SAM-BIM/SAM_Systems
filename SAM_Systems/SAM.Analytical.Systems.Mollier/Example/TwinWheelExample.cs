// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    /// <summary>
    /// Worked example and framework-free self-check for the Mollier to SAM_Systems bridge, built around a
    /// twin-wheel (latent + sensible recovery) air-handling unit.
    /// </summary>
    /// <remarks>
    /// <see cref="MollierProcesses"/> builds the supply and extract psychrometric chains; <see cref="Create"/>
    /// converts them to a connected <see cref="SystemEnergyCentre"/>; and <see cref="Verify"/> runs a set of
    /// deterministic checks (component creation, single plant room, JSON round-trip, cooling duty and bypass
    /// factor) so the bridge can be smoke-tested without a unit-test framework. This mirrors the worked example
    /// in docs/Mollier-Systems-Bridge.md.
    /// </remarks>
    public static class TwinWheelExample
    {
        /// <summary>Standard sea-level atmospheric pressure [Pa].</summary>
        public const double Pressure = 101325;

        /// <summary>Default design supply airflow used by the example [m3/s].</summary>
        public const double DefaultSupplyAirflow = 2.5;

        /// <summary>Default design extract airflow used by the example [m3/s].</summary>
        public const double DefaultExtractAirflow = 2.3;

        /// <summary>
        /// Builds the supply and extract Mollier process chains for the twin-wheel AHU.
        /// Each process starts where the previous one ended (process.End -> next process.Start).
        /// </summary>
        public static void MollierProcesses(out List<IMollierProcess> supplyMollierProcesses, out List<IMollierProcess> extractMollierProcesses)
        {
            // Summer design states.
            MollierPoint outdoor = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(32, 40, Pressure);
            MollierPoint room = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(24, 50, Pressure);

            // Supply chain: heat recovery -> cooling -> reheat -> supply fan.
            HeatRecoveryProcess supplyHeatRecovery = outdoor.HeatRecoveryProcess_Supply(room, 75, 65); // sensible 75%, latent 65%
            CoolingProcess cooling = supplyHeatRecovery.End.CoolingProcess(13, 0.85);               // off-coil 13 C, efficiency 0.85
            HeatingProcess reheat = cooling.End.HeatingProcess(16);                                 // reheat to 16 C
            FanProcess supplyFan = reheat.End.FanProcess(0.8);                                      // specific fan temperature rise

            supplyMollierProcesses = new List<IMollierProcess> { supplyHeatRecovery, cooling, reheat, supplyFan };

            // Extract chain: same wheel (exhaust side) -> extract fan.
            HeatRecoveryProcess extractHeatRecovery = room.HeatRecoveryProcess_Extract(outdoor, 75, 65);
            FanProcess extractFan = extractHeatRecovery.End.FanProcess(0.8);

            extractMollierProcesses = new List<IMollierProcess> { extractHeatRecovery, extractFan };
        }

        /// <summary>
        /// Builds the twin-wheel <see cref="SystemEnergyCentre"/> from the example Mollier process chains.
        /// </summary>
        public static SystemEnergyCentre Create(double supplyAirflow = DefaultSupplyAirflow, double extractAirflow = DefaultExtractAirflow)
        {
            MollierProcesses(out List<IMollierProcess> supplyMollierProcesses, out List<IMollierProcess> extractMollierProcesses);

            return SAM.Analytical.Systems.Mollier.Create.SystemEnergyCentre(supplyMollierProcesses, extractMollierProcesses, supplyAirflow, extractAirflow, "Twin-Wheel AHU");
        }

        /// <summary>
        /// Builds the twin-wheel system and converts it into a previewable <see cref="DisplaySystemEnergyCentre"/>
        /// (laid-out symbols + routed connections), using the bundled default symbol library.
        /// </summary>
        /// <param name="report">One line per component that has no symbol (skipped).</param>
        /// <param name="supplyAirflow">Design supply airflow [m3/s].</param>
        /// <param name="extractAirflow">Design extract airflow [m3/s].</param>
        public static DisplaySystemEnergyCentre CreateDisplay(out List<string> report, double supplyAirflow = DefaultSupplyAirflow, double extractAirflow = DefaultExtractAirflow)
        {
            SystemEnergyCentre systemEnergyCentre = Create(supplyAirflow, extractAirflow);

            // Fully-qualified: the local Mollier Create has no DisplaySystemEnergyCentre overload.
            return SAM.Analytical.Systems.Create.DisplaySystemEnergyCentre(systemEnergyCentre, out report);
        }

        /// <summary>
        /// Runs deterministic checks over the generated twin-wheel system and reports the outcome.
        /// </summary>
        /// <param name="messages">One PASS/FAIL line per check.</param>
        /// <returns>True when every check passes.</returns>
        public static bool Verify(out List<string> messages)
        {
            messages = new List<string>();
            bool result = true;

            SystemEnergyCentre systemEnergyCentre = Create();
            Check(ref result, messages, systemEnergyCentre != null, "SystemEnergyCentre created");
            if (systemEnergyCentre == null)
            {
                return false;
            }

            List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre.GetSystemPlantRooms();
            Check(ref result, messages, systemPlantRooms != null && systemPlantRooms.Count == 1, "Exactly one plant room generated");

            // JSON round-trip: serialise then reload and confirm the structure survives.
            JsonObject jsonObject = systemEnergyCentre.ToJsonObject();
            Check(ref result, messages, jsonObject != null, "SystemEnergyCentre serialises to JSON");

            if (jsonObject != null)
            {
                SystemEnergyCentre systemEnergyCentre_RoundTrip = new SystemEnergyCentre(jsonObject);
                List<SystemPlantRoom> systemPlantRooms_RoundTrip = systemEnergyCentre_RoundTrip?.GetSystemPlantRooms();
                Check(ref result, messages, systemPlantRooms_RoundTrip != null && systemPlantRooms_RoundTrip.Count == 1, "JSON round-trip preserves the plant room");

                // The derived recovery effectiveness must survive into the STORED exchanger and the JSON, not just
                // sit on a detached object: SystemPlantRoom.Add clones the component, so the efficiencies are read
                // back here from the round-tripped energy centre.
                SystemExchanger systemExchanger_RoundTrip = systemPlantRooms_RoundTrip?.FirstOrDefault()?.GetSystemComponents<SystemExchanger>()?.FirstOrDefault();
                Check(ref result, messages, systemExchanger_RoundTrip != null, "Round-trip plant room contains the shared exchanger");
                Check(ref result, messages, systemExchanger_RoundTrip?.SensibleEfficiency != null && systemExchanger_RoundTrip.SensibleEfficiency.Value > 0 && systemExchanger_RoundTrip.SensibleEfficiency.Value <= 1, $"Stored exchanger keeps sensible effectiveness (got {systemExchanger_RoundTrip?.SensibleEfficiency?.Value:0.###})");
                Check(ref result, messages, systemExchanger_RoundTrip?.LatentEfficiency != null && systemExchanger_RoundTrip.LatentEfficiency.Value > 0 && systemExchanger_RoundTrip.LatentEfficiency.Value <= 1, $"Stored exchanger keeps latent effectiveness (got {systemExchanger_RoundTrip?.LatentEfficiency?.Value:0.###})");
            }

            // A single-component chain must still relate its component to the air system, otherwise
            // GetSystemComponents<T>(ISystem) (and the export paths built on it) would see an empty plant room.
            MollierPoint singleOutdoor = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(2, 80, Pressure);
            HeatingProcess singleHeating = singleOutdoor.HeatingProcess(28);
            SystemPlantRoom singlePlantRoom = (new List<IMollierProcess> { singleHeating }).SystemPlantRoom(DefaultSupplyAirflow, "Single-Component Plant Room", "Single Air System");
            List<ISystem> singleSystems = singlePlantRoom?.GetSystems();
            bool singleRelated = singleSystems != null && singleSystems.Count != 0 && singleSystems.TrueForAll(x => singlePlantRoom.GetSystemComponents<ISystemComponent>(x)?.Count > 0);
            Check(ref result, messages, singleRelated, "Single-component chain relates its component to the air system");

            // Cooling-coil duty and bypass factor derived from the process states.
            MollierProcesses(out List<IMollierProcess> supplyMollierProcesses, out List<IMollierProcess> extractMollierProcesses);
            CoolingProcess coolingProcess = supplyMollierProcesses.OfType<CoolingProcess>().FirstOrDefault();
            Check(ref result, messages, coolingProcess != null, "Supply chain contains a cooling process");

            if (coolingProcess != null)
            {
                double bypassFactor = coolingProcess.BypassFactor();
                Check(ref result, messages, !double.IsNaN(bypassFactor) && bypassFactor >= 0 && bypassFactor <= 1, $"Cooling bypass factor within [0,1] (got {bypassFactor:0.###})");

                double duty = coolingProcess.Duty(DefaultSupplyAirflow);
                Check(ref result, messages, !double.IsNaN(duty) && duty > 0, $"Cooling duty positive (got {duty:0.} W)");
            }

            // Twin-wheel recovery effectiveness derived from both air paths.
            HeatRecoveryProcess supplyHeatRecovery = supplyMollierProcesses.OfType<HeatRecoveryProcess>().FirstOrDefault();
            HeatRecoveryProcess extractHeatRecovery = extractMollierProcesses.OfType<HeatRecoveryProcess>().FirstOrDefault();
            Check(ref result, messages, supplyHeatRecovery != null && extractHeatRecovery != null, "Both chains contain a heat-recovery process");

            if (supplyHeatRecovery != null && extractHeatRecovery != null)
            {
                supplyHeatRecovery.HeatRecoveryEfficiencies(extractHeatRecovery, out double sensibleEfficiency, out double latentEfficiency);
                Check(ref result, messages, !double.IsNaN(sensibleEfficiency) && sensibleEfficiency > 0 && sensibleEfficiency <= 1, $"Sensible recovery effectiveness within (0,1] (got {sensibleEfficiency:0.###})");
                Check(ref result, messages, !double.IsNaN(latentEfficiency) && latentEfficiency >= 0 && latentEfficiency <= 1, $"Latent recovery effectiveness within [0,1] (got {latentEfficiency:0.###})");
            }

            // The chain must be followable in the airflow (Out) direction: components have to be wired
            // previous.Out -> current.In, not In -> Out. GetOrderedSystemComponents excludes the start, so the
            // supply chain (HR -> cooling -> reheat -> fan) walks 3 downstream hops in the Out direction; the
            // In<->Out wiring bug would leave each component's Out connector free and cap every walk at a single
            // component (0 downstream hops).
            SystemPlantRoom plantRoom = systemPlantRooms?.FirstOrDefault();
            int maxOrdered = 0;
            if (plantRoom != null)
            {
                foreach (ISystem airSystem in plantRoom.GetSystems() ?? new List<ISystem>())
                {
                    List<ISystemComponent> systemComponents = plantRoom.GetSystemComponents<ISystemComponent>(airSystem);
                    if (systemComponents == null)
                    {
                        continue;
                    }

                    foreach (ISystemComponent systemComponent in systemComponents)
                    {
                        List<ISystemComponent> ordered = plantRoom.GetOrderedSystemComponents(systemComponent, airSystem, SAM.Core.Direction.Out);
                        if (ordered != null && ordered.Count > maxOrdered)
                        {
                            maxOrdered = ordered.Count;
                        }
                    }
                }
            }
            // Supply chain = 4 components in flow order, so the head's Out-walk reaches the other 3.
            Check(ref result, messages, maxOrdered >= 3, $"Chain followable in airflow (Out) direction (longest Out-walk reached {maxOrdered} downstream components)");

            // Display conversion: the logical energy centre must convert to a previewable schematic in which
            // every component carries drawable 2D geometry and no connection is lost. This is the check that
            // would have caught the "no preview in Grasshopper" symptom the bridge originally had.
            DisplaySystemEnergyCentre displaySystemEnergyCentre = SAM.Analytical.Systems.Create.DisplaySystemEnergyCentre(systemEnergyCentre, out List<string> displayReport);
            Check(ref result, messages, displaySystemEnergyCentre != null, "DisplaySystemEnergyCentre created");

            DisplaySystemPlantRoom displaySystemPlantRoom = displaySystemEnergyCentre?.GetSystemPlantRooms()?.FirstOrDefault();
            Check(ref result, messages, displaySystemPlantRoom != null, "Display plant room generated");

            SystemPlantRoom logicalPlantRoom = systemEnergyCentre.GetSystemPlantRooms()?.FirstOrDefault();
            if (displaySystemPlantRoom != null && logicalPlantRoom != null)
            {
                List<ISystemComponent> logicalComponents = logicalPlantRoom.GetSystemComponents<ISystemComponent>();
                logicalComponents?.RemoveAll(x => x is ISystemConnection);
                int logicalComponentCount = logicalComponents?.Count ?? 0;

                List<ISystemComponent> displayComponents = displaySystemPlantRoom.GetSystemComponents<ISystemComponent>() ?? new List<ISystemComponent>();
                List<ISystemComponent> displayConnections = displayComponents.FindAll(x => x is ISystemConnection);
                displayComponents.RemoveAll(x => x is ISystemConnection);

                Check(ref result, messages, displayComponents.Count == logicalComponentCount, $"Every component converted to a display object ({displayComponents.Count}/{logicalComponentCount})");

                bool componentsDrawable = displayComponents.Count != 0 && displayComponents.TrueForAll(x => x is IDisplaySystemObject && SAM.Analytical.Systems.Query.SAMGeometry2Dobject((IDisplaySystemObject)x) != null);
                Check(ref result, messages, componentsDrawable, "Every display component carries drawable 2D geometry");

                int logicalConnectionCount = logicalPlantRoom.GetSystemConnections()?.Count ?? 0;
                Check(ref result, messages, displayConnections.Count == logicalConnectionCount, $"Connection count preserved (logical {logicalConnectionCount}, display {displayConnections.Count})");

                bool connectionsDrawable = displayConnections.Count != 0 && displayConnections.TrueForAll(x => x is IDisplaySystemObject && SAM.Analytical.Systems.Query.SAMGeometry2Dobject((IDisplaySystemObject)x) != null);
                Check(ref result, messages, connectionsDrawable, "Every connection became a drawable display polyline");
            }

            return result;
        }

        private static void Check(ref bool result, List<string> messages, bool condition, string description)
        {
            messages.Add((condition ? "PASS" : "FAIL") + ": " + description);
            result &= condition;
        }
    }
}
