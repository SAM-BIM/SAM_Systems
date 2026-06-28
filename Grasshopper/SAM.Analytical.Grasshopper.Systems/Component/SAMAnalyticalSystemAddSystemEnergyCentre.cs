// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Grasshopper.Systems;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemAddSystemEnergyCentre : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("271e61dd-abc3-496e-b4d5-ca7be989c3ed");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public SAMAnalyticalSystemAddSystemEnergyCentre()
          : base("SAMAnalytical.AddSystemEnergyCentre", "SAMAnalytical.AddSystemEnergyCentre",
              "Attaches a SystemEnergyCentre to a SAM AnalyticalModel.\n" +
              "\n" +
              "The energy centre (the building's air systems and plantrooms) is stored on the analytical\n" +
              "model so it travels with the model through later steps and into simulation. The updated\n" +
              "analytical model is returned together with the energy centre that was attached.",
              "SAM", "Systems")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam() { Name = "_analyticalModel", NickName = "_analyticalModel", Description = "The SAM AnalyticalModel to attach the energy centre to. A copy is returned with the energy centre stored on it.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "_systemEnergyCentre_", NickName = "systemEnergyCentre", Description = "The SAM SystemEnergyCentre (air systems + plantrooms) to store on the analytical model.\n\nOptional: if omitted, any energy centre already present on the model is kept.", Optional = true, Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam() { Name = "analyticalModel", NickName = "analyticalModel", Description = "The SAM AnalyticalModel with the SystemEnergyCentre stored on it.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "systemEnergyCentre", NickName = "systemEnergyCentre", Description = "The SystemEnergyCentre that was attached to the analytical model.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
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
            index = Params.IndexOfInputParam("_systemEnergyCentre_");
            if(index != -1)
            {
                dataAccess.GetData(index, ref systemEnergyCentre);
            }

            if(systemEnergyCentre == null)
            {
                analyticalModel = new AnalyticalModel(analyticalModel);
                systemEnergyCentre = new SystemEnergyCentre(systemEnergyCentre);
                if(systemEnergyCentre != null)
                {
                    analyticalModel.SetValue(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre, systemEnergyCentre);
                }
            }

            if(systemEnergyCentre == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not create SystemEnergCentre");
                return;
            }

            index = Params.IndexOfOutputParam("analyticalModel");
            if (index != -1)
            {
                dataAccess.SetData(index, analyticalModel);
            }

            index = Params.IndexOfOutputParam("systemEnergyCentre");
            if (index != -1)
            {
                systemEnergyCentre = analyticalModel.GetValue<SystemEnergyCentre>(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre);
                dataAccess.SetData(index, systemEnergyCentre);
            }
        }
    }
}