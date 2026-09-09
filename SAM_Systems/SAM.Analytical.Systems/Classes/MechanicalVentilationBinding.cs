// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// One lineage row: an analytical source object, and the materialised Systems object it contributed
    /// to.
    /// <para>
    /// <b>Not one row per materialised object.</b> A space may carry several design terminals of one
    /// direction, and the design duty of that direction is their sum - a bedroom subdivided into three
    /// 7 + 8 + 15 l/s supply terminals has one 30 l/s supply leg. All three terminals are real analytical
    /// objects with their own identities and all three contributed, so all three get a row against that
    /// one connection. Collapsing them to a single row, or picking one as "the" source, would lose the
    /// lineage a caller needs to trace a duty back to the terminals that produced it.
    /// </para>
    /// <para>
    /// <b>Guids, never names.</b> Nothing in a binding is a display name, and nothing keys on one - two
    /// rooms called "Bedroom" are two rows with two different <see cref="Guid_Analytical"/>s.
    /// </para>
    /// </summary>
    public class MechanicalVentilationBinding
    {
        /// <summary>What kind of lineage this row states.</summary>
        public MechanicalVentilationBindingType BindingType { get; }

        /// <summary>
        /// The analytical source: an <c>AirHandlingUnit</c>, a <c>Space</c>, a <c>VentilationTerminal</c>
        /// or a <c>SpaceAirMovement</c>, according to <see cref="BindingType"/>.
        /// </summary>
        public Guid Guid_Analytical { get; }

        /// <summary>
        /// The transfer destination space for a <see cref="MechanicalVentilationBindingType.TransferConnection"/>
        /// row, so a movement's two endpoints are both stated. <see cref="Guid.Empty"/> on every other
        /// binding type.
        /// </summary>
        public Guid Guid_Analytical_Secondary { get; }

        /// <summary>
        /// The materialised object: an <c>AirSystem</c>, a <c>SystemSpace</c> or a
        /// <c>SystemConnection</c>. <b>Not unique across the rows</b> - the N:1 terminal rows share one
        /// connection guid, which is what makes the aggregation traceable.
        /// </summary>
        public Guid Guid_Systems { get; }

        public MechanicalVentilationBinding(MechanicalVentilationBindingType bindingType, Guid guid_Analytical, Guid guid_Systems)
            : this(bindingType, guid_Analytical, Guid.Empty, guid_Systems)
        {

        }

        public MechanicalVentilationBinding(MechanicalVentilationBindingType bindingType, Guid guid_Analytical, Guid guid_Analytical_Secondary, Guid guid_Systems)
        {
            BindingType = bindingType;
            Guid_Analytical = guid_Analytical;
            Guid_Analytical_Secondary = guid_Analytical_Secondary;
            Guid_Systems = guid_Systems;
        }

        public MechanicalVentilationBinding(MechanicalVentilationBinding mechanicalVentilationBinding)
        {
            if (mechanicalVentilationBinding != null)
            {
                BindingType = mechanicalVentilationBinding.BindingType;
                Guid_Analytical = mechanicalVentilationBinding.Guid_Analytical;
                Guid_Analytical_Secondary = mechanicalVentilationBinding.Guid_Analytical_Secondary;
                Guid_Systems = mechanicalVentilationBinding.Guid_Systems;
            }
        }

        /// <summary>
        /// The sort key, and the one combination that is unique across a materialisation's bindings.
        /// </summary>
        internal int CompareTo(MechanicalVentilationBinding mechanicalVentilationBinding)
        {
            if (mechanicalVentilationBinding == null)
            {
                return 1;
            }

            int result = BindingType.CompareTo(mechanicalVentilationBinding.BindingType);
            if (result != 0)
            {
                return result;
            }

            result = Guid_Analytical.CompareTo(mechanicalVentilationBinding.Guid_Analytical);
            if (result != 0)
            {
                return result;
            }

            return Guid_Analytical_Secondary.CompareTo(mechanicalVentilationBinding.Guid_Analytical_Secondary);
        }

        public override string ToString()
        {
            return string.Format("{0}: {1} -> {2}", BindingType, Guid_Analytical, Guid_Systems);
        }
    }
}
