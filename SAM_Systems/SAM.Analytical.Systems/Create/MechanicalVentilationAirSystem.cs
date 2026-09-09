// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Systems;
using SAM.Geometry.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    public static partial class Create
    {
        /// <summary>
        /// Materialises one physical air handling unit into the <b>shared</b> destination plant room: its
        /// own deterministically re-keyed copy of the template's air-system subgraph, one
        /// <c>SystemSpace</c> per space it serves, and that space's design supply and extract legs.
        /// <para>
        /// <b>It never creates or adds a plant room.</b> One materialisation produces one plant room
        /// holding N air systems, so this is called once per unit against the same instance - which is
        /// what stops a second unit replacing the first in the energy centre's guid-keyed plant-room
        /// dictionary, and what stops the whole non-air plant being cloned once per unit.
        /// </para>
        /// </summary>
        /// <returns>False where a refusal was raised; the caller then discards the whole graph.</returns>
        internal static bool MechanicalVentilationAirSystem(MechanicalVentilationContext context, SystemPlantRoom systemPlantRoom, AirSystem airSystem_Template, AirHandlingUnit airHandlingUnit, List<Guid> spaceGuids)
        {
            //---------------------------------------------------------------------------------------------
            //The air-system key. Every per-unit identity below derives through it, and it carries the
            //unit's own guid - which is why two units cannot derive the same guid for anything.
            //---------------------------------------------------------------------------------------------

            string key_AirSystem = string.Format("{0}#{1}", Query.MechanicalVentilationGuidComponent(airHandlingUnit.Guid), context.Key_EnergyCentre);

            Guid guid_AirSystem = context.Guid_Derived(
                "AirSystem",
                Query.MechanicalVentilationGuidComponent(airHandlingUnit.Guid),
                Query.MechanicalVentilationGuidComponent(airSystem_Template.Guid),
                context.Key_EnergyCentre);

            if (context.HasRefusals)
            {
                return false;
            }

            //---------------------------------------------------------------------------------------------
            //Walk the template air-system subgraph once, deciding re-key or share for everything it
            //reaches. Each physical unit genuinely has its own fans, dampers and junctions; the electrical,
            //hot water, heating, cooling and refrigerant collections are one shared installation and are
            //not copied per unit.
            //---------------------------------------------------------------------------------------------

            MechanicalVentilationTemplateSubgraph(systemPlantRoom, airSystem_Template, context.Settings.MaterialiseSystemSpaceComponents, out Dictionary<Guid, ISystemJSAMObject> dictionary_Rekey, out Dictionary<Guid, ISystemJSAMObject> dictionary_Shared);

            //---------------------------------------------------------------------------------------------
            //Re-key. Derived guids, never Guid.NewGuid(), so the same design against the same template
            //always produces the same identities.
            //---------------------------------------------------------------------------------------------

            Dictionary<Guid, ISystemJSAMObject> dictionary_New = new Dictionary<Guid, ISystemJSAMObject>();

            foreach (KeyValuePair<Guid, ISystemJSAMObject> keyValuePair in dictionary_Rekey)
            {
                if (!(keyValuePair.Value is SystemObject systemObject))
                {
                    context.Refuse(string.Format("The topology template could not be copied in isolation: '{0}' is not a system object and cannot be re-keyed.", keyValuePair.Value?.GetType()?.Name));
                    return false;
                }

                Guid guid_New = keyValuePair.Key == airSystem_Template.Guid
                    ? guid_AirSystem
                    : context.Guid_Derived("TemplateComponent", Query.MechanicalVentilationGuidComponent(keyValuePair.Key), key_AirSystem);

                if (context.HasRefusals)
                {
                    return false;
                }

                SystemObject systemObject_New = systemObject.Duplicate(guid_New);

                if (systemObject_New == null || systemObject_New.Guid != guid_New)
                {
                    context.Refuse(string.Format("The topology template could not be copied in isolation: '{0}' did not re-key.", systemObject.Name));
                    return false;
                }

                //Named after the physical unit, so the graph reads as the building does. Nothing keys on
                //this - the identities above are derived from guids alone.
                if (systemObject_New is AirSystem || systemObject_New is ISystemGroup)
                {
                    systemObject_New.Name = airHandlingUnit.Name;
                }

                if (systemObject_New is SystemFan systemFan && context.Settings.Schedule != null)
                {
                    systemFan.ScheduleName = context.Settings.Schedule.Name;
                }

                dictionary_New[keyValuePair.Key] = systemObject_New;
            }

            //Re-point every copied connection at the copies, before anything is added - SystemPlantRoom.Add
            //clones, so a reference fixed afterwards would be fixed on the wrong instance.
            foreach (ISystemJSAMObject systemJSAMObject in dictionary_New.Values)
            {
                if (!(systemJSAMObject is SystemConnection systemConnection))
                {
                    continue;
                }

                foreach (ObjectReference objectReference in systemConnection.ObjectReferences ?? new List<ObjectReference>())
                {
                    if (!System.Guid.TryParse(objectReference?.Reference?.ToString(), out Guid guid))
                    {
                        continue;
                    }

                    if (dictionary_New.TryGetValue(guid, out ISystemJSAMObject systemJSAMObject_New) && systemJSAMObject_New is SAMObject sAMObject_New)
                    {
                        systemConnection.Reassign(objectReference, new ObjectReference(sAMObject_New));
                    }
                }
            }

            foreach (ISystemJSAMObject systemJSAMObject in dictionary_New.Values)
            {
                Add(systemPlantRoom, systemJSAMObject);
            }

            //Rebuild the relations the template stated: copy-to-copy where both ends were re-keyed, and
            //copy-to-shared where the partner is the one shared instance.
            foreach (KeyValuePair<Guid, ISystemJSAMObject> keyValuePair in dictionary_Rekey)
            {
                ISystemJSAMObject systemJSAMObject_New = dictionary_New[keyValuePair.Key];

                foreach (ISystemJSAMObject systemJSAMObject_Related in systemPlantRoom.GetRelatedObjects(keyValuePair.Value) ?? new List<ISystemJSAMObject>())
                {
                    if (!(systemJSAMObject_Related is SAMObject sAMObject_Related))
                    {
                        continue;
                    }

                    if (dictionary_New.TryGetValue(sAMObject_Related.Guid, out ISystemJSAMObject systemJSAMObject_Partner))
                    {
                        Relate(systemPlantRoom, systemJSAMObject_New, systemJSAMObject_Partner);
                    }
                    else if (dictionary_Shared.ContainsKey(sAMObject_Related.Guid))
                    {
                        Relate(systemPlantRoom, systemJSAMObject_New, systemJSAMObject_Related);
                    }
                }
            }

            AirSystem airSystem = dictionary_New[airSystem_Template.Guid] as AirSystem;

            if (airSystem == null)
            {
                context.Refuse(string.Format("The topology template could not be copied in isolation: the air system of '{0}' did not copy.", airHandlingUnit.Name));
                return false;
            }

            context.Dictionary_AirSystem[airHandlingUnit.Guid] = airSystem;

            context.Bindings.Add(new MechanicalVentilationBinding(MechanicalVentilationBindingType.AirSystem, airHandlingUnit.Guid, airSystem.Guid));

            //---------------------------------------------------------------------------------------------
            //Prototype discovery, on this unit's own re-keyed copy - so the attachment components are this
            //unit's damper and fan, and not another unit's or the template's.
            //---------------------------------------------------------------------------------------------

            List<SystemSpace> systemSpaces_Prototype = new List<SystemSpace>();
            List<SystemSpace> systemSpaces_Copied = new List<SystemSpace>();

            foreach (ISystemJSAMObject systemJSAMObject in dictionary_New.Values)
            {
                if (systemJSAMObject is SystemSpace systemSpace_Copied)
                {
                    systemSpaces_Copied.Add(systemSpace_Copied);

                    if (Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace_Copied, 0) != null && Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace_Copied, 1) != null)
                    {
                        systemSpaces_Prototype.Add(systemSpace_Copied);
                    }
                }
            }

            if (systemSpaces_Prototype.Count == 0)
            {
                context.Refuse("The template's air system carries no space attached on both its supply and its extract connector, so there is nothing to materialise rooms from.");
                return false;
            }

            if (systemSpaces_Prototype.Count > 1)
            {
                context.Refuse(string.Format("The template's air system carries {0} spaces attached on both its supply and its extract connector, so the prototype is ambiguous.", systemSpaces_Prototype.Count));
                return false;
            }

            SystemSpace systemSpace_Prototype = systemSpaces_Prototype[0];

            if (systemSpace_Prototype.FlowRate == null || systemSpace_Prototype.FreshAir == null)
            {
                context.Refuse("The template's prototype space states no design flow rate or no fresh air flow, so a room's duty could not be written onto a copy of it.");
                return false;
            }

            if (systemSpace_Prototype.FlowRate.Value != systemSpace_Prototype.FreshAir.Value)
            {
                context.Refuse(string.Format("The template's prototype space states a design flow of {0} l/s and a fresh air flow of {1} l/s; a design duty cannot be split between them without inventing a fraction.", systemSpace_Prototype.FlowRate.Value, systemSpace_Prototype.FreshAir.Value));
                return false;
            }

            if (!TryGetAttachment(systemPlantRoom, systemSpace_Prototype, 0, out ISystemComponent systemComponent_Supply, out int index_Supply)
                || !TryGetAttachment(systemPlantRoom, systemSpace_Prototype, 1, out ISystemComponent systemComponent_Extract, out int index_Extract))
            {
                context.Refuse("The template's prototype space is attached to a component the template does not contain, so there is nothing to hang rooms on.");
                return false;
            }

            //Read once per unit, never per space.
            List<ISystemJSAMObject> systemJSAMObjects_Prototype = systemPlantRoom.GetRelatedObjects(systemSpace_Prototype) ?? new List<ISystemJSAMObject>();

            //---------------------------------------------------------------------------------------------
            //The rooms. Ascending space guid, so the graph is byte-identical whatever order the model was
            //enumerated in.
            //---------------------------------------------------------------------------------------------

            foreach (Guid guid_Space in spaceGuids)
            {
                Space space = context.Dictionary_Space[guid_Space];

                bool hasSupply = context.Dictionary_SupplyDuty_Lps.TryGetValue(guid_Space, out double supply_Lps);
                bool hasExtract = context.Dictionary_ExtractDuty_Lps.TryGetValue(guid_Space, out double extract_Lps);

                //A brand-new flow value every time. Nothing existing is mutated, so the template is safe
                //independently of what any copy constructor does with a shared reference.
                DesignConditionSizedFlowValue flowRate = new DesignConditionSizedFlowValue(
                    hasSupply ? supply_Lps : 0.0,
                    systemSpace_Prototype.FlowRate.SizeFranction,
                    SizingType.Value,
                    systemSpace_Prototype.FlowRate.SizeValue1,
                    systemSpace_Prototype.FlowRate.SizeValue2,
                    systemSpace_Prototype.FlowRate.SizedFlowMethod,
                    systemSpace_Prototype.FlowRate.DesignConditionNames);

                DesignConditionSizedFlowValue freshAir = new DesignConditionSizedFlowValue(
                    hasSupply ? supply_Lps : 0.0,
                    systemSpace_Prototype.FreshAir.SizeFranction,
                    SizingType.Value,
                    systemSpace_Prototype.FreshAir.SizeValue1,
                    systemSpace_Prototype.FreshAir.SizeValue2,
                    systemSpace_Prototype.FreshAir.SizedFlowMethod,
                    systemSpace_Prototype.FreshAir.DesignConditionNames);

                double area = space.TryGetValue(SpaceParameter.Area, out double area_Temp) ? area_Temp : double.NaN;
                double volume = space.TryGetValue(SpaceParameter.Volume, out double volume_Temp) ? volume_Temp : double.NaN;

                SystemSpace systemSpace = new SystemSpace(
                    space.Name,
                    area,
                    volume,
                    systemSpace_Prototype.TemperatureSetpoint,
                    systemSpace_Prototype.RelativeHumiditySetpoint,
                    systemSpace_Prototype.PollutantSetpoint,
                    systemSpace_Prototype.DisplacementVentilation,
                    systemSpace_Prototype.ModelInterzoneFlow,
                    systemSpace_Prototype.ModelVentilationFlow,
                    flowRate,
                    freshAir,
                    systemSpace_Prototype.MinimumDesignFlowFraction);

                CopyParameters(systemSpace_Prototype, systemSpace);

                systemSpace.SetValue(SystemSpaceParameter.SpaceName, space.Name);

                if (systemSpace_Prototype is DisplaySystemSpace displaySystemSpace_Prototype)
                {
                    SystemGeometryInstance systemGeometryInstance = displaySystemSpace_Prototype.SystemGeometry;

                    systemSpace = new DisplaySystemSpace(systemSpace, systemGeometryInstance?.SystemGeometrySymbol, systemGeometryInstance?.CoordinateSystem2D?.Origin);
                }

                Guid guid_SystemSpace = context.Guid_Derived("SystemSpace", Query.MechanicalVentilationGuidComponent(guid_Space), key_AirSystem);

                if (context.HasRefusals)
                {
                    return false;
                }

                systemSpace = (SystemSpace)systemSpace.Duplicate(guid_SystemSpace);

                systemPlantRoom.Add(systemSpace);

                context.Dictionary_SystemSpace[guid_Space] = systemSpace;
                context.Dictionary_SystemSpaceGuid[guid_Space] = systemSpace.Guid;

                context.Bindings.Add(new MechanicalVentilationBinding(MechanicalVentilationBindingType.SystemSpace, guid_Space, systemSpace.Guid));

                systemPlantRoom.Connect(airSystem, (ISystemComponent)systemSpace);

                //The partners the prototype had: this unit's re-keyed group, and the shared collections.
                //Air-side connections and the prototype's own space components are handled below.
                foreach (ISystemJSAMObject systemJSAMObject in systemJSAMObjects_Prototype)
                {
                    if (systemJSAMObject is ISystemConnection || systemJSAMObject is Core.Systems.ISystem || systemJSAMObject is ISystemSpaceComponent)
                    {
                        continue;
                    }

                    Relate(systemPlantRoom, systemSpace, systemJSAMObject);
                }

                if (context.Settings.MaterialiseSystemSpaceComponents)
                {
                    foreach (ISystemJSAMObject systemJSAMObject in systemJSAMObjects_Prototype)
                    {
                        if (!(systemJSAMObject is ISystemSpaceComponent) || !(systemJSAMObject is SystemObject systemObject))
                        {
                            continue;
                        }

                        Guid guid_SpaceComponent = context.Guid_Derived(
                            "SystemSpaceComponent",
                            Query.MechanicalVentilationGuidComponent(systemObject.Guid),
                            Query.MechanicalVentilationGuidComponent(guid_Space),
                            key_AirSystem);

                        if (context.HasRefusals)
                        {
                            return false;
                        }

                        if (systemObject.Duplicate(guid_SpaceComponent) is ISystemSpaceComponent systemSpaceComponent)
                        {
                            systemPlantRoom.Connect(systemSpaceComponent, systemSpace);
                        }
                    }
                }

                //The supply leg. Built directly rather than through Connect(a, b, out connection, …), which
                //refuses an occupied connector slot - every room in this system hangs off the one AHU-side
                //connector, which is the many-to-one the shipped template and Query.Duplicate already
                //produce. Indexes are always explicit: an ObjectReference stored with index -1 does not
                //survive SystemConnection.FromJsonObject.
                if (hasSupply)
                {
                    Guid guid_Connection = context.Guid_Derived("SupplyConnection", Query.MechanicalVentilationGuidComponent(guid_Space), key_AirSystem);

                    if (context.HasRefusals)
                    {
                        return false;
                    }

                    SystemConnection systemConnection = Connection(systemPlantRoom, airSystem, systemComponent_Supply, index_Supply, systemSpace, 0, guid_Connection, supply_Lps);

                    foreach (VentilationTerminal ventilationTerminal in Terminals(context, guid_Space, FlowClassification.Supply))
                    {
                        context.Bindings.Add(new MechanicalVentilationBinding(MechanicalVentilationBindingType.SupplyConnection, ventilationTerminal.Guid, systemConnection.Guid));
                    }
                }

                if (hasExtract)
                {
                    Guid guid_Connection = context.Guid_Derived("ExtractConnection", Query.MechanicalVentilationGuidComponent(guid_Space), key_AirSystem);

                    if (context.HasRefusals)
                    {
                        return false;
                    }

                    SystemConnection systemConnection = Connection(systemPlantRoom, airSystem, systemSpace, 1, systemComponent_Extract, index_Extract, guid_Connection, extract_Lps);

                    foreach (VentilationTerminal ventilationTerminal in Terminals(context, guid_Space, FlowClassification.Extract))
                    {
                        context.Bindings.Add(new MechanicalVentilationBinding(MechanicalVentilationBindingType.ExtractConnection, ventilationTerminal.Guid, systemConnection.Guid));
                    }
                }
            }

            //---------------------------------------------------------------------------------------------
            //This unit's prototypes have done their job. Remove(ISystemComponent) takes their connections
            //with them, and the same call takes their space components. Leaving them would put phantom
            //zones into the result.
            //---------------------------------------------------------------------------------------------

            foreach (SystemSpace systemSpace_Copied in systemSpaces_Copied)
            {
                foreach (ISystemSpaceComponent systemSpaceComponent in systemPlantRoom.GetRelatedObjects<ISystemSpaceComponent>(systemSpace_Copied) ?? new List<ISystemSpaceComponent>())
                {
                    systemPlantRoom.Remove(systemSpaceComponent);
                }

                systemPlantRoom.Remove((ISystemComponent)systemSpace_Copied);
            }

            context.Note(string.Format("Air handling unit '{0}' materialised as one air system serving {1} space(s).", airHandlingUnit.Name, spaceGuids.Count));

            return true;
        }

        /// <summary>
        /// The template's air-system subgraph, split into what each physical unit gets its own copy of and
        /// what every unit shares.
        /// <para>
        /// <b>Each physical unit genuinely has its own fans, dampers and junctions</b>, so those are
        /// re-keyed per unit. The electrical, hot water, heating, cooling and refrigerant collections are
        /// one installation the whole plant room shares - <c>SystemCollection</c> is an
        /// <c>ISystemComponent</c> rather than an <c>ISystem</c>, so the <c>ISystem</c> boundary alone does
        /// not hold them back and they need this explicit rule. Anything reachable only through another
        /// <c>ISystem</c> - the liquid plant - is not walked at all.
        /// </para>
        /// <para>
        /// The same split answers two questions with one walk: what to copy for a unit, and what of the
        /// template's own air plant to remove once every unit has been materialised.
        /// </para>
        /// </summary>
        internal static void MechanicalVentilationTemplateSubgraph(SystemPlantRoom systemPlantRoom, AirSystem airSystem_Template, bool materialiseSystemSpaceComponents, out Dictionary<Guid, ISystemJSAMObject> dictionary_Rekey, out Dictionary<Guid, ISystemJSAMObject> dictionary_Shared)
        {
            dictionary_Rekey = new Dictionary<Guid, ISystemJSAMObject>();
            dictionary_Shared = new Dictionary<Guid, ISystemJSAMObject>();

            SystemType systemType_AirSystem = new SystemType(airSystem_Template);

            dictionary_Rekey[airSystem_Template.Guid] = airSystem_Template;

            Queue<ISystemJSAMObject> queue = new Queue<ISystemJSAMObject>();
            queue.Enqueue(airSystem_Template);

            HashSet<Guid> guids_Walked = new HashSet<Guid> { airSystem_Template.Guid };

            while (queue.Count != 0)
            {
                ISystemJSAMObject systemJSAMObject = queue.Dequeue();

                foreach (ISystemJSAMObject systemJSAMObject_Related in systemPlantRoom.GetRelatedObjects(systemJSAMObject) ?? new List<ISystemJSAMObject>())
                {
                    if (!(systemJSAMObject_Related is SAMObject sAMObject_Related))
                    {
                        continue;
                    }

                    Guid guid = sAMObject_Related.Guid;

                    if (dictionary_Rekey.ContainsKey(guid) || dictionary_Shared.ContainsKey(guid))
                    {
                        continue;
                    }

                    bool rekey;
                    bool walk;

                    if (systemJSAMObject_Related is Core.Systems.ISystem)
                    {
                        //The liquid system, and anything reachable only through it. This is the boundary
                        //Modify.UpdateAirSystem gets from excludedTypes: { ISystem }.
                        continue;
                    }
                    else if (systemJSAMObject_Related is ISystemCollection)
                    {
                        rekey = false;
                        walk = false;
                    }
                    else if (systemJSAMObject_Related is ISystemSpaceComponent)
                    {
                        if (!materialiseSystemSpaceComponents)
                        {
                            continue;
                        }

                        rekey = true;
                        walk = false;
                    }
                    else if (systemJSAMObject_Related is ISystemConnection systemConnection_Related)
                    {
                        rekey = systemType_AirSystem.IsValid(systemConnection_Related.SystemType);
                        walk = rekey;
                    }
                    else if (systemJSAMObject_Related is IAirSystemComponent)
                    {
                        rekey = true;
                        walk = true;
                    }
                    else if (systemJSAMObject_Related is ISystemGroup systemGroup_Related)
                    {
                        rekey = systemType_AirSystem.IsValid(systemGroup_Related.SystemType);
                        walk = rekey;
                    }
                    else
                    {
                        rekey = false;
                        walk = false;
                    }

                    if (rekey)
                    {
                        dictionary_Rekey[guid] = systemJSAMObject_Related;
                    }
                    else
                    {
                        dictionary_Shared[guid] = systemJSAMObject_Related;
                    }

                    if (walk && guids_Walked.Add(guid))
                    {
                        queue.Enqueue(systemJSAMObject_Related);
                    }
                }
            }
        }

        /// <summary>
        /// One directional leg: constructed with explicit connector indexes, given its derived identity and
        /// its DESIGN airflow, added, and related to both its components and to the air system.
        /// </summary>
        private static SystemConnection Connection(SystemPlantRoom systemPlantRoom, AirSystem airSystem, ISystemComponent systemComponent_1, int index_1, ISystemComponent systemComponent_2, int index_2, Guid guid, double designFlowRate_Lps)
        {
            SystemConnection result = (SystemConnection)new SystemConnection(new SystemType(airSystem), systemComponent_1, index_1, systemComponent_2, index_2).Duplicate(guid);

            result.SetValue(SystemConnectionParameter.DesignFlowRate, designFlowRate_Lps);

            systemPlantRoom.Add((ISystemComponent)result);

            systemPlantRoom.Connect(result, systemComponent_1);
            systemPlantRoom.Connect(result, systemComponent_2);
            systemPlantRoom.Connect(systemComponent_1, systemComponent_2);

            //Cast to ISystemComponent on purpose: SystemConnection is one, and Connect(ISystem,
            //ISystemComponent) is a guid lookup, where Connect(ISystemConnection, ISystem) re-resolves every
            //endpoint through GetObjects<T>()'s uncached assembly walk - once per room, at five thousand
            //rooms.
            systemPlantRoom.Connect(airSystem, (ISystemComponent)result);

            return result;
        }

        /// <summary>The component and connector index at the other end of a prototype connector.</summary>
        private static bool TryGetAttachment(SystemPlantRoom systemPlantRoom, SystemSpace systemSpace, int index, out ISystemComponent systemComponent, out int index_Attachment)
        {
            systemComponent = null;
            index_Attachment = -1;

            ISystemConnection systemConnection = Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace, index);
            if (systemConnection == null)
            {
                return false;
            }

            ObjectReference objectReference_Space = new ObjectReference(systemSpace);

            foreach (ObjectReference objectReference in systemConnection.ObjectReferences ?? new List<ObjectReference>())
            {
                if (objectReference == objectReference_Space)
                {
                    continue;
                }

                if (!systemConnection.TryGetIndex(objectReference, out int index_Temp))
                {
                    continue;
                }

                ISystemComponent systemComponent_Temp = systemPlantRoom.GetSystemComponent<ISystemComponent>(objectReference);
                if (systemComponent_Temp == null)
                {
                    continue;
                }

                systemComponent = systemComponent_Temp;
                index_Attachment = index_Temp;
                return true;
            }

            return false;
        }

        /// <summary>The terminals of one direction of a space, in the ascending guid order they were indexed in.</summary>
        private static List<VentilationTerminal> Terminals(MechanicalVentilationContext context, Guid guid_Space, FlowClassification flowClassification)
        {
            List<VentilationTerminal> result = new List<VentilationTerminal>();

            if (!context.Dictionary_Terminal.TryGetValue(guid_Space, out List<VentilationTerminal> ventilationTerminals))
            {
                return result;
            }

            foreach (VentilationTerminal ventilationTerminal in ventilationTerminals)
            {
                if (ventilationTerminal.FlowClassification == flowClassification)
                {
                    result.Add(ventilationTerminal);
                }
            }

            return result;
        }

        /// <summary>The prototype's carried parameters, exactly the set <c>Query.Duplicate</c> carries.</summary>
        private static void CopyParameters(SystemSpace systemSpace_Prototype, SystemSpace systemSpace)
        {
            if (systemSpace_Prototype.TryGetValue(AirSystemComponentParameter.GroupIndex, out int groupIndex))
            {
                systemSpace.SetValue(AirSystemComponentParameter.GroupIndex, groupIndex);
            }

            CollectionLink collectionLink;

            if (systemSpace_Prototype.TryGetValue(AirSystemComponentParameter.ElectricalCollection, out collectionLink))
            {
                systemSpace.SetValue(AirSystemComponentParameter.ElectricalCollection, collectionLink);
            }

            if (systemSpace_Prototype.TryGetValue(SystemSpaceParameter.DomesticHotWaterCollection, out collectionLink))
            {
                systemSpace.SetValue(SystemSpaceParameter.DomesticHotWaterCollection, collectionLink);
            }

            if (systemSpace_Prototype.TryGetValue(SystemSpaceParameter.EquipmentElectricalCollection, out collectionLink))
            {
                systemSpace.SetValue(SystemSpaceParameter.EquipmentElectricalCollection, collectionLink);
            }

            if (systemSpace_Prototype.TryGetValue(SystemSpaceParameter.LightingElectricalCollection, out collectionLink))
            {
                systemSpace.SetValue(SystemSpaceParameter.LightingElectricalCollection, collectionLink);
            }
        }

        /// <summary>Adds one object through the typed overload its kind needs.</summary>
        internal static void Add(SystemPlantRoom systemPlantRoom, ISystemJSAMObject systemJSAMObject)
        {
            if (systemJSAMObject is Core.Systems.ISystem system)
            {
                systemPlantRoom.Add(system);
            }
            else if (systemJSAMObject is ISystemGroup systemGroup)
            {
                systemPlantRoom.Add(systemGroup);
            }
            else if (systemJSAMObject is ISystemSpace systemSpace)
            {
                systemPlantRoom.Add(systemSpace);
            }
            else if (systemJSAMObject is ISystemComponent systemComponent)
            {
                systemPlantRoom.Add(systemComponent);
            }
            else if (systemJSAMObject is ISystemSensor systemSensor)
            {
                systemPlantRoom.Add(systemSensor);
            }
            else if (systemJSAMObject is ISystemLabel systemLabel)
            {
                systemPlantRoom.Add(systemLabel);
            }
            else if (systemJSAMObject is ISystemResult systemResult)
            {
                systemPlantRoom.Add(systemResult);
            }
        }

        /// <summary>
        /// Relates two objects through the <c>Connect</c> overload their kinds need. Every one of these is
        /// a guid lookup on objects already added, so it stays O(1) at five thousand rooms.
        /// </summary>
        internal static void Relate(SystemPlantRoom systemPlantRoom, ISystemJSAMObject systemJSAMObject_1, ISystemJSAMObject systemJSAMObject_2)
        {
            if (systemJSAMObject_1 == null || systemJSAMObject_2 == null)
            {
                return;
            }

            if (systemJSAMObject_1 is Core.Systems.ISystem system_1)
            {
                Relate(systemPlantRoom, system_1, systemJSAMObject_2);
                return;
            }

            if (systemJSAMObject_2 is Core.Systems.ISystem system_2)
            {
                Relate(systemPlantRoom, system_2, systemJSAMObject_1);
                return;
            }

            if (systemJSAMObject_1 is ISystemGroup systemGroup_1)
            {
                Relate(systemPlantRoom, systemGroup_1, systemJSAMObject_2);
                return;
            }

            if (systemJSAMObject_2 is ISystemGroup systemGroup_2)
            {
                Relate(systemPlantRoom, systemGroup_2, systemJSAMObject_1);
                return;
            }

            if (systemJSAMObject_1 is ISystemSensor systemSensor_1)
            {
                Relate(systemPlantRoom, systemSensor_1, systemJSAMObject_2);
                return;
            }

            if (systemJSAMObject_2 is ISystemSensor systemSensor_2)
            {
                Relate(systemPlantRoom, systemSensor_2, systemJSAMObject_1);
                return;
            }

            if (systemJSAMObject_1 is ISystemSpaceComponent systemSpaceComponent_1 && systemJSAMObject_2 is ISystemSpace systemSpace_2)
            {
                systemPlantRoom.Connect(systemSpaceComponent_1, systemSpace_2);
                return;
            }

            if (systemJSAMObject_2 is ISystemSpaceComponent systemSpaceComponent_2 && systemJSAMObject_1 is ISystemSpace systemSpace_1)
            {
                systemPlantRoom.Connect(systemSpaceComponent_2, systemSpace_1);
                return;
            }

            if (systemJSAMObject_1 is ISystemComponent systemComponent_1 && systemJSAMObject_2 is ISystemComponent systemComponent_2)
            {
                systemPlantRoom.Connect(systemComponent_1, systemComponent_2);
            }
        }

        private static void Relate(SystemPlantRoom systemPlantRoom, Core.Systems.ISystem system, ISystemJSAMObject systemJSAMObject)
        {
            if (systemJSAMObject is ISystemGroup systemGroup)
            {
                systemPlantRoom.Connect(system, systemGroup);
            }
            else if (systemJSAMObject is ISystemComponent systemComponent)
            {
                systemPlantRoom.Connect(system, systemComponent);
            }
            else if (systemJSAMObject is ISystemSensor systemSensor)
            {
                systemPlantRoom.Connect(systemSensor, system);
            }
        }

        private static void Relate(SystemPlantRoom systemPlantRoom, ISystemGroup systemGroup, ISystemJSAMObject systemJSAMObject)
        {
            if (systemJSAMObject is ISystemComponent systemComponent)
            {
                systemPlantRoom.Connect(systemGroup, systemComponent);
            }
            else if (systemJSAMObject is ISystemSensor systemSensor)
            {
                systemPlantRoom.Connect(systemGroup, systemSensor);
            }
        }

        private static void Relate(SystemPlantRoom systemPlantRoom, ISystemSensor systemSensor, ISystemJSAMObject systemJSAMObject)
        {
            if (systemJSAMObject is ISystemConnection systemConnection)
            {
                systemPlantRoom.Connect(systemSensor, systemConnection);
            }
            else if (systemJSAMObject is ISystemComponent systemComponent)
            {
                systemPlantRoom.Connect(systemSensor, systemComponent);
            }
        }
    }
}
