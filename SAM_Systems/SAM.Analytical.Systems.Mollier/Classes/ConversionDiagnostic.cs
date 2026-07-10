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

    public static class DiagnosticCodes
    {
        public const string UnsupportedProcess = "MOLLIER-001";
        public const string NullProcess = "MOLLIER-002";
        public const string InvalidEndPoint = "MOLLIER-003";
        public const string AirflowNaN = "MOLLIER-004";
        public const string NullProcessChain = "MOLLIER-005";
        public const string ApparatusDewPointNotAvailable = "MOLLIER-006";
        public const string BypassFactorInvalid = "MOLLIER-007";
        public const string HeatRecoveryEfficiencyNotAvailable = "MOLLIER-008";
        public const string MoistureTransferDetected = "MOLLIER-009";
        public const string HeatRecoveryMissingExtract = "MOLLIER-010";
        public const string NoProcessesInChain = "MOLLIER-011";
        public const string ChainEmpty = "MOLLIER-012";
        public const string ConversionWithWarnings = "MOLLIER-013";
        public const string LiquidInjectionFailed = "MOLLIER-014";
    }
}
