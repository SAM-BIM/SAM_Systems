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
    public class SAMSystemsCreateEnergyCentreByMollier : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("b7e3c2a1-4d6f-4a8b-9c12-7e5f0a3b6d24");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public SAMSystemsCreateEnergyCentreByMollier()
          : base(
                "SAMSystems.CreateEnergyCentreByMollier",
                "SAMSystems.CreateEnergyCentreByMollier",
                "Converts a sequence of psychrometric (Mollier) processes into a connected, simulation-ready\n" +
                "SystemEnergyCentre - turning an air-handling concept sketched on the Mollier chart into a\n" +
                "model that can be simulated.\n" +
                "\n" +
                "HOW IT WORKS\n" +
                "Each Mollier process in the supplied chain is classified by type and mapped to the matching\n" +
                "air-handling component:\n" +
                "  - CoolingProcess        -> SystemCoolingCoil (off-coil Setpoint + BypassFactor + Duty)\n" +
                "  - HeatingProcess        -> SystemHeatingCoil (off-coil Setpoint + Duty)\n" +
                "  - FanProcess            -> SystemFan\n" +
                "  - HeatRecoveryProcess   -> SystemExchanger (latent-capable when humidity ratio changes)\n" +
                "  - HumidificationProcess -> SystemHumidifier\n" +
                "  - MixingProcess         -> SystemAirJunction\n" +
                "Undefined/Specific processes are skipped.\n" +
                "\n" +
                "Component duties, off-coil setpoints and the cooling bypass factor are derived from the\n" +
                "process end-states together with the design airflow (duty = mass flow x enthalpy change;\n" +
                "bypass factor from the cooling Apparatus Dew Point). The components are then wired in\n" +
                "process order along an air system inside a plantroom.\n" +
                "\n" +
                "TWIN-WHEEL (SUPPLY + EXTRACT)\n" +
                "Provide an extract chain to model heat recovery with both a supply and an extract air path.\n" +
                "A single SystemExchanger is shared across both chains (supply on air path 1, extract on air\n" +
                "path 2), so latent + sensible recovery is represented as one device.\n" +
                "\n" +
                "DOWNSTREAM\n" +
                "The result round-trips to JSON and can be simulated by connecting it to\n" +
                "SAMSystems.CreateTPDByTSDAndSystemEnergyCentre (Tas annual simulation).",
                "SAM",
                "Systems")
        {
        }

        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooMollierProcessParam() { Name = "_supplyMollierProcesses", NickName = "_supplyMollierProcesses", Description = "Ordered supply-side chain of SAM Mollier processes (e.g. mixing -> heat recovery -> cooling -> heating -> fan).\n\nThe list order defines the air flow direction and therefore the component connectivity: the air leaves one component and enters the next in the order given. Each process should start where the previous one ended (process.End -> next process.Start).", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_supplyAirflow_", NickName = "_supplyAirflow_", Description = "Design supply volumetric airflow in cubic metres per second [m3/s].\n\nMollier processes describe intensive air states only (no flow), so this is required to size component duties: mass flow = airflow x moist-air density at the process inlet, and duty = mass flow x enthalpy change. Off-coil setpoints and bypass factors do not depend on it.\n\nLeave empty to create the components and connectivity without computed duties.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooMollierProcessParam() { Name = "_extractMollierProcesses_", NickName = "_extractMollierProcesses_", Description = "Optional ordered extract-side chain of SAM Mollier processes (room air -> heat recovery -> extract fan).\n\nWhen supplied, a heat-recovery exchanger that appears in both the supply and extract chains is created once and shared across both air paths (supply on air path 1, extract on air path 2) - i.e. a twin-wheel / run-around unit modelled as a single device. Multiple recovery devices are paired between the chains in order.\n\nLeave empty for a supply-only air handling unit.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_extractAirflow_", NickName = "_extractAirflow_", Description = "Design extract volumetric airflow in cubic metres per second [m3/s], used to size the duties of the extract-side components. Only relevant when an extract chain is supplied.\n\nLeave empty to omit extract-side duties.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_name_", NickName = "_name_", Description = "Name for the created SystemEnergyCentre.\n\nDefaults to \"Energy Centre\" when left empty.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                return result.ToArray();
            }
        }

        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "systemEnergyCentre", NickName = "systemEnergyCentre", Description = "The generated SAM SystemEnergyCentre: a plantroom whose air-handling components are connected in the order of the supplied Mollier processes.\n\nIt round-trips to JSON and is ready for simulation - connect it to SAMSystems.CreateTPDByTSDAndSystemEnergyCentre to run an annual Tas simulation.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index;

            List<IMollierProcess> supplyMollierProcesses = new List<IMollierProcess>();
            index = Params.IndexOfInputParam("_supplyMollierProcesses");
            if (index == -1 || !dataAccess.GetDataList(index, supplyMollierProcesses) || supplyMollierProcesses == null || supplyMollierProcesses.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            double supplyAirflow = double.NaN;
            index = Params.IndexOfInputParam("_supplyAirflow_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref supplyAirflow);
            }

            List<IMollierProcess> extractMollierProcesses = new List<IMollierProcess>();
            index = Params.IndexOfInputParam("_extractMollierProcesses_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, extractMollierProcesses);
            }

            double extractAirflow = double.NaN;
            index = Params.IndexOfInputParam("_extractAirflow_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref extractAirflow);
            }

            string name = null;
            index = Params.IndexOfInputParam("_name_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref name);
            }

            string energyCentreName = string.IsNullOrWhiteSpace(name) ? "Energy Centre" : name;

            SystemEnergyCentre systemEnergyCentre;
            if (extractMollierProcesses != null && extractMollierProcesses.Count != 0)
            {
                systemEnergyCentre = SAM.Analytical.Systems.Mollier.Create.SystemEnergyCentre(supplyMollierProcesses, extractMollierProcesses, supplyAirflow, extractAirflow, energyCentreName);
            }
            else
            {
                systemEnergyCentre = SAM.Analytical.Systems.Mollier.Create.SystemEnergyCentre(supplyMollierProcesses, supplyAirflow, energyCentreName);
            }

            if (systemEnergyCentre == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No SystemEnergyCentre could be created from the supplied Mollier processes.");
            }

            index = Params.IndexOfOutputParam("systemEnergyCentre");
            if (index != -1)
            {
                dataAccess.SetData(index, systemEnergyCentre);
            }
        }
    }
}
