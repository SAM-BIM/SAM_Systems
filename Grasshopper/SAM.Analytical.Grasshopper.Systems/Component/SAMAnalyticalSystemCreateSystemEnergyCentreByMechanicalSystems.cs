// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemCreateSystemEnergyCentreByMechanicalSystems : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new ("168d4a90-00c3-424e-b4ec-d97437bac56b");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.1";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public SAMAnalyticalSystemCreateSystemEnergyCentreByMechanicalSystems()
          : base(
                  "SAMAnalytical.CreateSystemEnergyCentreByMechanicalSystems",
                  "SAMAnalytical.CreateSystemEnergyCentreByMechanicalSystems",
                  "Creates a SystemEnergyCentre from the mechanical (ventilation) systems defined on a SAM AnalyticalModel.\n" +
                  "\n" +
                  "For each distinct ventilation system referenced by the model's spaces, a matching plantroom is\n" +
                  "built from the system templates and its spaces are assigned to it. The energy centre is returned\n" +
                  "and also stored on the analytical model.\n" +
                  "\n" +
                  "To run a simulation, connect the SystemEnergyCentre to SAMSystems.CreateTPDByTSDAndSystemEnergyCentre.",
                  "SAM",
                  "Systems")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result =
                [
                    new GH_SAMParam(new GooAnalyticalModelParam() { Name = "_analyticalModel", NickName = "_analyticalModel", Description = "The SAM AnalyticalModel whose ventilation systems and spaces drive the energy centre. A copy is returned with the energy centre stored on it.", Access = GH_ParamAccess.item }, ParamVisibility.Binding),
                    new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "_systemEnergyCentre", NickName = "_systemEnergyCentre", Description = "An existing SystemEnergyCentre to extend / use as the template source.\n\nOptional: if omitted, the default system templates are used.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary),
                ];
                return [.. result];
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result =
                [
                    new GH_SAMParam(new GooAnalyticalModelParam() { Name = "analyticalModel", NickName = "analyticalModel", Description = "The SAM AnalyticalModel with the generated SystemEnergyCentre stored on it.", Access = GH_ParamAccess.item }, ParamVisibility.Binding),
                    new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "systemEnergyCentre", NickName = "systemEnergyCentre", Description = "SAM SystemEnergyCentre \n to simulate connect the SAMSystems.CreateTPDByTSDAndSystemEnergyCentre component.", Access = GH_ParamAccess.item }, ParamVisibility.Binding),
                ];
                return [.. result];
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="dataAccess">
        /// The DA object is used to retrieve from inputs and store in outputs.
        /// </param>
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index = -1;

            AnalyticalModel analyticalModel = null;
            index = Params.IndexOfInputParam("_analyticalModel");
            if (index == -1 || !dataAccess.GetData(index, ref analyticalModel) || analyticalModel == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            SystemEnergyCentre systemEnergyCentre = null;
            index = Params.IndexOfInputParam("_systemEnergyCentre");
            if (index == -1 || !dataAccess.GetData(index, ref systemEnergyCentre) || systemEnergyCentre == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            analyticalModel = new AnalyticalModel(analyticalModel);

            systemEnergyCentre = Analytical.Systems.Create.SystemEnergyCentreByMechanicalSystems(analyticalModel, out HashSet<string> unavailableSystemTypeNames, systemEnergyCentre);
            if (unavailableSystemTypeNames != null && unavailableSystemTypeNames.Count != 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("Following system types not defined: {0}", string.Join(", ", unavailableSystemTypeNames)));
            }

            Log log = Analytical.Systems.Create.Log(systemEnergyCentre);
            if (log is not null)
            {
                log.ToList().FindAll(x => x.LogRecordType == LogRecordType.Warning).ForEach(x => AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, x.ToString()));
                log.ToList().FindAll(x => x.LogRecordType == LogRecordType.Error).ForEach(x => AddRuntimeMessage(GH_RuntimeMessageLevel.Error, x.ToString()));
            }

            if (systemEnergyCentre != null)
            {
                analyticalModel.SetValue(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre, systemEnergyCentre);
            }

            index = Params.IndexOfOutputParam("analyticalModel");
            if (index != -1)
            {
                dataAccess.SetData(index, analyticalModel);
            }

            index = Params.IndexOfOutputParam("systemEnergyCentre");
            if (index != -1)
            {
                dataAccess.SetData(index, systemEnergyCentre);
            }
        }
    }
}