// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
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
