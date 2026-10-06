// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// How one air handling unit's elevated operating airflow is divided between the rooms it serves -
    /// derived from what the dwelling was designed to move, and from nothing else (SAM#123).
    /// <para>
    /// <b>The design distribution is the authority, and it is read, never written.</b> A dwelling's supply
    /// terminals are already balanced against one another: the proportions between rooms carry the
    /// designer's judgement about which rooms need what, and a manufacturer whose unit runs faster to
    /// mitigate overheating expects those proportions to be held. So each room's share of an elevated total
    /// is its own share of the design total, on its own side:
    /// </para>
    /// <code>
    /// operating supply_i  = operating total * (design supply_i  / design supply total)
    /// operating extract_j = operating total * (design extract_j / design extract total)
    /// </code>
    /// <para>
    /// <b>Supply and extract are proportioned separately, and both totals become the same number.</b> A
    /// dwelling supplies habitable rooms and extracts from wet rooms, so the two sides' room lists differ
    /// and only their totals are comparable. Raising supply without raising extract to match would be an
    /// unbalanced unit, so the elevated total is the total on both sides - which is exactly what a
    /// manufacturer means by the wet-room extract rising to match.
    /// </para>
    /// <para>
    /// <b>Four airflows stay four airflows.</b> Nothing here writes, scales or replaces a design airflow:
    /// the design figures are inputs and survive unchanged, and what this produces is an
    /// <c>OperatingAirFlow</c> distribution -
    /// <c>PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow</c>.
    /// </para>
    /// <para>
    /// <b>Fail closed.</b> There is no distribution to hold where there is no design distribution to hold
    /// it from: a missing, zero or unusable design total refuses rather than falling back to an even split
    /// between rooms, or to floor area. An even split is a distribution nobody stated, and it is the
    /// specific mistake a manufacturer's guidance warns against - it over-cools some rooms and under-cools
    /// others while looking entirely reasonable.
    /// </para>
    /// </summary>
    public class MechanicalVentilationOperatingFlows
    {
        /// <summary>
        /// How far the two sides' totals may land from the operating total before it is a refusal [l/s].
        /// <para>
        /// This is an arithmetic tolerance, not an engineering one: the shares are computed in double
        /// precision and their sum can differ from the total in the last bits. It is far below any airflow
        /// anybody states, so it can never absorb a real imbalance.
        /// </para>
        /// </summary>
        public const double Tolerance_Lps = 1e-6;

        private readonly Dictionary<Guid, double> dictionary_DesignSupply_Lps = new Dictionary<Guid, double>();
        private readonly Dictionary<Guid, double> dictionary_DesignExtract_Lps = new Dictionary<Guid, double>();
        private readonly Dictionary<Guid, double> dictionary_OperatingSupply_Lps = new Dictionary<Guid, double>();
        private readonly Dictionary<Guid, double> dictionary_OperatingExtract_Lps = new Dictionary<Guid, double>();
        private readonly string refusal;

        /// <summary>
        /// Divides an elevated operating airflow between rooms in their design proportions.
        /// </summary>
        /// <param name="designSupply_Lps">Each room's design supply airflow [l/s]. Rooms with none are simply absent.</param>
        /// <param name="designExtract_Lps">Each room's design extract airflow [l/s]. Rooms with none are simply absent.</param>
        /// <param name="operatingAirFlowRate_Lps">The unit's elevated total [l/s] - an operating airflow.</param>
        public MechanicalVentilationOperatingFlows(
            IReadOnlyDictionary<Guid, double> designSupply_Lps,
            IReadOnlyDictionary<Guid, double> designExtract_Lps,
            double operatingAirFlowRate_Lps)
        {
            OperatingAirFlowRate_Lps = operatingAirFlowRate_Lps;

            refusal = Read(designSupply_Lps, dictionary_DesignSupply_Lps, "supply")
                ?? Read(designExtract_Lps, dictionary_DesignExtract_Lps, "extract")
                ?? Distribute();
        }

        /// <summary>The elevated total [l/s] this distribution divides - the same number on both sides.</summary>
        public double OperatingAirFlowRate_Lps { get; }

        /// <summary>What the dwelling is designed to supply in total [l/s] - the denominator of every supply share.</summary>
        public double DesignSupplyTotal_Lps { get; private set; } = double.NaN;

        /// <summary>What the dwelling is designed to extract in total [l/s] - the denominator of every extract share.</summary>
        public double DesignExtractTotal_Lps { get; private set; } = double.NaN;

        /// <summary>Each room's elevated supply airflow [l/s]. Empty where this refuses.</summary>
        public IReadOnlyDictionary<Guid, double> OperatingSupply_Lps
        {
            get
            {
                return new Dictionary<Guid, double>(dictionary_OperatingSupply_Lps);
            }
        }

        /// <summary>Each room's elevated extract airflow [l/s]. Empty where this refuses.</summary>
        public IReadOnlyDictionary<Guid, double> OperatingExtract_Lps
        {
            get
            {
                return new Dictionary<Guid, double>(dictionary_OperatingExtract_Lps);
            }
        }

        /// <summary>
        /// One room's share [-] of the design supply total - the proportion this distribution holds. NaN
        /// where this refuses or the room supplies nothing.
        /// </summary>
        public double DesignSupplyShare(Guid guid_Space)
        {
            return refusal == null && dictionary_DesignSupply_Lps.TryGetValue(guid_Space, out double value) ? value / DesignSupplyTotal_Lps : double.NaN;
        }

        /// <summary>One room's share [-] of the design extract total. NaN where this refuses or the room extracts nothing.</summary>
        public double DesignExtractShare(Guid guid_Space)
        {
            return refusal == null && dictionary_DesignExtract_Lps.TryGetValue(guid_Space, out double value) ? value / DesignExtractTotal_Lps : double.NaN;
        }

        /// <summary>
        /// Why this distribution could not be stated, in words, or null where it could. Every rule refuses
        /// rather than repairs.
        /// </summary>
        public string Refusal()
        {
            return refusal;
        }

        private string Read(IReadOnlyDictionary<Guid, double> source, Dictionary<Guid, double> destination, string side)
        {
            if (source == null || source.Count == 0)
            {
                return string.Format("states no design {0} airflow for any room, so there is no distribution to hold.", side);
            }

            foreach (KeyValuePair<Guid, double> keyValuePair in source)
            {
                double value = keyValuePair.Value;

                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                {
                    return string.Format(CultureInfo.InvariantCulture, "states a design {0} airflow of {1} l/s for room {2}.", side, value, keyValuePair.Key);
                }

                destination[keyValuePair.Key] = value;
            }

            return null;
        }

        private string Distribute()
        {
            DesignSupplyTotal_Lps = Total(dictionary_DesignSupply_Lps);
            DesignExtractTotal_Lps = Total(dictionary_DesignExtract_Lps);

            if (!(DesignSupplyTotal_Lps > 0))
            {
                return string.Format(CultureInfo.InvariantCulture, "states a design supply total of {0:0.###} l/s, so no room's share of it can be stated.", DesignSupplyTotal_Lps);
            }

            if (!(DesignExtractTotal_Lps > 0))
            {
                return string.Format(CultureInfo.InvariantCulture, "states a design extract total of {0:0.###} l/s, so no room's share of it can be stated.", DesignExtractTotal_Lps);
            }

            if (double.IsNaN(OperatingAirFlowRate_Lps) || double.IsInfinity(OperatingAirFlowRate_Lps) || OperatingAirFlowRate_Lps <= 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "states an operating airflow of {0} l/s.", OperatingAirFlowRate_Lps);
            }

            //An elevated rate that is below what the dwelling already moves is not an elevated rate. Refused
            //rather than clamped up to the design total: the two figures come from different decisions, and
            //one contradicting the other is something to be shown, not quietly reconciled.
            if (OperatingAirFlowRate_Lps < DesignSupplyTotal_Lps - Tolerance_Lps || OperatingAirFlowRate_Lps < DesignExtractTotal_Lps - Tolerance_Lps)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "states an operating airflow of {0:0.###} l/s, below the {1:0.###}/{2:0.###} l/s supply/extract the dwelling is already designed to move.",
                    OperatingAirFlowRate_Lps,
                    DesignSupplyTotal_Lps,
                    DesignExtractTotal_Lps);
            }

            foreach (KeyValuePair<Guid, double> keyValuePair in dictionary_DesignSupply_Lps)
            {
                dictionary_OperatingSupply_Lps[keyValuePair.Key] = OperatingAirFlowRate_Lps * keyValuePair.Value / DesignSupplyTotal_Lps;
            }

            foreach (KeyValuePair<Guid, double> keyValuePair in dictionary_DesignExtract_Lps)
            {
                dictionary_OperatingExtract_Lps[keyValuePair.Key] = OperatingAirFlowRate_Lps * keyValuePair.Value / DesignExtractTotal_Lps;
            }

            //Read off the finished distribution rather than assumed of it: both sides have to add up to the
            //operating total, or the unit is unbalanced and the distribution is not the one that was asked
            //for.
            double supply_Lps = Total(dictionary_OperatingSupply_Lps);
            double extract_Lps = Total(dictionary_OperatingExtract_Lps);

            if (Math.Abs(supply_Lps - OperatingAirFlowRate_Lps) > Tolerance_Lps || Math.Abs(extract_Lps - OperatingAirFlowRate_Lps) > Tolerance_Lps)
            {
                dictionary_OperatingSupply_Lps.Clear();
                dictionary_OperatingExtract_Lps.Clear();

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "divides {0:0.###} l/s into {1:0.######}/{2:0.######} l/s supply/extract, which does not balance.",
                    OperatingAirFlowRate_Lps,
                    supply_Lps,
                    extract_Lps);
            }

            return null;
        }

        private static double Total(Dictionary<Guid, double> dictionary)
        {
            double result = 0;

            foreach (double value in dictionary.Values)
            {
                result += value;
            }

            return result;
        }
    }
}
