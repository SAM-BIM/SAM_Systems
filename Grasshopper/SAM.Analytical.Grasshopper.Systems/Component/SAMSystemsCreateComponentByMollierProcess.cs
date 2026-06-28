// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Grasshopper.Mollier;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMSystemsCreateComponentByMollierProcess : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("d9a5e4c3-6f8b-4cad-9e32-7a4b2c5d8f01");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public SAMSystemsCreateComponentByMollierProcess()
          : base(
                "SAMSystems.CreateComponentByMollierProcess",
                "SAMSystems.CreateComponentByMollierProcess",
                "Maps a single psychrometric (Mollier) process to the air-handling SystemComponent that realises it,\n" +
                "and reports the duty and bypass factor derived from the process and the design airflow.\n" +
                "\n" +
                "This is the per-process building block used by SAMSystems.CreateEnergyCentreByMollier; use it to\n" +
                "inspect or post-process individual components before assembling a system.\n" +
                "\n" +
                "MAPPING\n" +
                "  - CoolingProcess        -> SystemCoolingCoil   (off-coil Setpoint, BypassFactor, Duty)\n" +
                "  - HeatingProcess        -> SystemHeatingCoil   (off-coil Setpoint, Duty)\n" +
                "  - FanProcess            -> SystemFan\n" +
                "  - HeatRecoveryProcess   -> SystemExchanger     (latent-capable when humidity ratio changes)\n" +
                "  - HumidificationProcess -> SystemHumidifier\n" +
                "  - MixingProcess         -> SystemAirJunction\n" +
                "Undefined/Specific processes produce no component (null).",
                "SAM",
                "Systems")
        {
        }

        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooMollierProcessParam() { Name = "_mollierProcess", NickName = "_mollierProcess", Description = "A single SAM Mollier process. Its type selects the air-handling component, and its Start/End states set the off-coil temperature and (for cooling) the bypass factor.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_designAirflow_", NickName = "_designAirflow_", Description = "Design volumetric airflow in cubic metres per second [m3/s].\n\nUsed to convert the intensive process to an extensive duty: mass flow = airflow x inlet moist-air density, duty = mass flow x enthalpy change.\n\nLeave empty to create the component without a computed duty (the 'duty' output is then NaN).", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemComponentParam() { Name = "systemComponent", NickName = "systemComponent", Description = "The SAM air-handling SystemComponent realising the process (e.g. SystemCoolingCoil). Null for Undefined/Specific processes.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "duty", NickName = "duty", Description = "Component duty in watts [W] = |mass flow x (outlet enthalpy - inlet enthalpy)|. NaN when no design airflow is supplied or the process states are invalid.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "bypassFactor", NickName = "bypassFactor", Description = "Coil bypass factor [0..1], reported for cooling processes only: (off-coil - ADP) / (on-coil - ADP) on dry-bulb temperature, where ADP is the cooling Apparatus Dew Point. NaN for non-cooling processes.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index;

            IMollierProcess mollierProcess = null;
            index = Params.IndexOfInputParam("_mollierProcess");
            if (index == -1 || !dataAccess.GetData(index, ref mollierProcess) || mollierProcess == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            double designAirflow = double.NaN;
            index = Params.IndexOfInputParam("_designAirflow_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref designAirflow);
            }

            ISystemComponent systemComponent = SAM.Analytical.Systems.Mollier.Create.SystemComponent(mollierProcess, designAirflow);

            double duty = SAM.Analytical.Systems.Mollier.Query.Duty(mollierProcess, designAirflow);

            double bypassFactor = double.NaN;
            if (mollierProcess is CoolingProcess coolingProcess)
            {
                bypassFactor = SAM.Analytical.Systems.Mollier.Query.BypassFactor(coolingProcess);
            }

            index = Params.IndexOfOutputParam("systemComponent");
            if (index != -1)
            {
                dataAccess.SetData(index, systemComponent);
            }

            index = Params.IndexOfOutputParam("duty");
            if (index != -1)
            {
                dataAccess.SetData(index, duty);
            }

            index = Params.IndexOfOutputParam("bypassFactor");
            if (index != -1)
            {
                dataAccess.SetData(index, bypassFactor);
            }
        }
    }
}
