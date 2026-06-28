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
                "Creates a SystemEnergyCentre from an ordered chain of Mollier (psychrometric) processes.\n" +
                "\n" +
                "Each Mollier process is mapped to the matching air-handling component (cooling coil,\n" +
                "heating coil, fan, heat-recovery exchanger, humidifier, mixing junction); duties, off-coil\n" +
                "setpoints and cooling bypass factors are derived from the process end-states and the\n" +
                "supplied design airflow, and the components are wired sequentially into a plantroom.\n" +
                "\n" +
                "To run a simulation, connect the created SystemEnergyCentre to\n" +
                "the SAMSystems.CreateTPDByTSDAndSystemEnergyCentre component.",
                "SAM",
                "Systems")
        {
        }

        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooMollierProcessParam() { Name = "_mollierProcesses", NickName = "_mollierProcesses", Description = "Ordered chain of SAM Mollier processes", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_designAirflow_", NickName = "_designAirflow_", Description = "Design volumetric airflow [m3/s].\nUsed to derive component duties from the intensive Mollier states.\nLeave empty to create components without duties.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_name_", NickName = "_name_", Description = "Name for the created SystemEnergyCentre", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                return result.ToArray();
            }
        }

        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "systemEnergyCentre", NickName = "systemEnergyCentre", Description = "SAM SystemEnergyCentre\nto simulate connect the SAMSystems.CreateTPDByTSDAndSystemEnergyCentre component.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index;

            List<IMollierProcess> mollierProcesses = new List<IMollierProcess>();
            index = Params.IndexOfInputParam("_mollierProcesses");
            if (index == -1 || !dataAccess.GetDataList(index, mollierProcesses) || mollierProcesses == null || mollierProcesses.Count == 0)
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

            string name = null;
            index = Params.IndexOfInputParam("_name_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref name);
            }

            SystemEnergyCentre systemEnergyCentre = SAM.Analytical.Systems.Mollier.Create.SystemEnergyCentre(mollierProcesses, designAirflow, string.IsNullOrWhiteSpace(name) ? "Energy Centre" : name);
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
