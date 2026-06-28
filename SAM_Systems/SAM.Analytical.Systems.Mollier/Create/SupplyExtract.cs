// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using SAM.Core.Mollier;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        /// <summary>
        /// Builds a <see cref="SystemPlantRoom"/> from a supply and an extract chain of Mollier processes,
        /// sharing a single heat-recovery exchanger across both air paths (e.g. a twin-wheel unit).
        /// </summary>
        /// <remarks>
        /// The supply chain is wired onto a supply <see cref="AirSystem"/> and the extract chain onto an
        /// extract <see cref="AirSystem"/>. A <see cref="SystemExchanger"/> exposes two air paths (connection
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
            if (supplyMollierProcesses == null && extractMollierProcesses == null)
            {
                return null;
            }

            SystemPlantRoom systemPlantRoom = new SystemPlantRoom(name);

            // Supply chain.
            List<SystemExchanger> supplyExchangers = new List<SystemExchanger>();
            int supplyCount = AddChain(systemPlantRoom, supplyMollierProcesses, designSupplyAirflow, "Supply Air System", null, supplyExchangers);

            // Extract chain, reusing the supply-side exchangers (in order) so a twin-wheel is one device.
            int extractCount = AddChain(systemPlantRoom, extractMollierProcesses, designExtractAirflow, "Extract Air System", supplyExchangers, null);

            // With both air paths known, derive each shared exchanger's sensible/latent effectiveness.
            ApplyHeatRecoveryEfficiencies(supplyMollierProcesses, extractMollierProcesses, supplyExchangers);

            if (supplyCount == 0 && extractCount == 0)
            {
                return null;
            }

            return systemPlantRoom;
        }

        /// <summary>
        /// Builds a <see cref="SystemEnergyCentre"/> from a supply and extract Mollier process chain
        /// (twin-wheel aware). See <see cref="SystemPlantRoom(IEnumerable{IMollierProcess}, IEnumerable{IMollierProcess}, double, double, string)"/>.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, double designSupplyAirflow = double.NaN, double designExtractAirflow = double.NaN, string name = "Energy Centre")
        {
            SystemPlantRoom systemPlantRoom = Create.SystemPlantRoom(supplyMollierProcesses, extractMollierProcesses, designSupplyAirflow, designExtractAirflow);
            if (systemPlantRoom == null)
            {
                return null;
            }

            SystemEnergyCentre systemEnergyCentre = new SystemEnergyCentre(string.IsNullOrWhiteSpace(name) ? "Energy Centre" : name);
            systemEnergyCentre.Add(systemPlantRoom);

            return systemEnergyCentre;
        }

        /// <summary>
        /// Maps and wires one ordered process chain onto its own air system inside the plant room.
        /// When <paramref name="reusableExchangers"/> is supplied, heat-recovery processes reuse those exchanger
        /// instances (in order) instead of creating new ones; when <paramref name="createdExchangers"/> is supplied,
        /// newly created exchangers are appended to it.
        /// </summary>
        private static int AddChain(SystemPlantRoom systemPlantRoom, IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, string airSystemName, List<SystemExchanger> reusableExchangers, List<SystemExchanger> createdExchangers)
        {
            if (mollierProcesses == null)
            {
                return 0;
            }

            AirSystem airSystem = new AirSystem(airSystemName);
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
                    current = mollierProcess.SystemComponent(designAirflow);
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
                    systemPlantRoom.Connect(previous, current, out _, airSystem);
                }

                previous = current;
                count++;
            }

            return count;
        }

        /// <summary>
        /// Sets sensible (and, where moisture is transferred, latent) effectiveness on each shared heat-recovery
        /// exchanger, by pairing the supply and extract heat-recovery processes in order.
        /// </summary>
        /// <remarks>
        /// The supply-side exchangers were created in supply-chain order, one per HeatRecoveryProcess, so they
        /// correspond index-for-index with the ordered supply heat-recovery processes; the extract heat-recovery
        /// processes provide the second (exhaust) air path. See <see cref="Query.HeatRecoveryEfficiencies"/>.
        /// </remarks>
        private static void ApplyHeatRecoveryEfficiencies(IEnumerable<IMollierProcess> supplyMollierProcesses, IEnumerable<IMollierProcess> extractMollierProcesses, List<SystemExchanger> exchangers)
        {
            if (exchangers == null || exchangers.Count == 0 || supplyMollierProcesses == null || extractMollierProcesses == null)
            {
                return;
            }

            List<HeatRecoveryProcess> supplyHeatRecoveries = supplyMollierProcesses.OfType<HeatRecoveryProcess>().ToList();
            List<HeatRecoveryProcess> extractHeatRecoveries = extractMollierProcesses.OfType<HeatRecoveryProcess>().ToList();

            int count = System.Math.Min(exchangers.Count, System.Math.Min(supplyHeatRecoveries.Count, extractHeatRecoveries.Count));
            for (int i = 0; i < count; i++)
            {
                supplyHeatRecoveries[i].HeatRecoveryEfficiencies(extractHeatRecoveries[i], out double sensibleEfficiency, out double latentEfficiency);

                SystemExchanger systemExchanger = exchangers[i];
                if (systemExchanger == null)
                {
                    continue;
                }

                if (!double.IsNaN(sensibleEfficiency))
                {
                    systemExchanger.SensibleEfficiency = sensibleEfficiency;
                }

                if (!double.IsNaN(latentEfficiency))
                {
                    systemExchanger.LatentEfficiency = latentEfficiency;
                    systemExchanger.ExchangerLatentType = ExchangerLatentType.HumidityRatio;
                }
            }
        }
    }
}
