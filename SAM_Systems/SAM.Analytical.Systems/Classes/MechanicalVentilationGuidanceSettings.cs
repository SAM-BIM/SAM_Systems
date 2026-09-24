// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// One physical air handling unit's resolved manufacturer operating strategy - the control behaviour a
    /// manufacturer states for representing its own product in a dynamic thermal model (SAM#123).
    /// <para>
    /// <b>What it is.</b> The resolved thresholds, supply-temperature rules and elevated airflow of the
    /// unit's already-selected product, together with the published table the cooling rule reads. It is
    /// resolved values only: nothing here holds a product identity, and no engineering code anywhere
    /// branches on a manufacturer or model name. A second manufacturer with a different strategy is a
    /// second catalogue entry, resolved through the same seam.
    /// </para>
    /// <para>
    /// <b>Manufacturer modelling guidance, never certified performance.</b> A strategy states how a
    /// manufacturer recommends its unit be operated in a model. It is not a certified EN 13141-7 / SAP
    /// heat-recovery efficiency and not a certified specific fan power, it satisfies neither, and it shares
    /// nothing with <see cref="MechanicalVentilationUnitSettings"/>, which carries exactly those certified
    /// figures and is resolved separately. In particular the recovery rule's blend fraction produces the
    /// same arithmetic as an exchanger effectiveness and is never written as one - see
    /// <see cref="SupplyTemperatureRule"/>.
    /// </para>
    /// <para>
    /// <b>The observable is the package supply temperature.</b> Every rule states the air leaving the whole
    /// unit into the dwelling, downstream of everything inside the casing. That is the quantity a
    /// downstream grounding is held to, rather than any internal component property: a native exchanger or
    /// coil property that equals the value it was given proves only that it was written.
    /// </para>
    /// <para>
    /// <b>Four distinct airflows.</b> <see cref="VentilationUnitOperatingStrategy.ElevatedAirFlow_Lps"/> is
    /// what the unit moves while cooling. It is never the unit's selected capacity and never a ventilation
    /// design airflow:
    /// <c>PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow</c>. How it
    /// is divided between rooms is <see cref="MechanicalVentilationOperatingFlows"/>, which derives every
    /// room's share from that room's share of the design distribution and refuses where there is none.
    /// </para>
    /// </summary>
    public class MechanicalVentilationGuidanceSettings : ISystemJSAMObject
    {
        /// <summary>
        /// The manufacturer's stated operating strategy, resolved for this dwelling - thresholds, the three
        /// supply-temperature rules, the cooling activation temperature and the elevated cooling airflow.
        /// </summary>
        public VentilationUnitOperatingStrategy OperatingStrategy { get; set; }

        /// <summary>
        /// The product's published supply-air temperature table, read by a cooling rule of type
        /// <see cref="Analytical.Enums.SupplyTemperatureRuleType.PerformanceTable"/> - axes
        /// <see cref="VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature"/> (the unit's intake),
        /// <see cref="VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature"/> (the unit's extract)
        /// and <see cref="VentilationUnitPerformanceAxis.Name_AirFlowRate"/>, output
        /// <see cref="VentilationUnitPerformanceOutput.Name_SupplyAirTemperature"/>.
        /// <para>
        /// <b>Carried, not copied into the rule.</b> The product's data lives on its catalogue template; a
        /// second copy inside the rule would be a second place for it to be wrong. Required only where a
        /// rule actually reads it.
        /// </para>
        /// </summary>
        public VentilationUnitPerformanceTable SupplyAirTemperatureTable { get; set; }

        /// <summary>
        /// A short, traceable identifier of the manufacturer guidance these values came from, carried so
        /// that a persisted result can be revalidated later without the document.
        /// <para>
        /// <b>Provenance, not contents.</b> Manufacturer guidance is frequently supplied under a
        /// redistribution restriction: this states what the document is, who wrote it and when - and, where
        /// one is kept, the hash of the manifest that records it - never any of its text.
        /// </para>
        /// </summary>
        public string SourceIdentifier { get; set; }

        public MechanicalVentilationGuidanceSettings()
        {
        }

        public MechanicalVentilationGuidanceSettings(MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings)
        {
            if (mechanicalVentilationGuidanceSettings != null)
            {
                OperatingStrategy = mechanicalVentilationGuidanceSettings.OperatingStrategy == null ? null : new VentilationUnitOperatingStrategy(mechanicalVentilationGuidanceSettings.OperatingStrategy);
                SupplyAirTemperatureTable = mechanicalVentilationGuidanceSettings.SupplyAirTemperatureTable == null ? null : new VentilationUnitPerformanceTable(mechanicalVentilationGuidanceSettings.SupplyAirTemperatureTable);
                SourceIdentifier = mechanicalVentilationGuidanceSettings.SourceIdentifier;
            }
        }

        public MechanicalVentilationGuidanceSettings(JsonObject jsonObject)
        {
            FromJsonObject(jsonObject);
        }

        /// <summary>
        /// The package supply temperature [degC] this unit delivers for one hour, the mode it is in, and the
        /// airflow it moves - the manufacturer's own rule, evaluated on the unit's own sensed temperatures.
        /// </summary>
        /// <param name="intakeTemperature_C">The air temperature [degC] at the unit's intake.</param>
        /// <param name="extractTemperature_C">The air temperature [degC] at the unit's extract.</param>
        /// <param name="designAirFlowRate_Lps">What the dwelling is designed to move [l/s] - the background rate.</param>
        /// <param name="ventilationUnitOperatingMode">The mode selected.</param>
        /// <param name="operatingAirFlowRate_Lps">The airflow [l/s] the unit moves in that mode.</param>
        /// <returns>The supply temperature, or <see cref="double.NaN"/> where it cannot be stated.</returns>
        public double SupplyTemperature(
            double intakeTemperature_C,
            double extractTemperature_C,
            double designAirFlowRate_Lps,
            out Analytical.Enums.VentilationUnitOperatingMode ventilationUnitOperatingMode,
            out double operatingAirFlowRate_Lps)
        {
            return SupplyTemperature(intakeTemperature_C, extractTemperature_C, double.NaN, designAirFlowRate_Lps, out ventilationUnitOperatingMode, out operatingAirFlowRate_Lps);
        }

        /// <summary>
        /// As <see cref="SupplyTemperature(double, double, double, out Analytical.Enums.VentilationUnitOperatingMode, out double)"/>,
        /// with the temperature [degC] of the room hosting the cooling-stat - required when the strategy's
        /// cooling is switched by a room stat rather than by the extract.
        /// </summary>
        public double SupplyTemperature(
            double intakeTemperature_C,
            double extractTemperature_C,
            double roomTemperature_C,
            double designAirFlowRate_Lps,
            out Analytical.Enums.VentilationUnitOperatingMode ventilationUnitOperatingMode,
            out double operatingAirFlowRate_Lps)
        {
            ventilationUnitOperatingMode = Analytical.Enums.VentilationUnitOperatingMode.Undefined;
            operatingAirFlowRate_Lps = double.NaN;

            if (Refusal() != null)
            {
                return double.NaN;
            }

            return OperatingStrategy.SupplyTemperature(
                intakeTemperature_C,
                extractTemperature_C,
                roomTemperature_C,
                designAirFlowRate_Lps,
                SupplyAirTemperatureTable,
                out ventilationUnitOperatingMode,
                out operatingAirFlowRate_Lps);
        }

        /// <summary>
        /// Why these settings cannot be materialised, in words, or null where they can. Every rule is a
        /// refusal rather than a repair: nothing here clamps, defaults or reorders a stated figure.
        /// </summary>
        public string Refusal()
        {
            if (OperatingStrategy == null)
            {
                return "states no manufacturer operating strategy.";
            }

            string refusal = OperatingStrategy.Refusal();
            if (refusal != null)
            {
                return string.Format("has a manufacturer operating strategy that {0}", refusal);
            }

            if (string.IsNullOrWhiteSpace(SourceIdentifier))
            {
                return "states no manufacturer guidance source, so a persisted result could not be revalidated against one.";
            }

            //The table is required only where a rule reads it - a strategy whose modes are all stated as
            //rules that need no table needs none.
            bool needsTable = false;

            foreach (Analytical.Enums.VentilationUnitOperatingMode ventilationUnitOperatingMode in new[]
            {
                Analytical.Enums.VentilationUnitOperatingMode.SummerBypass,
                Analytical.Enums.VentilationUnitOperatingMode.HeatCoolthRecovery,
                Analytical.Enums.VentilationUnitOperatingMode.Cooling,
            })
            {
                if (OperatingStrategy.SupplyTemperatureRule(ventilationUnitOperatingMode)?.SupplyTemperatureRuleType == Analytical.Enums.SupplyTemperatureRuleType.PerformanceTable)
                {
                    needsTable = true;
                }
            }

            if (!needsTable)
            {
                return null;
            }

            VentilationUnitPerformanceTable table = SupplyAirTemperatureTable;

            if (table == null)
            {
                return "reads a published supply-air temperature table in one of its modes, and states none.";
            }

            if (!table.IsValid)
            {
                return "states a supply-air temperature table that is not valid (an axis is not strictly rising or finite, or the values do not fill the grid).";
            }

            if (table.Output(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature) == null)
            {
                return string.Format("states a table with no '{0}' output.", VentilationUnitPerformanceOutput.Name_SupplyAirTemperature);
            }

            foreach (string name in MechanicalVentilationCoolingSettings.AxisNames)
            {
                if (table.AxisIndex(name) < 0)
                {
                    return string.Format("states a supply-air temperature table with no '{0}' axis.", name);
                }
            }

            VentilationUnitPerformanceAxis axis_AirFlow = table.Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate);

            if (!string.Equals(axis_AirFlow?.Unit, VentilationUnitPerformanceAxis.Unit_LitresPerSecond, StringComparison.Ordinal))
            {
                return string.Format("states the table's airflow axis in '{0}'; it has to be litres per second.", axis_AirFlow?.Unit);
            }

            return null;
        }

        /// <summary>A stable text of every stated value, folded into the derived energy-centre identity.</summary>
        internal string IdentityComponent()
        {
            StringBuilder stringBuilder = new StringBuilder("guidance");
            stringBuilder.Append('|').Append(SourceIdentifier);
            stringBuilder.Append('|').Append(OperatingStrategy?.ToJsonObject()?.ToJsonString());
            stringBuilder.Append('|').Append(SupplyAirTemperatureTable?.ToJsonObject()?.ToJsonString());

            return stringBuilder.ToString();
        }

        public override string ToString()
        {
            return Refusal() != null
                ? "Invalid MechanicalVentilationGuidanceSettings"
                : string.Format(CultureInfo.InvariantCulture, "{0} [{1}]", OperatingStrategy, SourceIdentifier);
        }

        public bool FromJsonObject(JsonObject jsonObject)
        {
            if (jsonObject == null)
            {
                return false;
            }

            OperatingStrategy = jsonObject["OperatingStrategy"] is JsonObject jsonObject_OperatingStrategy ? new VentilationUnitOperatingStrategy(jsonObject_OperatingStrategy) : null;
            SupplyAirTemperatureTable = jsonObject["SupplyAirTemperatureTable"] is JsonObject jsonObject_Table ? new VentilationUnitPerformanceTable(jsonObject_Table) : null;
            SourceIdentifier = jsonObject.ContainsKey("SourceIdentifier") ? jsonObject["SourceIdentifier"]?.GetValue<string>() : null;

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject result = new JsonObject
            {
                { "_type", Core.Query.FullTypeName(this) }
            };

            if (OperatingStrategy != null)
            {
                result.Add("OperatingStrategy", OperatingStrategy.ToJsonObject());
            }

            if (SupplyAirTemperatureTable != null)
            {
                result.Add("SupplyAirTemperatureTable", SupplyAirTemperatureTable.ToJsonObject());
            }

            if (!string.IsNullOrWhiteSpace(SourceIdentifier))
            {
                result.Add("SourceIdentifier", SourceIdentifier);
            }

            return result;
        }
    }
}
