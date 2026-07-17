// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ConversionDiagnostic
    {
        public DiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public IMollierProcess SourceProcess { get; }

        public ConversionDiagnostic(DiagnosticSeverity severity, string code, string message, IMollierProcess sourceProcess = null)
        {
            Severity = severity;
            Code = code;
            Message = message;
            SourceProcess = sourceProcess;
        }
    }

    /// <summary>
    /// Canonical, stable diagnostic codes emitted by the Mollier to SAM_Systems bridge. Each code has exactly one
    /// meaning and one severity across every emission site in the bridge (see the per-constant remarks below).
    /// </summary>
    public static class DiagnosticCodes
    {
        /// <summary>
        /// Severity: Warning. A Mollier process type maps to no system component (e.g. <see cref="UndefinedProcess"/>,
        /// <see cref="SpecificProcess"/>); the process is skipped and no component is created for it.
        /// </summary>
        public const string UnsupportedProcess = "MOLLIER-001";

        /// <summary>
        /// Severity: Warning. A null <see cref="IMollierProcess"/> was encountered where a process was expected;
        /// it is skipped and no component is created for it.
        /// </summary>
        public const string NullProcess = "MOLLIER-002";

        /// <summary>
        /// Severity: Warning. The process' Start or End <see cref="MollierPoint"/> is null or invalid, so the
        /// setpoints and duties derived from it are not set.
        /// </summary>
        public const string InvalidProcessState = "MOLLIER-003";

        /// <summary>
        /// Severity: Warning. The design airflow supplied for the conversion is <see cref="double.NaN"/>, so
        /// duties and flows that depend on it are not set.
        /// </summary>
        public const string AirflowNaN = "MOLLIER-004";

        /// <summary>
        /// Severity: Error. The process chain (or <see cref="MollierGroup"/>) argument passed to the conversion
        /// is null.
        /// </summary>
        public const string NullProcessChain = "MOLLIER-005";

        /// <summary>
        /// Severity: Info. The Apparatus Dew Point could not be computed for a cooling process, so
        /// <c>MinimumOffcoil</c> is not set on the resulting cooling coil.
        /// </summary>
        public const string ApparatusDewPointNotAvailable = "MOLLIER-006";

        /// <summary>
        /// Severity: Info. The cooling coil bypass factor is <see cref="double.NaN"/> or otherwise invalid, so
        /// it is not set on the resulting cooling coil.
        /// </summary>
        public const string BypassFactorInvalid = "MOLLIER-007";

        /// <summary>
        /// Severity: Warning. A paired supply/extract heat-recovery process pair yielded no usable sensible or
        /// latent effectiveness, so the shared exchanger's efficiency fields are left unset.
        /// </summary>
        public const string HeatRecoveryEfficiencyNotAvailable = "MOLLIER-008";

        /// <summary>
        /// Severity: Warning. A connector operation between two components failed, or a directional connector
        /// could not be resolved and the wiring fell back from directional to automatic connector selection.
        /// </summary>
        public const string ConnectionFailed = "MOLLIER-009";

        /// <summary>
        /// Severity: Info. No display symbol (or no symbol library at all) was available for a component, so
        /// display promotion skipped it.
        /// </summary>
        public const string DisplaySymbolMissing = "MOLLIER-010";

        /// <summary>
        /// Severity: Error. The supplied <see cref="MollierGroup"/> contains zero processes.
        /// </summary>
        public const string NoProcessesInChain = "MOLLIER-011";

        /// <summary>
        /// Severity: Error. The process chain produced zero system components.
        /// </summary>
        public const string ChainEmpty = "MOLLIER-012";

        /// <summary>
        /// Severity: Warning. The supply and extract chains contain different counts of
        /// <see cref="HeatRecoveryProcess"/> processes; surplus processes are given their own exchanger instead
        /// of sharing one across both air paths.
        /// </summary>
        public const string HeatRecoveryCountMismatch = "MOLLIER-013";

        /// <summary>
        /// Severity: Error. Liquid system injection failed, or the liquid loop was left partially wired.
        /// </summary>
        public const string LiquidInjectionFailed = "MOLLIER-014";
    }
}
