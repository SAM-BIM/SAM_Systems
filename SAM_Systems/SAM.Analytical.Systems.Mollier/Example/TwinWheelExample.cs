// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SAM.Core.Mollier;
using SAM.Core.Systems;

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
            HeatRecoveryProcess supplyHeatRecovery = outdoor.HeatRecoveryProcess(room, 0.75, 0.65); // sensible 0.75, latent 0.65
            CoolingProcess cooling = supplyHeatRecovery.End.CoolingProcess(13, 0.85);               // off-coil 13 C, efficiency 0.85
            HeatingProcess reheat = cooling.End.HeatingProcess(16);                                 // reheat to 16 C
            FanProcess supplyFan = reheat.End.FanProcess(0.8);                                      // specific fan temperature rise

            supplyMollierProcesses = new List<IMollierProcess> { supplyHeatRecovery, cooling, reheat, supplyFan };

            // Extract chain: same wheel (exhaust side) -> extract fan.
            HeatRecoveryProcess extractHeatRecovery = room.HeatRecoveryProcess(outdoor, 0.75, 0.65, true);
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
            }

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

            return result;
        }

        private static void Check(ref bool result, List<string> messages, bool condition, string description)
        {
            messages.Add((condition ? "PASS" : "FAIL") + ": " + description);
            result &= condition;
        }
    }
}
