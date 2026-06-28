// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.IO;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemUpdateAirSystem : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("76c087b1-93ed-4a20-873a-cabf2e176f0c");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.1";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public SAMAnalyticalSystemUpdateAirSystem()
          : base("SAMAnalytical.UpdateVentilationSystem", "SAMAnalytical.UpdateVentilationSystem",
              "Updates the air system serving the given spaces on the analytical model's energy centre.\n" +
              "\n" +
              "Applies the supplied air system definition to the listed spaces, configuring their components from\n" +
              "the system templates, and returns the updated analytical model.",
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
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam() { Name = "_analyticalModel", NickName = "_analyticalModel", Description = "The SAM AnalyticalModel to update. A copy is returned with the updated energy centre.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSpaceParam() { Name = "_spaces", NickName = "_spaces", Description = "The SAM analytical spaces whose air system should be updated.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_airSystem", NickName = "_airSystem", Description = "The air system definition to apply to the listed spaces.", Access = GH_ParamAccess.item, Optional = false }, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_systemEnergyCentresDirectory", NickName = "_systemEnergyCentresDirectory", Description = "Folder of SystemEnergyCentre JSON templates to draw system definitions from.\n\nOptional: defaults to the local SAM library (%AppData%\\SAM\\resources\\Analytical\\Systems\\SystemEnergyCentre).", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));

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
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam { Name = "analyticalModel", NickName = "analyticalModel", Description = "The SAM AnalyticalModel with the updated air system applied to the spaces.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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
            int index;

            index = Params.IndexOfInputParam("_analyticalModel");
            AnalyticalModel analyticalModel = null;
            if (!dataAccess.GetData(index, ref analyticalModel) || analyticalModel == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<SystemEnergyCentre> systemEnergyCentres = [];
            string directory = null;
            index = Params.IndexOfInputParam("_systemEnergyCentresDirectory");
            if (index != -1 && dataAccess.GetData(index, ref directory) && Directory.Exists(directory))
            {
                if (new DirectoryInfo(directory)?.GetFiles("*.json") is FileInfo[] fileInfos)
                {
                    foreach (FileInfo fileInfo in fileInfos)
                    {
                        List<SystemEnergyCentre> systemEnergyCentres_File = Core.Convert.ToSAM<SystemEnergyCentre>(fileInfo.FullName);
                        if (systemEnergyCentres_File == null || systemEnergyCentres_File.Count == 0)
                        {
                            continue;
                        }

                        if (Analytical.Query.TryParse(Path.GetFileNameWithoutExtension(fileInfo.FullName), out SystemTemplate systemTemplate) && systemTemplate != null)
                        {
                            foreach (SystemEnergyCentre systemEnergyCentre_Temp in systemEnergyCentres)
                            {
                                systemEnergyCentre_Temp.SetValue(SystemEnergyCentreParameter.SystemTemplate, systemTemplate.Clone());
                            }
                        }

                        systemEnergyCentres.AddRange(systemEnergyCentres);
                    }
                }
            }

            if (systemEnergyCentres.Count == 0)
            {
                systemEnergyCentres = null;
            }

            if (!analyticalModel.TryGetValue(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre, out SystemEnergyCentre systemEnergyCentre) || systemEnergyCentre == null)
            {
                systemEnergyCentre = analyticalModel.SystemEnergyCentre(out HashSet<string> unavailableSystemTypeNames, systemEnergyCentres);
                if(unavailableSystemTypeNames != null && unavailableSystemTypeNames.Count != 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("Following system types not defined: {0}", string.Join(", ", unavailableSystemTypeNames)));
                }
            }

            if (systemEnergyCentre == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_spaces");
            List<Space> spaces = new List<Space>();
            if (index == -1 || !dataAccess.GetDataList(index, spaces))
            {
                spaces = null;
            }

            index = Params.IndexOfInputParam("_airSystem");
            ISystemObject systemObject = null;
            if (!dataAccess.GetData(index, ref systemObject) || systemObject == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            AirSystem airSystem = systemObject as AirSystem;
            if (airSystem != null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            systemEnergyCentre = new SystemEnergyCentre(systemEnergyCentre);

            airSystem = Analytical.Systems.Modify.UpdateAirSystem(systemEnergyCentre, airSystem, spaces);
            if(airSystem != null)
            {
                analyticalModel = new AnalyticalModel(analyticalModel);
                analyticalModel.SetValue(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre, systemEnergyCentre);
            }

            index = Params.IndexOfOutputParam("analyticalModel");
            if (index != -1)
            {
                dataAccess.SetData(index, analyticalModel);
            }
        }
    }
}