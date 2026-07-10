// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;

namespace SAM.Analytical.Systems.Mollier
{
    public class ConversionResult
    {
        public List<ConversionDiagnostic> Diagnostics { get; } = new List<ConversionDiagnostic>();

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Severity == DiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool HasWarnings
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Severity == DiagnosticSeverity.Warning)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
