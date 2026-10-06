// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// What kind of analytical-to-Systems lineage a <see cref="MechanicalVentilationBinding"/> states.
    /// <para>
    /// <b>There is deliberately no plant-room member.</b> The materialised <c>SystemPlantRoom</c> has no
    /// analytical source object - it is the container the template supplied - so a row for it would
    /// assert a lineage that does not exist. The same is true of the fans, dampers and junctions copied
    /// from the template: they are real objects in the result, and none of them came from anything in the
    /// analytical model.
    /// </para>
    /// </summary>
    [Description("Mechanical Ventilation Binding Type")]
    public enum MechanicalVentilationBindingType
    {
        [Description("Undefined")] Undefined,

        /// <summary>An <c>AirHandlingUnit</c> and the one <c>AirSystem</c> materialised for it.</summary>
        [Description("Air System")] AirSystem,

        /// <summary>A <c>Space</c> and the one <c>SystemSpace</c> materialised for it.</summary>
        [Description("System Space")] SystemSpace,

        /// <summary>
        /// One contributing supply <c>VentilationTerminal</c> and the space's single supply
        /// <c>SystemConnection</c>. Several terminals may name the same connection - see
        /// <see cref="MechanicalVentilationBinding"/>.
        /// </summary>
        [Description("Supply Connection")] SupplyConnection,

        /// <summary>
        /// One contributing extract <c>VentilationTerminal</c> and the space's single extract
        /// <c>SystemConnection</c>. Several terminals may name the same connection.
        /// </summary>
        [Description("Extract Connection")] ExtractConnection,

        /// <summary>A <c>SpaceAirMovement</c> and the transfer <c>SystemConnection</c> materialised for it.</summary>
        [Description("Transfer Connection")] TransferConnection,
    }
}
