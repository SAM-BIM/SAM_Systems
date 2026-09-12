// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System.Globalization;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// One physical air handling unit's manufacturer-aware behaviour, resolved generically and applied to
    /// its own re-keyed copy of the topology template - Part O Iteration 3 PR5A (SAM#111 plan §C).
    /// <para>
    /// <b>Every field is an override, and every field is independent.</b> Each is <see cref="double.NaN"/>
    /// (or null, for <see cref="HeatRecoverySupplyLimit_C"/>) by default, meaning "leave the template's own
    /// value on this property untouched" - never zero, and never a manufacturer figure. A caller states
    /// only what it has resolved: a unit with certified fan data but no certified heat recovery efficiency
    /// sets the fan fields and leaves <see cref="HeatRecoverySensibleEfficiency"/> unset, and that unit's
    /// exchanger is left exactly as the template shipped it.
    /// </para>
    /// <para>
    /// <b>Resolved values only - no product identity, and no manufacturer-name logic.</b> This carries the
    /// numbers <c>SAM.Analytical.Query.VentilationUnitOperatingParameters</c> already resolved from a
    /// selected product's certified data; it does not carry the product reference, the catalogue, or any
    /// conditional on what product the values came from. What each number means is stated where it is
    /// resolved, not here.
    /// </para>
    /// <para>
    /// <b>Never capacity, never a design or operating airflow.</b>
    /// <c>PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow</c> - this
    /// type carries none of the four, and nothing in <c>Create.MechanicalVentilation</c> ever writes a
    /// fan's <c>Capacity</c> from it.
    /// </para>
    /// </summary>
    public class MechanicalVentilationUnitSettings : ISystemJSAMObject
    {
        /// <summary>
        /// The certified sensible heat recovery efficiency [-] to state on the unit's single
        /// <c>SystemExchanger</c>, or <see cref="double.NaN"/> to leave the template's exchanger untouched.
        /// <para>
        /// Applying a stated value also sets <c>LatentEfficiency = 0</c>, <c>ExchangerType</c> and
        /// <c>ExchangerCalculationMethod</c> to <c>Simple</c>, <c>HeatingOnly = false</c>, and
        /// <c>AdjustForOptimiser = false</c> - the frozen plan's mapping (SAM#111 plan §C), not a
        /// manufacturer figure and not what the shipped <c>MVRE.json</c> otherwise states. Requesting this
        /// on a template whose air-system copy does not carry exactly one <c>SystemExchanger</c> refuses
        /// the whole materialisation.
        /// </para>
        /// </summary>
        public double HeatRecoverySensibleEfficiency { get; set; } = double.NaN;

        /// <summary>
        /// A declared bypass/supply-limit route parameter [degC] - a scenario value, never manufacturer
        /// data (SAM#111 plan §C/§E: "B3 = a declared, deterministic mode"). Null leaves it unset; it is
        /// carried here only so it travels with the rest of a unit's settings - the SAM_Tas PR5A slice is
        /// what interprets it, not this repository.
        /// </summary>
        public double? HeatRecoverySupplyLimit_C { get; set; }

        /// <summary>The supply fan's total pressure rise [Pa], or NaN to leave it untouched.</summary>
        public double SupplyFanPressure_Pa { get; set; } = double.NaN;

        /// <summary>The extract fan's total pressure rise [Pa], or NaN to leave it untouched.</summary>
        public double ExtractFanPressure_Pa { get; set; } = double.NaN;

        /// <summary>
        /// The overall efficiency [0-1] stated on both the supply and the extract fan, or NaN to leave them
        /// untouched. One certified specific fan power is a whole-unit figure, declaratively split between
        /// the two fans' pressure (SAM#111 plan §C) - this is the other half of that same split.
        /// </summary>
        public double FanOverallEfficiency { get; set; } = double.NaN;

        /// <summary>The supply fan's heat gain factor [0-1], or NaN to leave it untouched.</summary>
        public double SupplyFanHeatGainFactor { get; set; } = double.NaN;

        /// <summary>The extract fan's heat gain factor [0-1], or NaN to leave it untouched.</summary>
        public double ExtractFanHeatGainFactor { get; set; } = double.NaN;

        public MechanicalVentilationUnitSettings()
        {
        }

        public MechanicalVentilationUnitSettings(MechanicalVentilationUnitSettings mechanicalVentilationUnitSettings)
        {
            if (mechanicalVentilationUnitSettings != null)
            {
                HeatRecoverySensibleEfficiency = mechanicalVentilationUnitSettings.HeatRecoverySensibleEfficiency;
                HeatRecoverySupplyLimit_C = mechanicalVentilationUnitSettings.HeatRecoverySupplyLimit_C;
                SupplyFanPressure_Pa = mechanicalVentilationUnitSettings.SupplyFanPressure_Pa;
                ExtractFanPressure_Pa = mechanicalVentilationUnitSettings.ExtractFanPressure_Pa;
                FanOverallEfficiency = mechanicalVentilationUnitSettings.FanOverallEfficiency;
                SupplyFanHeatGainFactor = mechanicalVentilationUnitSettings.SupplyFanHeatGainFactor;
                ExtractFanHeatGainFactor = mechanicalVentilationUnitSettings.ExtractFanHeatGainFactor;
            }
        }

        public MechanicalVentilationUnitSettings(JsonObject jsonObject)
        {
            FromJsonObject(jsonObject);
        }

        /// <summary>Whether any override is stated at all - an all-untouched settings object materialises exactly as PR1 did.</summary>
        public bool HasOverride
        {
            get
            {
                return !double.IsNaN(HeatRecoverySensibleEfficiency)
                    || HeatRecoverySupplyLimit_C.HasValue
                    || !double.IsNaN(SupplyFanPressure_Pa)
                    || !double.IsNaN(ExtractFanPressure_Pa)
                    || !double.IsNaN(FanOverallEfficiency)
                    || !double.IsNaN(SupplyFanHeatGainFactor)
                    || !double.IsNaN(ExtractFanHeatGainFactor);
            }
        }

        public bool FromJsonObject(JsonObject jsonObject)
        {
            if (jsonObject == null)
            {
                return false;
            }

            HeatRecoverySensibleEfficiency = jsonObject.ContainsKey("HeatRecoverySensibleEfficiency") ? (jsonObject["HeatRecoverySensibleEfficiency"]?.GetValue<double>() ?? double.NaN) : double.NaN;
            HeatRecoverySupplyLimit_C = jsonObject.ContainsKey("HeatRecoverySupplyLimit_C") ? jsonObject["HeatRecoverySupplyLimit_C"]?.GetValue<double>() : null;
            SupplyFanPressure_Pa = jsonObject.ContainsKey("SupplyFanPressure_Pa") ? (jsonObject["SupplyFanPressure_Pa"]?.GetValue<double>() ?? double.NaN) : double.NaN;
            ExtractFanPressure_Pa = jsonObject.ContainsKey("ExtractFanPressure_Pa") ? (jsonObject["ExtractFanPressure_Pa"]?.GetValue<double>() ?? double.NaN) : double.NaN;
            FanOverallEfficiency = jsonObject.ContainsKey("FanOverallEfficiency") ? (jsonObject["FanOverallEfficiency"]?.GetValue<double>() ?? double.NaN) : double.NaN;
            SupplyFanHeatGainFactor = jsonObject.ContainsKey("SupplyFanHeatGainFactor") ? (jsonObject["SupplyFanHeatGainFactor"]?.GetValue<double>() ?? double.NaN) : double.NaN;
            ExtractFanHeatGainFactor = jsonObject.ContainsKey("ExtractFanHeatGainFactor") ? (jsonObject["ExtractFanHeatGainFactor"]?.GetValue<double>() ?? double.NaN) : double.NaN;

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject result = new JsonObject
            {
                { "_type", Core.Query.FullTypeName(this) }
            };

            if (!double.IsNaN(HeatRecoverySensibleEfficiency))
            {
                result.Add("HeatRecoverySensibleEfficiency", HeatRecoverySensibleEfficiency);
            }

            if (HeatRecoverySupplyLimit_C.HasValue)
            {
                result.Add("HeatRecoverySupplyLimit_C", HeatRecoverySupplyLimit_C.Value);
            }

            if (!double.IsNaN(SupplyFanPressure_Pa))
            {
                result.Add("SupplyFanPressure_Pa", SupplyFanPressure_Pa);
            }

            if (!double.IsNaN(ExtractFanPressure_Pa))
            {
                result.Add("ExtractFanPressure_Pa", ExtractFanPressure_Pa);
            }

            if (!double.IsNaN(FanOverallEfficiency))
            {
                result.Add("FanOverallEfficiency", FanOverallEfficiency);
            }

            if (!double.IsNaN(SupplyFanHeatGainFactor))
            {
                result.Add("SupplyFanHeatGainFactor", SupplyFanHeatGainFactor);
            }

            if (!double.IsNaN(ExtractFanHeatGainFactor))
            {
                result.Add("ExtractFanHeatGainFactor", ExtractFanHeatGainFactor);
            }

            return result;
        }

        /// <summary>
        /// A stable, order-independent text of every stated field - used to fold this unit's settings into
        /// the derived energy-centre identity, so the same design against the same template with different
        /// settings never reuses another run's guids.
        /// </summary>
        internal string IdentityComponent()
        {
            return string.Join(
                "|",
                HeatRecoverySensibleEfficiency.ToString("R", CultureInfo.InvariantCulture),
                HeatRecoverySupplyLimit_C.HasValue ? HeatRecoverySupplyLimit_C.Value.ToString("R", CultureInfo.InvariantCulture) : "-",
                SupplyFanPressure_Pa.ToString("R", CultureInfo.InvariantCulture),
                ExtractFanPressure_Pa.ToString("R", CultureInfo.InvariantCulture),
                FanOverallEfficiency.ToString("R", CultureInfo.InvariantCulture),
                SupplyFanHeatGainFactor.ToString("R", CultureInfo.InvariantCulture),
                ExtractFanHeatGainFactor.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
