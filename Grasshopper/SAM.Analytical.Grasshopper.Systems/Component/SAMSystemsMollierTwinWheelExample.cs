// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMSystemsMollierTwinWheelExample : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("e1b6f5d4-7a9c-4dbe-af43-8b5c3d6e9f12");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public SAMSystemsMollierTwinWheelExample()
          : base(
                "SAMSystems.MollierTwinWheelExample",
                "SAMSystems.MollierTwinWheelExample",
                "Builds the worked twin-wheel (latent + sensible recovery) air-handling example for the\n" +
                "Mollier -> SAM_Systems bridge, and runs a built-in self-check.\n" +
                "\n" +
                "It constructs a supply chain (heat recovery -> cooling -> reheat -> supply fan) and an extract\n" +
                "chain (heat recovery -> extract fan) as Mollier processes, then converts them to a connected\n" +
                "SystemEnergyCentre in which a single SystemExchanger is shared across both air paths.\n" +
                "\n" +
                "Use it as a self-contained example of the factory API and as a smoke test: the 'report' output\n" +
                "lists one PASS/FAIL line per check (component creation, single plant room, JSON round-trip,\n" +
                "cooling duty and bypass factor) and 'success' is true only when every check passes.",
                "SAM",
                "Systems")
        {
        }

        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_run_", NickName = "_run_", Description = "Set to true to build the example twin-wheel system and run the self-check.\n\nDefaults to true.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_supplyAirflow_", NickName = "_supplyAirflow_", Description = "Design supply volumetric airflow [m3/s] used to size the example component duties.\n\nDefaults to 2.5 m3/s.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_extractAirflow_", NickName = "_extractAirflow_", Description = "Design extract volumetric airflow [m3/s] used to size the example extract-side duties.\n\nDefaults to 2.3 m3/s.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                return result.ToArray();
            }
        }

        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "systemEnergyCentre", NickName = "systemEnergyCentre", Description = "The example twin-wheel SystemEnergyCentre, ready to serialise or simulate.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "report", NickName = "report", Description = "Self-check results: one PASS/FAIL line per check.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "success", NickName = "success", Description = "True only when every self-check passes.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index;

            bool run = true;
            index = Params.IndexOfInputParam("_run_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref run);
            }

            if (!run)
            {
                return;
            }

            double supplyAirflow = SAM.Analytical.Systems.Mollier.TwinWheelExample.DefaultSupplyAirflow;
            index = Params.IndexOfInputParam("_supplyAirflow_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref supplyAirflow);
            }

            double extractAirflow = SAM.Analytical.Systems.Mollier.TwinWheelExample.DefaultExtractAirflow;
            index = Params.IndexOfInputParam("_extractAirflow_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref extractAirflow);
            }

            SystemEnergyCentre systemEnergyCentre = SAM.Analytical.Systems.Mollier.TwinWheelExample.Create(supplyAirflow, extractAirflow);
            bool success = SAM.Analytical.Systems.Mollier.TwinWheelExample.Verify(out List<string> messages);

            if (!success)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "One or more self-checks failed; see the 'report' output.");
            }

            index = Params.IndexOfOutputParam("systemEnergyCentre");
            if (index != -1)
            {
                dataAccess.SetData(index, systemEnergyCentre);
            }

            index = Params.IndexOfOutputParam("report");
            if (index != -1)
            {
                dataAccess.SetDataList(index, messages);
            }

            index = Params.IndexOfOutputParam("success");
            if (index != -1)
            {
                dataAccess.SetData(index, success);
            }
        }
    }
}
