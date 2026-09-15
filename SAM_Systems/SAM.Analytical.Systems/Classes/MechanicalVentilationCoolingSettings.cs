// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// One physical air handling unit's resolved aggregate cooling module, materialised as an internal
    /// recirculation branch inside that unit's own air system - Part O Iteration 3 PR5B (SAM#111).
    /// <para>
    /// <b>What it is.</b> An aggregate thermal surrogate: the air leaving the cooling module is the
    /// published supply-air temperature at the outdoor dry bulb, the entering (room / mixed-return) dry
    /// bulb and the recirculation airflow, and nothing more. No compressor, refrigerant, electrical or
    /// latent behaviour is stated, and none is modelled.
    /// </para>
    /// <para>
    /// <b>Where it goes.</b> The unit's rooms already sit on its one air system. The branch takes air from
    /// those SAME rooms, through one return damper per room, into the cooling coil, through a
    /// recirculation fan and back to the SAME rooms through one supply damper per room. It adds no room,
    /// no air system and no outdoor air, and it never touches a ventilation design airflow.
    /// </para>
    /// <para>
    /// <b>Resolved values only - no product identity, no manufacturer-name logic.</b> The caller resolves
    /// a selected product's published data; this carries the resolved numbers.
    /// </para>
    /// <para>
    /// <b>Four distinct airflows.</b> <see cref="MaximumOperatingAirFlow_Lps"/> is the validated ceiling
    /// of the cooling table's own airflow axis - never the unit's selected capacity and never a
    /// ventilation design airflow:
    /// <c>PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow</c>.
    /// </para>
    /// </summary>
    public class MechanicalVentilationCoolingSettings : ISystemJSAMObject
    {
        /// <summary>
        /// The published supply-air temperature [degC] against outdoor dry bulb, entering dry bulb and
        /// airflow - axes <see cref="VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature"/>,
        /// <see cref="VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature"/> and
        /// <see cref="VentilationUnitPerformanceAxis.Name_AirFlowRate"/>, output
        /// <see cref="VentilationUnitPerformanceOutput.Name_SupplyAirTemperature"/>. The entering dry bulb
        /// is the room / mixed-return air the module draws, which is why the module sits on the
        /// recirculation branch and not on the outdoor supply path.
        /// </summary>
        public VentilationUnitPerformanceTable SupplyAirTemperatureTable { get; set; }

        /// <summary>
        /// The recirculation fraction [-] of <see cref="MaximumOperatingAirFlow_Lps"/> against the
        /// mixed-return control temperature [degC]. Exactly two points, rising: a linear law between them
        /// and held at either end outside it.
        /// </summary>
        public FlowFractionControlCurve FlowFractionByControlTemperature { get; set; }

        /// <summary>
        /// The most recirculation air [l/s] the branch may carry - the validated ceiling of the cooling
        /// table. It may not exceed the table's own airflow axis; a figure the table does not tabulate is
        /// not validated performance.
        /// </summary>
        public double MaximumOperatingAirFlow_Lps { get; set; } = double.NaN;

        /// <summary>
        /// The mixed-return temperature [degC] below which the cooling module does nothing - a declared
        /// cooling-enable threshold, not a heating setpoint. The module never heats.
        /// </summary>
        public double CoolingEnableTemperature_C { get; set; } = double.NaN;

        public MechanicalVentilationCoolingSettings()
        {
        }

        public MechanicalVentilationCoolingSettings(MechanicalVentilationCoolingSettings mechanicalVentilationCoolingSettings)
        {
            if (mechanicalVentilationCoolingSettings != null)
            {
                SupplyAirTemperatureTable = mechanicalVentilationCoolingSettings.SupplyAirTemperatureTable == null ? null : new VentilationUnitPerformanceTable(mechanicalVentilationCoolingSettings.SupplyAirTemperatureTable);
                FlowFractionByControlTemperature = mechanicalVentilationCoolingSettings.FlowFractionByControlTemperature == null ? null : new FlowFractionControlCurve(mechanicalVentilationCoolingSettings.FlowFractionByControlTemperature);
                MaximumOperatingAirFlow_Lps = mechanicalVentilationCoolingSettings.MaximumOperatingAirFlow_Lps;
                CoolingEnableTemperature_C = mechanicalVentilationCoolingSettings.CoolingEnableTemperature_C;
            }
        }

        public MechanicalVentilationCoolingSettings(JsonObject jsonObject)
        {
            FromJsonObject(jsonObject);
        }

        /// <summary>The smallest recirculation airflow [l/s] the law allows: the lower fraction of the ceiling.</summary>
        public double MinimumOperatingAirFlow_Lps
        {
            get
            {
                double[] flowFractions = FlowFractionByControlTemperature?.FlowFractions;
                return flowFractions == null || flowFractions.Length == 0 ? double.NaN : MaximumOperatingAirFlow_Lps * flowFractions[0];
            }
        }

        /// <summary>
        /// Why these settings cannot be materialised, in words, or null where they can. Every rule is a
        /// refusal rather than a repair: nothing here clamps, defaults or reorders a published figure.
        /// </summary>
        public string Refusal()
        {
            VentilationUnitPerformanceTable table = SupplyAirTemperatureTable;
            if (table == null)
            {
                return "states no supply-air temperature table.";
            }

            if (!table.IsValid)
            {
                return "states a supply-air temperature table that is not valid (an axis is not strictly rising or finite, or the values do not fill the grid).";
            }

            if (table.AxisCount != 3)
            {
                return string.Format("states a supply-air temperature table over {0} axis(es); the module is tabulated over outdoor dry bulb, entering dry bulb and airflow.", table.AxisCount);
            }

            foreach (string name in AxisNames)
            {
                if (table.AxisIndex(name) < 0)
                {
                    return string.Format("states a supply-air temperature table with no '{0}' axis.", name);
                }
            }

            if (table.Output(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature) == null)
            {
                return string.Format("states a table with no '{0}' output.", VentilationUnitPerformanceOutput.Name_SupplyAirTemperature);
            }

            VentilationUnitPerformanceAxis axis_AirFlow = table.Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate);
            if (!string.Equals(axis_AirFlow?.Unit, VentilationUnitPerformanceAxis.Unit_LitresPerSecond, StringComparison.Ordinal))
            {
                return string.Format("states the table's airflow axis in '{0}'; it has to be litres per second.", axis_AirFlow?.Unit);
            }

            if (double.IsNaN(MaximumOperatingAirFlow_Lps) || double.IsInfinity(MaximumOperatingAirFlow_Lps) || MaximumOperatingAirFlow_Lps <= 0)
            {
                return string.Format("states a maximum operating airflow of {0} l/s.", MaximumOperatingAirFlow_Lps);
            }

            if (MaximumOperatingAirFlow_Lps > axis_AirFlow.Maximum)
            {
                return string.Format(
                    "states a maximum operating airflow of {0} l/s, above the {1} l/s the cooling table tabulates - an airflow the table does not state is not validated performance.",
                    MaximumOperatingAirFlow_Lps,
                    axis_AirFlow.Maximum);
            }

            FlowFractionControlCurve flowFractionControlCurve = FlowFractionByControlTemperature;
            if (flowFractionControlCurve == null || !flowFractionControlCurve.IsValid)
            {
                return "states no valid flow-fraction control curve.";
            }

            double[] controlTemperatures = flowFractionControlCurve.ControlTemperatures_C;
            double[] flowFractions = flowFractionControlCurve.FlowFractions;
            if (controlTemperatures == null || flowFractions == null || controlTemperatures.Length != 2 || flowFractions.Length != 2)
            {
                return "states a flow-fraction control curve that is not a two-point linear law.";
            }

            if (!(controlTemperatures[1] > controlTemperatures[0]))
            {
                return "states a flow-fraction control curve whose control temperatures do not rise.";
            }

            if (!(flowFractions[0] > 0) || flowFractions[1] < flowFractions[0] || flowFractions[1] > 1)
            {
                return string.Format("states flow fractions {0} -> {1}; they have to rise within (0, 1].", flowFractions[0], flowFractions[1]);
            }

            if (double.IsNaN(CoolingEnableTemperature_C) || double.IsInfinity(CoolingEnableTemperature_C))
            {
                return "states no cooling-enable temperature.";
            }

            return null;
        }

        /// <summary>The three table axes, in the order the cooling coil's table is written.</summary>
        public static IReadOnlyList<string> AxisNames { get; } = new[]
        {
            VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature,
            VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature,
            VentilationUnitPerformanceAxis.Name_AirFlowRate,
        };

        /// <summary>A stable text of every stated value, folded into the derived energy-centre identity.</summary>
        internal string IdentityComponent()
        {
            StringBuilder stringBuilder = new StringBuilder("cooling");
            stringBuilder.Append('|').Append(MaximumOperatingAirFlow_Lps.ToString("R", CultureInfo.InvariantCulture));
            stringBuilder.Append('|').Append(CoolingEnableTemperature_C.ToString("R", CultureInfo.InvariantCulture));
            stringBuilder.Append('|').Append(SupplyAirTemperatureTable?.ToJsonObject()?.ToJsonString());
            stringBuilder.Append('|').Append(FlowFractionByControlTemperature?.ToJsonObject()?.ToJsonString());

            return stringBuilder.ToString();
        }

        public bool FromJsonObject(JsonObject jsonObject)
        {
            if (jsonObject == null)
            {
                return false;
            }

            SupplyAirTemperatureTable = jsonObject["SupplyAirTemperatureTable"] is JsonObject jsonObject_Table ? new VentilationUnitPerformanceTable(jsonObject_Table) : null;
            FlowFractionByControlTemperature = jsonObject["FlowFractionByControlTemperature"] is JsonObject jsonObject_Curve ? new FlowFractionControlCurve(jsonObject_Curve) : null;
            MaximumOperatingAirFlow_Lps = jsonObject.ContainsKey("MaximumOperatingAirFlow_Lps") ? (jsonObject["MaximumOperatingAirFlow_Lps"]?.GetValue<double>() ?? double.NaN) : double.NaN;
            CoolingEnableTemperature_C = jsonObject.ContainsKey("CoolingEnableTemperature_C") ? (jsonObject["CoolingEnableTemperature_C"]?.GetValue<double>() ?? double.NaN) : double.NaN;

            return true;
        }

        public JsonObject ToJsonObject()
        {
            JsonObject result = new JsonObject
            {
                { "_type", Core.Query.FullTypeName(this) }
            };

            if (SupplyAirTemperatureTable != null)
            {
                result.Add("SupplyAirTemperatureTable", SupplyAirTemperatureTable.ToJsonObject());
            }

            if (FlowFractionByControlTemperature != null)
            {
                result.Add("FlowFractionByControlTemperature", FlowFractionByControlTemperature.ToJsonObject());
            }

            if (!double.IsNaN(MaximumOperatingAirFlow_Lps))
            {
                result.Add("MaximumOperatingAirFlow_Lps", MaximumOperatingAirFlow_Lps);
            }

            if (!double.IsNaN(CoolingEnableTemperature_C))
            {
                result.Add("CoolingEnableTemperature_C", CoolingEnableTemperature_C);
            }

            return result;
        }
    }
}
