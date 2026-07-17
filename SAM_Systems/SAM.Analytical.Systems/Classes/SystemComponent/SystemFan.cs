// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;
using SAM.Core.Systems;
using System;

namespace SAM.Analytical.Systems
{
    public class SystemFan : SystemComponent, IAirSystemComponent
    {
        /// <summary>
        /// Fan total (overall) efficiency [0-1].
        /// </summary>
        public ModifiableValue OverallEfficiency { get; set; }
        public double HeatGainFactor { get; set; }
        /// <summary>
        /// Fan total pressure rise [Pa].
        /// </summary>
        public double Pressure { get; set; }
        /// <summary>
        /// Design flow rate, expressed in the units indicated by <see cref="DesignFlowType"/>. The Mollier
        /// bridge sets this in litres per second with <see cref="FlowRateType.Value"/>, matching Tas TPD fan
        /// flow units (Specific Fan Power is W/(l/s)).
        /// </summary>
        public SizedFlowValue DesignFlowRate { get; set; }
        /// <summary>
        /// How <see cref="DesignFlowRate"/> is expressed (e.g. an absolute value vs. a proportion of another
        /// quantity).
        /// </summary>
        public FlowRateType DesignFlowType { get; set; }
        public SizedFlowValue MinimumFlowRate { get; set; }
        public FlowRateType MinimumFlowType { get; set; }
        public double MinimumFlowFraction { get; set; }
        public double Capacity { get; set; }
        public FanControlType FanControlType { get; set; }
        public ModifiableValue PartLoad { get; set; }

        public string ScheduleName { get; set; }


        public SystemFan(string name)
            : base(name)
        {

        }

        public SystemFan(SystemFan systemFan)
            : base(systemFan)
        {
            if(systemFan != null)
            {
                OverallEfficiency = systemFan.OverallEfficiency?.Clone();
                HeatGainFactor = systemFan.HeatGainFactor;
                Pressure = systemFan.Pressure;
                DesignFlowRate = systemFan.DesignFlowRate?.Clone();
                DesignFlowType = systemFan.DesignFlowType;
                MinimumFlowRate = systemFan.MinimumFlowRate?.Clone();
                MinimumFlowType = systemFan.MinimumFlowType;
                MinimumFlowFraction = systemFan.MinimumFlowFraction;
                Capacity = systemFan.Capacity;
                FanControlType = systemFan.FanControlType;
                PartLoad = systemFan.PartLoad?.Clone();
                ScheduleName = systemFan.ScheduleName;
            }
        }

        public SystemFan(System.Guid guid, SystemFan systemFan)
            : base(guid, systemFan)
        {
            if (systemFan != null)
            {
                OverallEfficiency = systemFan.OverallEfficiency?.Clone();
                HeatGainFactor = systemFan.HeatGainFactor;
                Pressure = systemFan.Pressure;
                DesignFlowRate = systemFan.DesignFlowRate?.Clone();
                DesignFlowType = systemFan.DesignFlowType;
                MinimumFlowRate = systemFan.MinimumFlowRate?.Clone();
                MinimumFlowType = systemFan.MinimumFlowType;
                MinimumFlowFraction = systemFan.MinimumFlowFraction;
                Capacity = systemFan.Capacity;
                FanControlType = systemFan.FanControlType;
                PartLoad = systemFan.PartLoad?.Clone();
                ScheduleName = systemFan.ScheduleName;
            }
        }

        public SystemFan(JsonObject jObject)
            : base(jObject)
        {

        }

        public override SystemConnectorManager SystemConnectorManager
        {
            get
            {
                return Core.Systems.Create.SystemConnectorManager
                (
                    Core.Systems.Create.SystemConnector<AirSystem>(Direction.In, 1),
                    Core.Systems.Create.SystemConnector<AirSystem>(Direction.Out, 1),
                    //Core.Systems.Create.SystemConnector<ElectricalSystem>(),
                    Core.Systems.Create.SystemConnector<IControlSystem>()
                );
            }
        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            bool result = base.FromJsonObject(jObject);
            if (!result)
            {
                return result;
            }

            if (jObject.ContainsKey("OverallEfficiency"))
            {
                OverallEfficiency = Core.Query.IJSAMObject<ModifiableValue>(jObject["OverallEfficiency"] as JsonObject);
            }

            if (jObject.ContainsKey("HeatGainFactor"))
            {
                HeatGainFactor = jObject["HeatGainFactor"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("Pressure"))
            {
                Pressure = jObject["Pressure"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("DesignFlowRate"))
            {
                DesignFlowRate = Core.Query.IJSAMObject<SizedFlowValue>(jObject["DesignFlowRate"] as JsonObject);
            }

            if (jObject.ContainsKey("DesignFlowType"))
            {
                DesignFlowType = Core.Query.Enum<FlowRateType>(jObject["DesignFlowType"]?.GetValue<string>() ?? null);
            }

            if (jObject.ContainsKey("MinimumFlowRate"))
            {
                MinimumFlowRate = Core.Query.IJSAMObject<SizedFlowValue>(jObject["MinimumFlowRate"] as JsonObject);
            }

            if (jObject.ContainsKey("MinimumFlowType"))
            {
                MinimumFlowType = Core.Query.Enum<FlowRateType>(jObject["MinimumFlowType"]?.GetValue<string>() ?? null);
            }

            if (jObject.ContainsKey("MinimumFlowFraction"))
            {
                MinimumFlowFraction = jObject["MinimumFlowFraction"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("Capacity"))
            {
                Capacity = jObject["Capacity"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("FanControlType"))
            {
                FanControlType = Core.Query.Enum<FanControlType>(jObject["FanControlType"]?.GetValue<string>() ?? null);
            }

            if (jObject.ContainsKey("PartLoad"))
            {
                PartLoad = Core.Query.IJSAMObject<ModifiableValue>(jObject["PartLoad"] as JsonObject);
            }

            if (jObject.ContainsKey("ScheduleName"))
            {
                ScheduleName = jObject["ScheduleName"]?.GetValue<string>() ?? null;
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

            if (OverallEfficiency != null)
            {
                result.Add("OverallEfficiency", OverallEfficiency.ToJsonObject());
            }

            if (!double.IsNaN(HeatGainFactor))
            {
                result.Add("HeatGainFactor", HeatGainFactor);
            }

            if (!double.IsNaN(Pressure))
            {
                result.Add("Pressure", Pressure);
            }

            if (DesignFlowRate != null)
            {
                result.Add("DesignFlowRate", DesignFlowRate.ToJsonObject());
            }

            result.Add("DesignFlowType", DesignFlowType.ToString());

            if (MinimumFlowRate != null)
            {
                result.Add("MinimumFlowRate", MinimumFlowRate.ToJsonObject());
            }

            result.Add("MinimumFlowType", MinimumFlowType.ToString());

            if (!double.IsNaN(MinimumFlowFraction))
            {
                result.Add("MinimumFlowFraction", MinimumFlowFraction);
            }

            if (!double.IsNaN(Capacity))
            {
                result.Add("Capacity", Capacity);
            }

            result.Add("FanControlType", FanControlType.ToString());

            if (PartLoad != null)
            {
                result.Add("PartLoad", PartLoad.ToJsonObject());
            }

            if (ScheduleName != null)
            {
                result.Add("ScheduleName", ScheduleName);
            }

            return result;
        }

        public override SystemObject Duplicate(Guid? guid = null)
        {
            return new SystemFan(guid == null ? Guid.NewGuid() : guid.Value, this);
        }
    }
}
