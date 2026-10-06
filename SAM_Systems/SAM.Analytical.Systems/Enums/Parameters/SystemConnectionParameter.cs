// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using SAM.Core.Attributes;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems
{
    [AssociatedTypes(typeof(ISystemConnection)), Description("System Connection Parameter")]
    public enum SystemConnectionParameter
    {
        [ParameterProperties("Fluid Type Name", "Fluid Type Name"), ParameterValue(Core.ParameterType.String)] FluidTypeName,

        /// <summary>
        /// The DESIGN flow rate the connection carries, in litres per second - the airflow the design
        /// states for that leg, and nothing else.
        /// <para>
        /// <b>Design airflow only, never overloaded.</b> A required airflow (what a regulation asks of a
        /// room), a selected product's capacity (what the chosen machine can move) and an operating
        /// airflow (what it actually moved in one hour of a simulation) are three further, routinely
        /// different numbers about the same leg. Writing any of them here would make the graph state a
        /// duty the design does not, with nothing to say which of the four it was.
        /// </para>
        /// </summary>
        [ParameterProperties("Design Flow Rate", "Design Flow Rate [l/s]"), ParameterValue(Core.ParameterType.Double)] DesignFlowRate,
    }
}
