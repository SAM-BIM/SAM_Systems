// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core;
using SAM.Core.Systems;
using System;

namespace SAM.Analytical.Systems
{
    public class SystemSpace : SystemComponent, ISystemSpace, IAirSystemComponent
    {
        private double area;
        private double volume;

        public ModifiableValue TemperatureSetpoint { get; set; }
        public ModifiableValue RelativeHumiditySetpoint { get; set; }
        public ModifiableValue PollutantSetpoint { get; set; }
        public bool DisplacementVentilation { get; set; }
        public bool ModelInterzoneFlow { get; set; }
        public bool ModelVentilationFlow { get; set; }
        public DesignConditionSizedFlowValue FlowRate { get; set; }
        public DesignConditionSizedFlowValue FreshAir { get; set; }

        public double MinimumDesignFlowFraction { get; set; }

        public SystemSpace(string name, double area, double volume, ModifiableValue temperatureSetpoint, ModifiableValue relativeHumiditySetpoint, ModifiableValue pollutantSetpoint, bool displacementVentilation, bool modelInterzoneFlow, bool modelVentilationFlow, DesignConditionSizedFlowValue flowRate, DesignConditionSizedFlowValue freshAir, double minimumDesignFlowFraction)
            : base(name)
        {
            this.area = area;
            this.volume = volume;
            TemperatureSetpoint = temperatureSetpoint?.Clone();
            RelativeHumiditySetpoint = relativeHumiditySetpoint?.Clone();
            PollutantSetpoint = pollutantSetpoint?.Clone();
            DisplacementVentilation = displacementVentilation;
            ModelInterzoneFlow = modelInterzoneFlow;
            ModelVentilationFlow = modelVentilationFlow;
            FlowRate = flowRate?.Clone();
            FreshAir = freshAir?.Clone();
            MinimumDesignFlowFraction = minimumDesignFlowFraction;
        }

        public SystemSpace(JsonObject jObject)
            : base(jObject)
        {
            FromJsonObject(jObject);
        }

        public SystemSpace(SystemSpace systemSpace)
            : base(systemSpace)
        {
            if (systemSpace != null)
            {
                area = systemSpace.area;
                volume = systemSpace.volume;

                TemperatureSetpoint = systemSpace.TemperatureSetpoint?.Clone();
                RelativeHumiditySetpoint = systemSpace.RelativeHumiditySetpoint?.Clone();
                PollutantSetpoint = systemSpace.PollutantSetpoint?.Clone();
                DisplacementVentilation = systemSpace.DisplacementVentilation;
                ModelInterzoneFlow = systemSpace.ModelInterzoneFlow;
                ModelVentilationFlow = systemSpace.ModelVentilationFlow;
                //Cloned, not shared. Every sibling component clones its flow values, and
                //Modify.UpdateSpaceAirflows writes SizedFlowValue.Value in place - so a shared reference is
                //a live route from a copy back into whatever it was copied from, including a template.
                FlowRate = systemSpace.FlowRate?.Clone();
                FreshAir = systemSpace.FreshAir?.Clone();
                MinimumDesignFlowFraction = systemSpace.MinimumDesignFlowFraction;
            }
        }

        public SystemSpace(System.Guid guid, SystemSpace systemSpace)
            : base(guid, systemSpace)
        {
            if (systemSpace != null)
            {
                area = systemSpace.area;
                volume = systemSpace.volume;

                TemperatureSetpoint = systemSpace.TemperatureSetpoint?.Clone();
                RelativeHumiditySetpoint = systemSpace.RelativeHumiditySetpoint?.Clone();
                PollutantSetpoint = systemSpace.PollutantSetpoint?.Clone();
                DisplacementVentilation = systemSpace.DisplacementVentilation;
                ModelInterzoneFlow = systemSpace.ModelInterzoneFlow;
                ModelVentilationFlow = systemSpace.ModelVentilationFlow;
                //Cloned, not shared. Every sibling component clones its flow values, and
                //Modify.UpdateSpaceAirflows writes SizedFlowValue.Value in place - so a shared reference is
                //a live route from a copy back into whatever it was copied from, including a template.
                FlowRate = systemSpace.FlowRate?.Clone();
                FreshAir = systemSpace.FreshAir?.Clone();
                MinimumDesignFlowFraction = systemSpace.MinimumDesignFlowFraction;
            }
        }

        public double Area
        {
            get
            {
                return area;
            }
        }

        public double Volume
        {
            get
            {
                return volume;
            }
        }

        public override SystemConnectorManager SystemConnectorManager
        {
            get
            {
                return Core.Systems.Create.SystemConnectorManager
                (
                    Core.Systems.Create.SystemConnector<AirSystem>(Direction.In),
                    Core.Systems.Create.SystemConnector<AirSystem>(Direction.Out),
                    Core.Systems.Create.SystemConnector<IControlSystem>(Direction.Out)
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

            if (jObject.ContainsKey("Area"))
            {
                area = jObject["Area"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("Volume"))
            {
                volume = jObject["Volume"]?.GetValue<double>() ?? default(double);
            }

            if (jObject.ContainsKey("TemperatureSetpoint"))
            {
                TemperatureSetpoint = Core.Query.IJSAMObject<ModifiableValue>(jObject["TemperatureSetpoint"] as JsonObject);
            }

            if (jObject.ContainsKey("RelativeHumiditySetpoint"))
            {
                RelativeHumiditySetpoint = Core.Query.IJSAMObject<ModifiableValue>(jObject["RelativeHumiditySetpoint"] as JsonObject);
            }

            if (jObject.ContainsKey("PollutantSetpoint"))
            {
                PollutantSetpoint = Core.Query.IJSAMObject<ModifiableValue>(jObject["PollutantSetpoint"] as JsonObject);
            }

            if (jObject.ContainsKey("DisplacementVentilation"))
            {
                DisplacementVentilation = jObject["DisplacementVentilation"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("ModelInterzoneFlow"))
            {
                ModelInterzoneFlow = jObject["ModelInterzoneFlow"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("ModelVentilationFlow"))
            {
                ModelVentilationFlow = jObject["ModelVentilationFlow"]?.GetValue<bool>() ?? default(bool);
            }

            if (jObject.ContainsKey("FlowRate"))
            {
                FlowRate = Core.Query.IJSAMObject<DesignConditionSizedFlowValue>(jObject["FlowRate"] as JsonObject);
            }

            if (jObject.ContainsKey("FreshAir"))
            {
                FreshAir = Core.Query.IJSAMObject<DesignConditionSizedFlowValue>(jObject["FreshAir"] as JsonObject);
            }

            if (jObject.ContainsKey("MinimumDesignFlowFraction"))
            {
                MinimumDesignFlowFraction = jObject["MinimumDesignFlowFraction"]?.GetValue<double>() ?? default(double);
            }

            return true;
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject result = base.ToJsonObject();
            if (result == null)
            {
                return null;
            }

            if (!double.IsNaN(area))
            {
                result.Add("Area", area);
            }

            if (!double.IsNaN(volume))
            {
                result.Add("Volume", volume);
            }

            if(TemperatureSetpoint != null)
            {
                result.Add("TemperatureSetpoint", TemperatureSetpoint.ToJsonObject());
            }

            if (RelativeHumiditySetpoint != null)
            {
                result.Add("RelativeHumiditySetpoint", RelativeHumiditySetpoint.ToJsonObject());
            }

            if (PollutantSetpoint != null)
            {
                result.Add("PollutantSetpoint", PollutantSetpoint.ToJsonObject());
            }

            result.Add("DisplacementVentilation", DisplacementVentilation);

            result.Add("ModelVentilationFlow", ModelVentilationFlow);

            result.Add("ModelInterzoneFlow", ModelInterzoneFlow);

            if (FlowRate != null)
            {
                result.Add("FlowRate", FlowRate.ToJsonObject());
            }

            if (FreshAir != null)
            {
                result.Add("FreshAir", FreshAir.ToJsonObject());
            }

            if (!double.IsNaN(MinimumDesignFlowFraction))
            {
                result.Add("MinimumDesignFlowFraction", MinimumDesignFlowFraction);
            }

            return result;
        }

        public override SystemObject Duplicate(Guid? guid = null)
        {
            return new SystemSpace(guid == null ? Guid.NewGuid() : guid.Value, this);
        }
    }
}
