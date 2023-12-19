﻿using Newtonsoft.Json.Linq;
using SAM.Core.Systems;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    public class SystemRadiator : SystemSpaceComponent
    {
        public double Efficiency { get; set; }
        public double Duty { get; set; }

        public override List<SystemConnector> SystemConnectors
        {
            get
            {
                return new List<SystemConnector>()
                {
                    Create.SystemConnector<LiquidSystem>(Core.Direction.In),
                    Create.SystemConnector<LiquidSystem>(Core.Direction.Out),
                    Create.SystemConnector<ElectricalSystem>(Core.Direction.In),
                };
            }
        }

        public SystemRadiator(string name)
            : base(name)
        {
        }

        public SystemRadiator(JObject jObject)
            : base(jObject)
        {
            FromJObject(jObject);
        }

        public SystemRadiator(SystemRadiator systemRadiator)
            : base(systemRadiator)
        {
            if (systemRadiator != null)
            {
                Efficiency = systemRadiator.Efficiency;
                Duty = systemRadiator.Duty;
            }
        }

        public override bool FromJObject(JObject jObject)
        {
            bool result = base.FromJObject(jObject);
            if (!result)
            {
                return result;
            }

            if (jObject.ContainsKey("Efficiency"))
            {
                Efficiency = jObject.Value<double>("Efficiency");
            }

            if (jObject.ContainsKey("Duty"))
            {
                Duty = jObject.Value<double>("Duty");
            }

            return true;
        }

        public override JObject ToJObject()
        {
            JObject result = base.ToJObject();
            if (result == null)
            {
                return null;
            }

            if (!double.IsNaN(Efficiency))
            {
                result.Add("Efficiency", Efficiency);
            }

            if (!double.IsNaN(Duty))
            {
                result.Add("Duty", Duty);
            }

            return result;
        }
    }
}
