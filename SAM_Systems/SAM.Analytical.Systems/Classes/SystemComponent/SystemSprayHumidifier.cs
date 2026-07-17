// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;
using SAM.Core.Systems;
using System;

namespace SAM.Analytical.Systems
{
    public class SystemSprayHumidifier : SystemHumidifier
    {
        /// <summary>
        /// Off-humidifier relative humidity setpoint [%, 0-100]. Passes straight through to Tas TPD
        /// (<c>Convert.ToTPD</c> maps it verbatim), whose spray humidifiers control downstream relative
        /// humidity under their default flags.
        /// </summary>
        public ModifiableValue Setpoint { get; set; }

        /// <summary>
        /// Adiabatic humidification effectiveness [0-1]: the achieved humidity-ratio rise relative to the
        /// maximum possible (saturation) rise, clamped to [0,1].
        /// </summary>
        public ModifiableValue Effectiveness { get; set; }

        /// <summary>
        /// Mass flow rate of water evaporated [kg/s].
        /// </summary>
        public ISizableValue WaterFlowCapacity { get; set; }

        public ModifiableValue ElectricalLoad { get; set; }

        public SystemSprayHumidifier(string name)
            : base(name)
        {

        }

        public SystemSprayHumidifier(SystemSprayHumidifier systemSprayHumidifier)
            : base(systemSprayHumidifier)
        {
            if(systemSprayHumidifier != null)
            {
                Setpoint = systemSprayHumidifier.Setpoint?.Clone();
                Effectiveness = systemSprayHumidifier?.Effectiveness?.Clone();
                WaterFlowCapacity = systemSprayHumidifier?.WaterFlowCapacity?.Clone();
                ElectricalLoad = systemSprayHumidifier?.ElectricalLoad?.Clone();
            }
        }

        public SystemSprayHumidifier(System.Guid guid, SystemSprayHumidifier systemSprayHumidifier)
            : base(guid, systemSprayHumidifier)
        {
            if (systemSprayHumidifier != null)
            {
                Setpoint = systemSprayHumidifier.Setpoint?.Clone();
                Effectiveness = systemSprayHumidifier?.Effectiveness?.Clone();
                WaterFlowCapacity = systemSprayHumidifier?.WaterFlowCapacity?.Clone();
                ElectricalLoad = systemSprayHumidifier?.ElectricalLoad?.Clone();
            }
        }

        public SystemSprayHumidifier(JsonObject jObject)
            : base(jObject)
        {

        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            bool result = base.FromJsonObject(jObject);
            if (!result)
            {
                return result;
            }

            if (jObject.ContainsKey("Setpoint"))
            {
                Setpoint = Core.Query.IJSAMObject<ModifiableValue>(jObject["Setpoint"] as JsonObject);
            }

            if (jObject.ContainsKey("Effectiveness"))
            {
                Effectiveness = Core.Query.IJSAMObject<ModifiableValue>(jObject["Effectiveness"] as JsonObject);
            }

            if (jObject.ContainsKey("WaterFlowCapacity"))
            {
                WaterFlowCapacity = Core.Query.IJSAMObject<SizableValue>(jObject["WaterFlowCapacity"] as JsonObject);
            }

            if (jObject.ContainsKey("ElectricalLoad"))
            {
                ElectricalLoad = Core.Query.IJSAMObject<ModifiableValue>(jObject["ElectricalLoad"] as JsonObject);
            }

            return result;
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject result = base.ToJsonObject();
            if (result == null)
            {
                return null;
            }

            if (Setpoint != null)
            {
                result.Add("Setpoint", Setpoint.ToJsonObject());
            }

            if (Effectiveness != null)
            {
                result.Add("Effectiveness", Effectiveness.ToJsonObject());
            }

            if (WaterFlowCapacity != null)
            {
                result.Add("WaterFlowCapacity", WaterFlowCapacity.ToJsonObject());
            }

            if (ElectricalLoad != null)
            {
                result.Add("ElectricalLoad", ElectricalLoad.ToJsonObject());
            }

            return result;
        }

        public override SystemObject Duplicate(Guid? guid = null)
        {
            return new SystemSprayHumidifier(guid == null ? Guid.NewGuid() : guid.Value, this);
        }
    }
}
