// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Attributes;
using SAM.Core;
using System.ComponentModel;
using SAM.Geometry.Systems;

namespace SAM.Analytical.Systems
{
    [AssociatedTypes(typeof(Setting)), Description("Analytical System Setting Parameter")]
    public enum AnalyticalSystemSettingParameter
    {
        [ParameterProperties("Default DisplaySystemManager File Name", "Default DisplaySystemManager File Name"), ParameterValue(ParameterType.String)] DefaultDisplaySystemManagerFileName,

        [ParameterProperties("Default DisplaySystemManager", "Default DisplaySystemManager"), SAMObjectParameterValue(typeof(DisplaySystemManager))] DefaultDisplaySystemManager,

        [ParameterProperties("Default SystemEnergyCentre File Directory", "Default SystemEnergyCentre File Directory"), ParameterValue(ParameterType.String)] DefaultSystemEnergyCentreFileDirectory,

        [ParameterProperties("Default SystemEnergyCentre Directory Name", "Default SystemEnergyCentre Directory Name"), ParameterValue(ParameterType.String)] DefaultSystemEnergyCentreDirectoryName,

        [ParameterProperties("Default VentilationUnit File Directory", "Default VentilationUnit File Directory"), ParameterValue(ParameterType.String)] DefaultVentilationUnitFileDirectory,

        [ParameterProperties("Default VentilationUnit Directory Name", "Default VentilationUnit Directory Name"), ParameterValue(ParameterType.String)] DefaultVentilationUnitDirectoryName,
    }
}
