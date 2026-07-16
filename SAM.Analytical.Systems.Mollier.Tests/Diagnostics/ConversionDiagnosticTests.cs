// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Diagnostics
{
    public class ConversionDiagnosticTests
    {
        private static readonly double Pressure = 101325.0;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void UnsupportedProcess_Emits_Diagnostic()
        {
            UndefinedProcess process = new UndefinedProcess(new JsonObject());
            Mollier.Create.SystemComponent(process, 2.0, out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.UnsupportedProcess);
        }

        [Fact]
        public void NaN_Airflow_EmitsWarning_Diagnostic()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            Mollier.Create.SystemComponent(process, double.NaN, out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.AirflowNaN);
        }

        [Fact]
        public void EmptyProcessChain_Emits_ChainEmptyDiagnostic()
        {
            List<IMollierProcess> processes = new List<IMollierProcess>();
            Mollier.Create.SystemPlantRoom(processes, 2.0, "Plant Room", "Air System", out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.ChainEmpty);
        }

        [Fact]
        public void NullMollierGroup_Emits_Diagnostic()
        {
            Mollier.Create.SystemPlantRoom((MollierGroup)null, 2.0, null, out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.NullProcessChain && d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void NullProcessChain_Emits_Error()
        {
            Mollier.Create.SystemPlantRoom((IEnumerable<IMollierProcess>)null, 2.0, "PR", "Air System", out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.NullProcessChain && d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void NullProcess_Emits_NullProcessDiagnostic()
        {
            ISystemComponent result = ((IMollierProcess)null).SystemComponent(2.0, out List<ConversionDiagnostic> diagnostics);

            Assert.Null(result);
            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.NullProcess && d.Severity == DiagnosticSeverity.Warning);
        }

        [Fact]
        public void EmptyChain_Emits_Error_Severity()
        {
            List<IMollierProcess> processes = new List<IMollierProcess>();
            Mollier.Create.SystemPlantRoom(processes, 2.0, "Plant Room", "Air System", out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.ChainEmpty && d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void InvalidProcessState_Emits_Diagnostic()
        {
            // Non-null but invalid Start (NaN dry-bulb): the public MollierPoint constructor and the
            // HeatingProcess factory only null-check the start point, so this is reachable through public API
            // without touching internal/JSON construction.
            MollierPoint invalidStart = new MollierPoint(double.NaN, 0.008, Pressure);
            Assert.False(invalidStart.IsValid());

            HeatingProcess process = invalidStart.HeatingProcess(35.0);
            Assert.NotNull(process);

            ISystemComponent result = process.SystemComponent(2.0, out List<ConversionDiagnostic> diagnostics);

            Assert.NotNull(result);
            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.InvalidProcessState && d.Severity == DiagnosticSeverity.Warning);
        }

        [Fact]
        public void TemplateOverload_Surfaces_Diagnostics()
        {
            // Same process type used by UnsupportedProcess_Emits_Diagnostic to trigger MOLLIER-001.
            UndefinedProcess process = new UndefinedProcess(new JsonObject());
            List<IMollierProcess> processes = new List<IMollierProcess> { process };

            Mollier.Create.SystemEnergyCentre(processes, 2.0, "EC", (SystemEnergyCentre)null, out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.UnsupportedProcess);
        }

        [Fact]
        public void HeatRecoveryCountMismatch_Emits_013()
        {
            MollierPoint outdoor = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(32, 40, Pressure);
            MollierPoint room = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(24, 50, Pressure);

            // Supply chain has two heat-recovery stages; the second is built from the first's End point so the
            // chain stays physically continuous.
            HeatRecoveryProcess supply1 = outdoor.HeatRecoveryProcess_Supply(room, 75, 65);
            HeatRecoveryProcess supply2 = supply1.End.HeatRecoveryProcess_Supply(room, 50, 0);
            List<IMollierProcess> supply = new List<IMollierProcess> { supply1, supply2 };

            // Extract chain has only one.
            HeatRecoveryProcess extract1 = room.HeatRecoveryProcess_Extract(outdoor, 75, 65);
            List<IMollierProcess> extract = new List<IMollierProcess> { extract1 };

            Mollier.Create.SystemPlantRoom(supply, extract, 2.0, 2.0, "PR", out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.HeatRecoveryCountMismatch && d.Severity == DiagnosticSeverity.Warning);
        }

        [Fact]
        public void Diagnostics_CodeMeanings_AreUnique()
        {
            FieldInfo[] fields = typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static);

            List<string> codes = new List<string>();
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(string) || !field.IsLiteral)
                {
                    continue;
                }

                string code = (string)field.GetRawConstantValue();
                Assert.Matches(@"^MOLLIER-\d{3}$", code);
                codes.Add(code);
            }

            Assert.Equal(14, codes.Count);
            Assert.Equal(codes.Count, codes.Distinct().Count());
        }

        [Fact]
        public void ZeroEfficiencyCooling_Emits_ApparatusDewPointAndBypassFactor_Diagnostics()
        {
            // Sensible-only branch of Create.CoolingProcess: the target dry-bulb (20C) sits above the ~11C dew
            // point of 0.008 kg/kg air, so End is computed independently of Efficiency - but the stored zero
            // Efficiency then drives CoolingProcess.ApparatusDewPoint()'s division to +/-Infinity/NaN, producing
            // a non-null but invalid ADP. Reachable entirely through public factories (verified: no exceptions,
            // ApparatusDewPoint() = (-Infinity, NaN) which MollierPoint.IsValid() rejects, BypassFactor() = NaN).
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(20.0, 0.0);
            Assert.NotNull(process);
            Assert.False(process.ApparatusDewPoint().IsValid());
            Assert.True(double.IsNaN(process.BypassFactor()));

            ISystemComponent result = process.SystemComponent(2.0, out List<ConversionDiagnostic> diagnostics);

            Assert.NotNull(result);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.ApparatusDewPointNotAvailable && d.Severity == DiagnosticSeverity.Info);
            Assert.Contains(diagnostics, d => d.Code == DiagnosticCodes.BypassFactorInvalid && d.Severity == DiagnosticSeverity.Info);
        }

        [Fact]
        public void Diagnostic_HasSeverity()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            Mollier.Create.SystemComponent(process, double.NaN, out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostics[0].Severity);
        }

        [Fact]
        public void Diagnostic_HasCode()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            Mollier.Create.SystemComponent(process, double.NaN, out List<ConversionDiagnostic> diagnostics);

            Assert.NotEmpty(diagnostics);
            Assert.False(string.IsNullOrWhiteSpace(diagnostics[0].Code));
        }

        [Fact]
        public void DiagnosticCodes_HaveExpectedFormat()
        {
            Assert.StartsWith("MOLLIER-", DiagnosticCodes.UnsupportedProcess);
            Assert.StartsWith("MOLLIER-", DiagnosticCodes.AirflowNaN);
            Assert.StartsWith("MOLLIER-", DiagnosticCodes.ChainEmpty);
            Assert.StartsWith("MOLLIER-", DiagnosticCodes.NoProcessesInChain);
            Assert.StartsWith("MOLLIER-", DiagnosticCodes.LiquidInjectionFailed);
        }
    }
}
