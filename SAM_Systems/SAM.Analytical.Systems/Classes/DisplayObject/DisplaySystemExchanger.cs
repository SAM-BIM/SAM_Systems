﻿using Newtonsoft.Json.Linq;
using SAM.Geometry.Planar;
using SAM.Geometry.Systems;

namespace SAM.Analytical.Systems
{
    public class DisplaySystemExchanger : SystemExchanger, IDisplaySystemObject<SystemGeometryInstance>
    {
        private SystemGeometryInstance systemGeometryInstance;

        public SystemGeometryInstance SystemGeometry
        {
            get
            {
                return systemGeometryInstance == null ? null : new SystemGeometryInstance(systemGeometryInstance);
            }
        }

        public DisplaySystemExchanger(SystemExchanger systemExchanger, SystemGeometrySymbol systemGeometrySymbol, Point2D location)
            :base(systemExchanger)
        {
            systemGeometryInstance = new SystemGeometryInstance(systemGeometrySymbol, location);
        }

        public DisplaySystemExchanger(DisplaySystemExchanger displaySystemExchanger)
            : base(displaySystemExchanger)
        {
            systemGeometryInstance = displaySystemExchanger?.systemGeometryInstance == null ? null : new SystemGeometryInstance(displaySystemExchanger?.systemGeometryInstance);
        }

        public DisplaySystemExchanger(JObject jObject)
            : base(jObject)
        {

        }

        public bool Move(Vector2D vector2D)
        {
            if(systemGeometryInstance == null || vector2D == null)
            {
                return false;
            }

            return systemGeometryInstance.Move(vector2D);
        }

        public bool Transform(ITransform2D transform2D)
        {
            if (systemGeometryInstance == null || transform2D == null)
            {
                return false;
            }

            return systemGeometryInstance.Transform(transform2D);
        }

        public override bool FromJObject(JObject jObject)
        {
            bool result = base.FromJObject(jObject);
            if(!result)
            {
                return result;
            }

            if(jObject.ContainsKey("SystemGeometryInstance"))
            {
                systemGeometryInstance = new SystemGeometryInstance(jObject.Value<JObject>("SystemGeometryInstance"));
            }

            return result;
        }

        public override JObject ToJObject()
        {
            JObject result = base.ToJObject();
            if(result == null)
            {
                return result;
            }

            if (systemGeometryInstance != null)
            {
                result.Add("SystemGeometryInstance", systemGeometryInstance.ToJObject());
            }

            return result;
        }
    }
}
