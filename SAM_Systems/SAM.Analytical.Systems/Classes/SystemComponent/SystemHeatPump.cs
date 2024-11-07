﻿using Newtonsoft.Json.Linq;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems
{
    public abstract class SystemHeatPump : SystemComponent
    {
        public SystemHeatPump(string name)
            : base(name)
        {

        }

        public SystemHeatPump(SystemHeatPump systemHeatPump)
            : base(systemHeatPump)
        {

        }

        public SystemHeatPump(JObject jObject)
            : base(jObject)
        {

        }

        public override bool FromJObject(JObject jObject)
        {
            return base.FromJObject(jObject);
        }

        public override JObject ToJObject()
        {
            return base.ToJObject();
        }
    }
}
