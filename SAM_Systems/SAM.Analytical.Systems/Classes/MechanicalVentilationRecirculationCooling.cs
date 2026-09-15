// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// One materialised recirculation cooling branch - PR5B (SAM#111): which objects of the graph it is
    /// made of, which room each of its two dampers serves, and the settings it was materialised from.
    /// <para>
    /// <b>Lineage, not a second design.</b> Every guid here names an object in the materialised graph. A
    /// downstream conversion uses it to tell the branch apart from the ventilation legs by identity - never
    /// by a name or a component type - so the branch is never read as a second supply or extract leg of
    /// a room, and the ventilation design airflow reconciliation is exactly B0's.
    /// </para>
    /// </summary>
    public class MechanicalVentilationRecirculationCooling
    {
        private readonly List<MechanicalVentilationRecirculationRoom> rooms;

        public MechanicalVentilationRecirculationCooling(
            Guid guid_AirHandlingUnit,
            Guid guid_AirSystem,
            Guid guid_DXCoil,
            Guid guid_Fan,
            Guid guid_Fan_Source,
            Guid guid_Connection_DXCoilToFan,
            IEnumerable<MechanicalVentilationRecirculationRoom> rooms,
            MechanicalVentilationCoolingSettings mechanicalVentilationCoolingSettings)
        {
            Guid_AirHandlingUnit = guid_AirHandlingUnit;
            Guid_AirSystem = guid_AirSystem;
            Guid_DXCoil = guid_DXCoil;
            Guid_Fan = guid_Fan;
            Guid_Fan_Source = guid_Fan_Source;
            Guid_Connection_DXCoilToFan = guid_Connection_DXCoilToFan;
            this.rooms = rooms == null ? new List<MechanicalVentilationRecirculationRoom>() : new List<MechanicalVentilationRecirculationRoom>(rooms);
            settings = mechanicalVentilationCoolingSettings == null ? null : new MechanicalVentilationCoolingSettings(mechanicalVentilationCoolingSettings);
        }

        private readonly MechanicalVentilationCoolingSettings settings;

        /// <summary>The analytical unit whose cooling module this is.</summary>
        public Guid Guid_AirHandlingUnit { get; }

        /// <summary>The unit's own - and only - air system. The branch adds none.</summary>
        public Guid Guid_AirSystem { get; }

        /// <summary>The aggregate cooling coil.</summary>
        public Guid Guid_DXCoil { get; }

        /// <summary>
        /// The recirculation fan: a pressure-flow carrier for the internal loop, copied from the unit's
        /// supply fan so it states that fan's pressure and efficiency. It is a simulation surrogate, not a
        /// claim that the product holds a further fan, and it states no heat gain.
        /// </summary>
        public Guid Guid_Fan { get; }

        /// <summary>The unit's supply fan the recirculation fan was copied from.</summary>
        public Guid Guid_Fan_Source { get; }

        /// <summary>The one connection from the cooling coil to the recirculation fan.</summary>
        public Guid Guid_Connection_DXCoilToFan { get; }

        /// <summary>One row per room the branch serves, ordered by analytical space guid.</summary>
        public List<MechanicalVentilationRecirculationRoom> Rooms
        {
            get
            {
                return new List<MechanicalVentilationRecirculationRoom>(rooms);
            }
        }

        /// <summary>A copy of the settings the branch was materialised from - a new copy on every read, so nothing a caller does to it reaches the branch.</summary>
        public MechanicalVentilationCoolingSettings Settings
        {
            get
            {
                return settings == null ? null : new MechanicalVentilationCoolingSettings(settings);
            }
        }

        /// <summary>Every connection the branch added, so none of them is ever read as a ventilation leg.</summary>
        public HashSet<Guid> Guids_Connection
        {
            get
            {
                HashSet<Guid> result = new HashSet<Guid> { Guid_Connection_DXCoilToFan };
                foreach (MechanicalVentilationRecirculationRoom room in rooms)
                {
                    result.Add(room.Guid_Connection_RoomToReturnDamper);
                    result.Add(room.Guid_Connection_ReturnDamperToDXCoil);
                    result.Add(room.Guid_Connection_FanToSupplyDamper);
                    result.Add(room.Guid_Connection_SupplyDamperToRoom);
                }

                return result;
            }
        }

        /// <summary>Every component the branch added.</summary>
        public HashSet<Guid> Guids_Component
        {
            get
            {
                HashSet<Guid> result = new HashSet<Guid> { Guid_DXCoil, Guid_Fan };
                foreach (MechanicalVentilationRecirculationRoom room in rooms)
                {
                    result.Add(room.Guid_Damper_Return);
                    result.Add(room.Guid_Damper_Supply);
                }

                return result;
            }
        }
    }

    /// <summary>One room of a recirculation cooling branch: its two dampers, its four connections and its share of the ceiling.</summary>
    public class MechanicalVentilationRecirculationRoom
    {
        public MechanicalVentilationRecirculationRoom(
            Guid guid_Space,
            Guid guid_SystemSpace,
            Guid guid_Damper_Return,
            Guid guid_Damper_Supply,
            Guid guid_Connection_RoomToReturnDamper,
            Guid guid_Connection_ReturnDamperToDXCoil,
            Guid guid_Connection_FanToSupplyDamper,
            Guid guid_Connection_SupplyDamperToRoom,
            double designFlowRate_Lps)
        {
            Guid_Space = guid_Space;
            Guid_SystemSpace = guid_SystemSpace;
            Guid_Damper_Return = guid_Damper_Return;
            Guid_Damper_Supply = guid_Damper_Supply;
            Guid_Connection_RoomToReturnDamper = guid_Connection_RoomToReturnDamper;
            Guid_Connection_ReturnDamperToDXCoil = guid_Connection_ReturnDamperToDXCoil;
            Guid_Connection_FanToSupplyDamper = guid_Connection_FanToSupplyDamper;
            Guid_Connection_SupplyDamperToRoom = guid_Connection_SupplyDamperToRoom;
            DesignFlowRate_Lps = designFlowRate_Lps;
        }

        /// <summary>The analytical room.</summary>
        public Guid Guid_Space { get; }

        /// <summary>The room's one existing <c>SystemSpace</c> - the same one its ventilation legs use.</summary>
        public Guid Guid_SystemSpace { get; }

        public Guid Guid_Damper_Return { get; }

        public Guid Guid_Damper_Supply { get; }

        public Guid Guid_Connection_RoomToReturnDamper { get; }

        public Guid Guid_Connection_ReturnDamperToDXCoil { get; }

        public Guid Guid_Connection_FanToSupplyDamper { get; }

        public Guid Guid_Connection_SupplyDamperToRoom { get; }

        /// <summary>
        /// The room's recirculation duty at the full law [l/s] - its floor-area share of the ceiling, the
        /// same figure on its supply and its return damper. A recirculation airflow, never the room's
        /// ventilation design airflow.
        /// </summary>
        public double DesignFlowRate_Lps { get; }
    }
}
