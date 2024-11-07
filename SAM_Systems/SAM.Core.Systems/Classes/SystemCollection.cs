﻿using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace SAM.Core.Systems
{
    public abstract class SystemCollection<T> : SystemComponent, ISystemCollection where T : ISystem
    {
        public SystemCollection()
            : base(string.Empty)
        {

        }

        public SystemCollection(string name)
            : base(name)
        {

        }

        public SystemCollection(SystemCollection<T> systemGroup)
            : base(systemGroup)
        {

        }

        public SystemCollection(JObject jObject)
            : base(jObject)
        {

        }

        public SystemType SystemType
        {
            get
            {
                return new SystemType(typeof(T));
            }
        }

        public override SystemConnectorManager SystemConnectorManager
        {
            get
            {
                return Core.Systems.Create.SystemConnectorManager
                (
                    Core.Systems.Create.SystemConnector<T>(Direction.In, 1),
                    Core.Systems.Create.SystemConnector<T>(Direction.Out, 1),
                    //Core.Systems.Create.SystemConnector<ElectricalSystem>(),
                    Core.Systems.Create.SystemConnector<IControlSystem>()
                );
            }
        }

        public virtual bool IsValid(ISystemComponent systemComponent)
        {
            if(systemComponent == null)
            {
                return false;
            }

            SystemConnectorManager systemConnectorManager = systemComponent.SystemConnectorManager;
            if(systemConnectorManager == null)
            {
                return false;
            }

            List<int> indexes = systemConnectorManager.GetIndexes(SystemType);
            
            return indexes != null && indexes.Count != 0;
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