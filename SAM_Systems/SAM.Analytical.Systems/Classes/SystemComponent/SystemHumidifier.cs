// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Text.Json.Nodes;
using SAM.Core.Systems;
using System;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// Abstract base for air humidification components. Concrete behaviour (spray/adiabatic, steam, direct
    /// evaporative) lives in the subtypes <see cref="SystemSprayHumidifier"/>, <see cref="SystemSteamHumidifier"/>
    /// and <see cref="SystemDirectEvaporativeCooler"/>, each of which has its own display symbol in
    /// SAM_DisplaySystemManager.JSON. The base is abstract so callers always choose a concrete, drawable type.
    /// </summary>
    public abstract class SystemHumidifier : SystemComponent, IAirSystemComponent
    {
        public string ScheduleName { get; set; }

        protected SystemHumidifier(string name)
            : base(name)
        {

        }

        protected SystemHumidifier(SystemHumidifier systemHumidifier)
            : base(systemHumidifier)
        {
            if(systemHumidifier != null)
            {
                ScheduleName = systemHumidifier.ScheduleName;
            }
        }

        protected SystemHumidifier(System.Guid guid, SystemHumidifier systemHumidifier)
            : base(guid, systemHumidifier)
        {
            if (systemHumidifier != null)
            {
                ScheduleName = systemHumidifier.ScheduleName;
            }
        }

        protected SystemHumidifier(JsonObject jObject)
            : base(jObject)
        {

        }

        public override SystemConnectorManager SystemConnectorManager
        {
            get
            {
                return Core.Systems.Create.SystemConnectorManager
                (
                    Core.Systems.Create.SystemConnector<AirSystem>(Core.Direction.In, 1),
                    Core.Systems.Create.SystemConnector<AirSystem>(Core.Direction.Out, 1),
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

            if (ScheduleName != null)
            {
                result.Add("ScheduleName", ScheduleName);
            }

            return result;
        }

        // Abstract: each concrete humidifier subtype provides its own Duplicate so the base type is never
        // instantiated. This keeps deep-copy/round-trip working without a drawable, concrete-less base instance.
        public abstract override SystemObject Duplicate(Guid? guid = null);
    }
}
