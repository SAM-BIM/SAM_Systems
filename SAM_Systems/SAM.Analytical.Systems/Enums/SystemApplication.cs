// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// What kind of building a shipped <c>SystemEnergyCentre</c> template is for.
    /// <para>
    /// <b>An eligibility constraint, not a capability and not a preference.</b> A commercial
    /// air-handling type is not a worse answer for a dwelling - it is not an answer at all, and it must
    /// be excluded before <c>SAM.Analytical</c> is ever offered it. An earlier revision recorded this as
    /// documentation and let <c>Rank</c> keep commercial systems out of a dwelling answer, which meant a
    /// single edited number could have put a variable-air-volume unit in a flat, and any caller reading
    /// <c>CapableSystems</c> for its own policy was told a commercial AHU was suitable.
    /// </para>
    /// <para>
    /// <b>It lives here, not in <c>SAM.Analytical</c>.</b> Which of <i>these</i> templates is a dwelling
    /// system is a fact about this repository's resources, on exactly the same footing as their
    /// capabilities. The analytical layer never learns the words "domestic" or "commercial"; it is handed
    /// the systems that are eligible and decides suitability among them.
    /// </para>
    /// </summary>
    public enum SystemApplication
    {
        /// <summary>
        /// No constraint - every template is offered. The right value for a caller that has not said what
        /// it is assessing, and the wrong one for an Approved Document F dwelling.
        /// </summary>
        Undefined,

        /// <summary>A dwelling system. Approved Document F selection asks for these.</summary>
        Domestic,

        /// <summary>
        /// A commercial air-handling type. Never eligible for a dwelling.
        /// </summary>
        Commercial,

        /// <summary>
        /// Suits either, so it is eligible whatever was asked for.
        /// </summary>
        Any
    }
}
