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
using System.Linq;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemModifyAirSystem : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("6f57c6d7-8669-45a0-8af6-9595572921c2");

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
        public SAMAnalyticalSystemModifyAirSystem()
          : base("SAMAnalytical.ModifyAirSystem", "SAMAnalytical.ModifyAirSystem",
              "Assigns spaces to an air system within a SystemEnergyCentre and returns the updated model.\n" +
              "\n" +
              "Use it to set which spaces a given air system serves. Optionally remove those spaces from any\n" +
              "other air system they were on, and clean up air systems left with no spaces.\n" +
              "\n" +
              "If systemEnergyCentre_ is not supplied, the energy centre stored on the analytical model is used.",
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
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam() { Name = "_analyticalModel", NickName = "_analyticalModel", Description = "The SAM AnalyticalModel to update. A copy is returned with the modified energy centre.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSpaceParam() { Name = "_spaces", NickName = "_spaces", Description = "The SAM analytical spaces to assign to the air system.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "systemEnergyCentre_", NickName = "systemEnergyCentre_", Description = "The SystemEnergyCentre to modify.\n\nOptional: if omitted, the energy centre stored on the analytical model is used.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_airSystem", NickName = "_airSystem", Description = "The air system that the spaces should be assigned to.", Access = GH_ParamAccess.item, Optional = false }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean param_Boolean;

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_cleanUnusedSystems_", NickName = "_cleanUnusedSystems_", Description = "When true, removes any air systems left serving no spaces after the reassignment.\n\nOptional: defaults to false.", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

                param_Boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_removeSpacesFormExistingAirSystem_", NickName = "_removeSpacesFormExistingAirSystem_", Description = "When true, the spaces are first removed from any other air system they are currently assigned to, so each space serves only one air system.\n\nOptional: defaults to false.", Access = GH_ParamAccess.item, Optional = true };
                param_Boolean.SetPersistentData(false);
                result.Add(new GH_SAMParam(param_Boolean, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam { Name = "analyticalModel", NickName = "analyticalModel", Description = "The SAM AnalyticalModel with the updated energy centre.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemObjectParam { Name = "airSystem", NickName = "airSystem", Description = "The air system the spaces were assigned to.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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

            analyticalModel = new AnalyticalModel(analyticalModel);

            if(!analyticalModel.TryGetValue(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre, out SystemEnergyCentre systemEnergyCentre) || systemEnergyCentre == null)
            {
                systemEnergyCentre = analyticalModel.SystemEnergyCentre(out HashSet<string> unavailableSystemTypeNames, systemEnergyCentres);
                if (unavailableSystemTypeNames != null && unavailableSystemTypeNames.Count != 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("Following system types not defined: {0}", string.Join(", ", unavailableSystemTypeNames)));
                }
            }

            if (systemEnergyCentre == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not get and create SystemEnergyCentre");
                return;
            }

            index = Params.IndexOfInputParam("_airSystem");
            ISystemObject systemObject = null;
            if (!dataAccess.GetData(index, ref systemObject) || systemObject == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            AirSystem airSystem = systemObject as AirSystem;
            if (airSystem == null)
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

            if(spaces == null)
            {
                spaces = analyticalModel.GetSpaces();
            }

            index = Params.IndexOfInputParam("systemEnergyCentre_");
            SystemEnergyCentre systemEnergyCentre_Source = null;
            if(index != -1)
            {
                dataAccess.GetData(index, ref systemEnergyCentre_Source);
            }

            SystemPlantRoom systemPlantRoom = null;

            if (systemEnergyCentre_Source == null)
            {
                airSystem = Analytical.Systems.Modify.UpdateAirSystem(systemEnergyCentre, airSystem, spaces);
            }
            else if (systemEnergyCentre_Source.TryGetSystem(airSystem.Guid, out systemPlantRoom, out airSystem) && systemPlantRoom != null && airSystem != null)
            {
                SystemPlantRoom systemPlantRoom_Destionation = systemEnergyCentre.GetSystemPlantRooms()?.FirstOrDefault();
                if(systemPlantRoom_Destionation == null)
                {
                    systemPlantRoom_Destionation = new SystemPlantRoom(systemPlantRoom.Name);
                    systemEnergyCentre.Add(systemPlantRoom_Destionation);
                }

                airSystem = Analytical.Systems.Modify.UpdateAirSystem(systemPlantRoom_Destionation, systemPlantRoom, airSystem, spaces);
                if(airSystem != null)
                {
                    systemEnergyCentre.Add(systemPlantRoom_Destionation);
                }
            }

            if(airSystem != null)
            {
                systemEnergyCentre.TryGetSystem(airSystem.Guid, out systemPlantRoom, out airSystem);
            }

            if(systemPlantRoom != null)
            {
                index = Params.IndexOfInputParam("_removeSpacesFormExistingAirSystem_");
                bool removeSpacesFormExistingAirSystem = false;
                if (index != -1 && dataAccess.GetData(index, ref removeSpacesFormExistingAirSystem) && removeSpacesFormExistingAirSystem)
                {
                    List<AirSystem> airSystems = systemPlantRoom.GetSystems<AirSystem>();
                    if (airSystems != null)
                    {
                        List<SystemSpace> systemSpaces = systemPlantRoom.GetSystemComponents<SystemSpace>(airSystem);
                        if (systemSpaces != null && systemSpaces.Count != 0)
                        {
                            airSystems.RemoveAll(x => x.Guid == airSystem.Guid);
                            foreach (AirSystem airSystem_Temp in airSystems)
                            {
                                Analytical.Systems.Modify.RemoveSpaces(systemPlantRoom, airSystem_Temp, systemSpaces.ConvertAll(x => x.Name));
                            }

                            systemEnergyCentre.Add(systemPlantRoom);
                        }
                    }
                }

                index = Params.IndexOfInputParam("_cleanUnusedSystems_");
                bool cleanUnusedSystems = false;
                if (index != -1 && dataAccess.GetData(index, ref cleanUnusedSystems) && cleanUnusedSystems)
                {
                    systemPlantRoom.CleanSystems<AirSystem>();
                    systemEnergyCentre.Add(systemPlantRoom);
                }
            }

            index = Params.IndexOfOutputParam("analyticalModel");
            if (index != -1)
            {
                dataAccess.SetData(index, analyticalModel);
            }

            index = Params.IndexOfOutputParam("airSystem");
            if (index != -1)
            {
                dataAccess.SetData(index, airSystem);
            }
        }
    }
}