// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// SAM#123: what the manufacturer-guidance materialisation built for one air handling unit - the unit's
    /// own exchanger, the supply DX coil inserted after it, its fans, the room hosting the cooling-stat and
    /// every room's design and elevated airflows - so the TAS grounding can find each object by identity
    /// rather than by name.
    /// <para>
    /// <b>A record of the graph, not a second source of engineering data.</b> The strategy it carries is the
    /// catalogue's, resolved for this dwelling; the flows are the design duties and their elevated
    /// counterparts in design proportions (<see cref="MechanicalVentilationOperatingFlows"/>). Requirement
    /// airflow, design airflow, equipment capacity and operating airflow stay distinct: nothing here is a
    /// capacity.
    /// </para>
    /// </summary>
    public class MechanicalVentilationGuidanceCooling
    {
        private readonly List<MechanicalVentilationGuidanceRoom> rooms;
        private readonly MechanicalVentilationGuidanceSettings settings;

        public MechanicalVentilationGuidanceCooling(
            Guid guid_AirHandlingUnit,
            Guid guid_AirSystem,
            Guid guid_Exchanger,
            Guid guid_DXCoil,
            Guid guid_Fan_Supply,
            Guid guid_Fan_Extract,
            Guid guid_Space_Stat,
            IEnumerable<MechanicalVentilationGuidanceRoom> rooms,
            MechanicalVentilationGuidanceSettings mechanicalVentilationGuidanceSettings)
        {
            Guid_AirHandlingUnit = guid_AirHandlingUnit;
            Guid_AirSystem = guid_AirSystem;
            Guid_Exchanger = guid_Exchanger;
            Guid_DXCoil = guid_DXCoil;
            Guid_Fan_Supply = guid_Fan_Supply;
            Guid_Fan_Extract = guid_Fan_Extract;
            Guid_Space_Stat = guid_Space_Stat;
            this.rooms = rooms == null ? new List<MechanicalVentilationGuidanceRoom>() : new List<MechanicalVentilationGuidanceRoom>(rooms);
            settings = mechanicalVentilationGuidanceSettings == null ? null : new MechanicalVentilationGuidanceSettings(mechanicalVentilationGuidanceSettings);
        }

        public Guid Guid_AirHandlingUnit { get; }

        public Guid Guid_AirSystem { get; }

        public Guid Guid_Exchanger { get; }

        /// <summary>The supply DX coil, between the exchanger's supply outlet and the supply fan.</summary>
        public Guid Guid_DXCoil { get; }

        public Guid Guid_Fan_Supply { get; }

        public Guid Guid_Fan_Extract { get; }

        /// <summary>
        /// The analytical space whose air the cooling-stat senses - the unit's habitable room with the largest
        /// design supply. PROVISIONAL: the manufacturer states a room stat without stating which room.
        /// </summary>
        public Guid Guid_Space_Stat { get; }

        public List<MechanicalVentilationGuidanceRoom> Rooms
        {
            get
            {
                return new List<MechanicalVentilationGuidanceRoom>(rooms);
            }
        }

        public MechanicalVentilationGuidanceSettings Settings
        {
            get
            {
                return settings == null ? null : new MechanicalVentilationGuidanceSettings(settings);
            }
        }

        /// <summary>The unit's design (background) supply total [l/s].</summary>
        public double DesignSupply_Lps
        {
            get
            {
                double result = 0;
                rooms.ForEach(x => result += x.DesignSupply_Lps);
                return result;
            }
        }

        /// <summary>The unit's design (background) extract total [l/s].</summary>
        public double DesignExtract_Lps
        {
            get
            {
                double result = 0;
                rooms.ForEach(x => result += x.DesignExtract_Lps);
                return result;
            }
        }

        /// <summary>The elevated operating airflow [l/s] the unit moves on each side while cooling.</summary>
        public double ElevatedAirFlow_Lps
        {
            get
            {
                return settings?.OperatingStrategy?.ElevatedAirFlow_Lps ?? double.NaN;
            }
        }
    }

    /// <summary>One room of a manufacturer-guidance unit: its design duties and elevated operating airflows.</summary>
    public class MechanicalVentilationGuidanceRoom
    {
        public MechanicalVentilationGuidanceRoom(Guid guid_Space, Guid guid_SystemSpace, double designSupply_Lps, double designExtract_Lps, double elevatedSupply_Lps, double elevatedExtract_Lps)
        {
            Guid_Space = guid_Space;
            Guid_SystemSpace = guid_SystemSpace;
            DesignSupply_Lps = designSupply_Lps;
            DesignExtract_Lps = designExtract_Lps;
            ElevatedSupply_Lps = elevatedSupply_Lps;
            ElevatedExtract_Lps = elevatedExtract_Lps;
        }

        public Guid Guid_Space { get; }

        public Guid Guid_SystemSpace { get; }

        /// <summary>Design (background) supply duty [l/s]; 0 for an extract-only room.</summary>
        public double DesignSupply_Lps { get; }

        /// <summary>Design (background) extract duty [l/s]; 0 for a supply-only room.</summary>
        public double DesignExtract_Lps { get; }

        /// <summary>Supply [l/s] while cooling: the room's design share of the elevated total.</summary>
        public double ElevatedSupply_Lps { get; }

        /// <summary>Extract [l/s] while cooling: the room's design share of the elevated total.</summary>
        public double ElevatedExtract_Lps { get; }
    }
}
