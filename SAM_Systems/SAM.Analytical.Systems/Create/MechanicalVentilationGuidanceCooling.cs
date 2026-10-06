// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    public static partial class Create
    {
        /// <summary>
        /// SAM#123: materialises a manufacturer-guidance cooling unit's physical arrangement inside one air
        /// system already built from the MVRE topology - outdoor air, heat exchanger, <b>supply DX coil</b>,
        /// supply fan - by inserting the coil on the exchanger's supply outlet, and records what the TAS
        /// grounding needs: the exchanger, the coil, both fans, the room hosting the cooling-stat and every
        /// room's design and elevated airflows.
        /// <para>
        /// <b>Topology only.</b> Nothing here states a coil setpoint, a duty or a control law in TAS terms;
        /// the strategy travels with the record and is grounded natively (SAM_Tas). No recirculation branch is
        /// built: the coil sits on the ventilation supply path, which is the product's own arrangement.
        /// </para>
        /// </summary>
        internal static bool MechanicalVentilationGuidanceCooling(
            MechanicalVentilationContext context,
            SystemPlantRoom systemPlantRoom,
            AirSystem airSystem,
            AirHandlingUnit airHandlingUnit,
            string key_AirSystem,
            List<Guid> spaceGuids,
            SystemExchanger systemExchanger,
            SystemFan systemFan_Supply,
            SystemFan systemFan_Extract,
            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings)
        {
            string label = string.Format("Air handling unit '{0}' has a manufacturer-guidance cooling unit", airHandlingUnit.Name);

            string refusal = mechanicalVentilationGuidanceSettings?.Refusal() ?? "states no guidance settings.";
            if (mechanicalVentilationGuidanceSettings == null || mechanicalVentilationGuidanceSettings.Refusal() != null)
            {
                context.Refuse(string.Format("{0} that {1}", label, refusal));
                return false;
            }

            if (systemExchanger == null || systemFan_Supply == null || systemFan_Extract == null)
            {
                context.Refuse(string.Format("{0}, but its exchanger or fans could not be identified in its air system.", label));
                return false;
            }

            if (spaceGuids == null || spaceGuids.Count == 0)
            {
                context.Refuse(string.Format("{0} and serves no room.", label));
                return false;
            }

            //---------------------------------------------------------------------------------------------
            //Design duties and their elevated counterparts, in design proportions on each side.
            //---------------------------------------------------------------------------------------------

            Dictionary<Guid, double> designSupply_Lps = new Dictionary<Guid, double>();
            Dictionary<Guid, double> designExtract_Lps = new Dictionary<Guid, double>();

            foreach (Guid guid_Space in spaceGuids)
            {
                if (context.Dictionary_SupplyDuty_Lps.TryGetValue(guid_Space, out double supply_Lps) && supply_Lps > 0)
                {
                    designSupply_Lps[guid_Space] = supply_Lps;
                }

                if (context.Dictionary_ExtractDuty_Lps.TryGetValue(guid_Space, out double extract_Lps) && extract_Lps > 0)
                {
                    designExtract_Lps[guid_Space] = extract_Lps;
                }
            }

            double elevated_Lps = mechanicalVentilationGuidanceSettings.OperatingStrategy.ElevatedAirFlow_Lps;

            MechanicalVentilationOperatingFlows mechanicalVentilationOperatingFlows = new MechanicalVentilationOperatingFlows(designSupply_Lps, designExtract_Lps, elevated_Lps);
            string refusal_Flows = mechanicalVentilationOperatingFlows.Refusal();
            if (refusal_Flows != null)
            {
                context.Refuse(string.Format("{0}, but its elevated airflow could not be distributed: {1}", label, refusal_Flows));
                return false;
            }

            IReadOnlyDictionary<Guid, double> elevatedSupply_Lps = mechanicalVentilationOperatingFlows.OperatingSupply_Lps;
            IReadOnlyDictionary<Guid, double> elevatedExtract_Lps = mechanicalVentilationOperatingFlows.OperatingExtract_Lps;

            Guid guid_Space_Stat = mechanicalVentilationGuidanceSettings.CoolingStatSpaceGuid;
            if (guid_Space_Stat == System.Guid.Empty || !spaceGuids.Contains(guid_Space_Stat))
            {
                context.Refuse(string.Format("{0}, but its selected cooling control room is missing or is not served by this unit. Confirm a room in the cooled dwelling before simulation.", label));
                return false;
            }

            if (!designSupply_Lps.ContainsKey(guid_Space_Stat))
            {
                context.Refuse(string.Format("{0}, but its selected cooling control room is not supplied by this unit.", label));
                return false;
            }

            List<MechanicalVentilationGuidanceRoom> rooms = new List<MechanicalVentilationGuidanceRoom>(spaceGuids.Count);

            foreach (Guid guid_Space in spaceGuids)
            {
                if (!context.Dictionary_SystemSpace.TryGetValue(guid_Space, out SystemSpace systemSpace) || systemSpace == null)
                {
                    context.Refuse(string.Format("{0}, but room '{1}' has no materialised system space.", label, context.Dictionary_Space[guid_Space].Name));
                    return false;
                }

                rooms.Add(new MechanicalVentilationGuidanceRoom(
                    guid_Space,
                    systemSpace.Guid,
                    designSupply_Lps.TryGetValue(guid_Space, out double supply_Design) ? supply_Design : 0.0,
                    designExtract_Lps.TryGetValue(guid_Space, out double extract_Design) ? extract_Design : 0.0,
                    elevatedSupply_Lps.TryGetValue(guid_Space, out double supply_Elevated) ? supply_Elevated : 0.0,
                    elevatedExtract_Lps.TryGetValue(guid_Space, out double extract_Elevated) ? extract_Elevated : 0.0));
            }

            //---------------------------------------------------------------------------------------------
            //The supply DX coil, on the exchanger's supply outlet (connector 1) ahead of the supply fan.
            //---------------------------------------------------------------------------------------------

            List<ISystemConnection> systemConnections_ExchangerToFan = systemPlantRoom.GetSystemConnections(systemExchanger, systemFan_Supply, new SystemType(airSystem));
            if (systemConnections_ExchangerToFan == null || systemConnections_ExchangerToFan.Count != 1)
            {
                context.Refuse(string.Format(
                    "{0}, but its exchanger is joined to its supply fan by {1} connection(s); the supply DX coil goes on exactly one - the exchanger's supply outlet into the supply fan.",
                    label,
                    systemConnections_ExchangerToFan == null ? 0 : systemConnections_ExchangerToFan.Count));

                return false;
            }

            if (Core.Systems.Query.SystemConnection(systemPlantRoom, systemExchanger, 1) == null || !systemPlantRoom.Disconnect(systemExchanger, 1))
            {
                context.Refuse(string.Format("{0}, but its exchanger's supply outlet (connector 1) is not the connection into the supply fan, so the coil's place is not where the product puts it.", label));
                return false;
            }

            Guid guid_DXCoil = context.Guid_Derived("GuidanceCooling", "DXCoil", key_AirSystem);
            Guid guid_Connection_ExchangerToDXCoil = context.Guid_Derived("GuidanceCooling", "Connection:Exchanger-DXCoil", key_AirSystem);
            Guid guid_Connection_DXCoilToFan = context.Guid_Derived("GuidanceCooling", "Connection:DXCoil-Fan", key_AirSystem);

            if (context.HasRefusals)
            {
                return false;
            }

            SystemDXCoil systemDXCoil = new SystemDXCoil("Manufacturer guidance cooling coil")
            {
                Description = "Supply DX coil of the selected product's cooling module (manufacturer guidance, not certified performance). Its control law, supply rule and duty are grounded natively from the product's operating strategy.",
                HeatingDuty = new SizableValue(0.0) { SizingType = SizingType.Value },
            };

            //Drawn where it is connected: between the exchanger's supply outlet and the supply fan, on the fan's row.
            //Presentation only (the ducts, not the drawing, are the topology) - but "fan X - 4" put it 4 units LEFT of
            //the fan, which on the MVRE template is to the left of the fresh-air inlet and read as a coil upstream of
            //the heat exchanger (2026-09-29 real project).
            Point2D point2D_Fan = (systemFan_Supply as DisplaySystemFan)?.SystemGeometry?.CoordinateSystem2D?.Origin;
            Point2D point2D_Exchanger = (systemExchanger as DisplaySystemExchanger)?.SystemGeometry?.CoordinateSystem2D?.Origin;
            Point2D point2D_DXCoil = point2D_Fan == null
                ? new Point2D(0, 0)
                : point2D_Exchanger == null
                    ? new Point2D(point2D_Fan.X - 1.0, point2D_Fan.Y)
                    : new Point2D((point2D_Exchanger.X + point2D_Fan.X) / 2.0, point2D_Fan.Y);

            DisplaySystemDXCoil displaySystemDXCoil = systemDXCoil.DisplayObject<DisplaySystemDXCoil>(point2D_DXCoil, Query.DefaultDisplaySystemManager());
            if (displaySystemDXCoil == null || !(displaySystemDXCoil.Duplicate(guid_DXCoil) is DisplaySystemDXCoil displaySystemDXCoil_Keyed))
            {
                context.Refuse(string.Format("{0}, but no supply DX coil could be drawn for it.", label));
                return false;
            }

            systemPlantRoom.Add(displaySystemDXCoil_Keyed);
            systemPlantRoom.Connect(airSystem, displaySystemDXCoil_Keyed);

            AttachRecirculation(systemPlantRoom, airSystem, systemExchanger, 1, displaySystemDXCoil_Keyed, 0, guid_Connection_ExchangerToDXCoil);
            AttachRecirculation(systemPlantRoom, airSystem, displaySystemDXCoil_Keyed, 1, systemFan_Supply, 0, guid_Connection_DXCoilToFan);

            context.GuidanceCoolings.Add(new MechanicalVentilationGuidanceCooling(
                airHandlingUnit.Guid,
                airSystem.Guid,
                ((SAMObject)systemExchanger).Guid,
                guid_DXCoil,
                ((SAMObject)systemFan_Supply).Guid,
                ((SAMObject)systemFan_Extract).Guid,
                guid_Space_Stat,
                rooms,
                mechanicalVentilationGuidanceSettings));

            context.Note(string.Format(
                "{0}: supply DX coil after the exchanger, {1} room(s), design {2:0.###} l/s supply / {3:0.###} l/s extract, elevated {4:0.###} l/s on each side while cooling, cooling-stat in '{5}' (manufacturer guidance, provisional).",
                label,
                rooms.Count,
                mechanicalVentilationOperatingFlows.DesignSupplyTotal_Lps,
                mechanicalVentilationOperatingFlows.DesignExtractTotal_Lps,
                elevated_Lps,
                context.Dictionary_Space[guid_Space_Stat].Name));

            return true;
        }
    }
}
