using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMSystemsMergeEnergyCentreByECs : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("3e1534f7-3ff4-40a0-8b29-1633699eeab5");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.1";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM3_0;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public SAMSystemsMergeEnergyCentreByECs()
          : base("SAMSystems.MergeEnergyCentreByECs", "SAMSystems.MergeEnergyCentreByECs",
              "Merges several SystemEnergyCentres into one.\n" +
              "\n" +
              "Combines the plantrooms (and their air systems) from the supplied energy centres into a single\n" +
              "SystemEnergyCentre. Optionally restrict the merge to specific air systems or plantrooms, and\n" +
              "rename groups to keep them unique across the merged result.",
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
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "_systemEnergyCentres", NickName = "_systemEnergyCentres", Description = "The SAM SystemEnergyCentres to merge into one.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new Core.Grasshopper.Systems.GooSystemParam() { Name = "airSystems_", NickName = "airSystems_", Description = "Optional subset of air systems to include in the merge. Leave empty to include all air systems from the supplied energy centres.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "systemPlantRooms_", NickName = "systemPlantRooms_", Description = "Optional subset of plantrooms to include in the merge. Leave empty to include all plantrooms from the supplied energy centres.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Boolean @boolean = null;

                @boolean = new global::Grasshopper.Kernel.Parameters.Param_Boolean() { Name = "_renameGroups_", NickName = "_renameGroups_", Description = "When true, system groups are renamed so they stay unique after merging (avoids name clashes between energy centres).\n\nDefaults to true.", Access = GH_ParamAccess.item };
                @boolean.SetPersistentData(true);
                result.Add(new GH_SAMParam(@boolean, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "systemEnergyCentre", NickName = "systemEnergyCentre", Description = "The single merged SystemEnergyCentre.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemGroupParam() { Name = "airSystemGroups", NickName = "airSystemGroups", Description = "The air system groups in the merged energy centre (one per merged air system).", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            int index_SystemEnergyCentre = Params.IndexOfOutputParam("systemEnergyCentre");
            int index_SystemAirGroups = Params.IndexOfOutputParam("airSystemGroups");

            SystemEnergyCentre systemEnergyCentre = null;
            List<AirSystemGroup> airSystemGroups = null;

            if (index_SystemEnergyCentre != -1)
            {
                dataAccess.SetData(index_SystemEnergyCentre, systemEnergyCentre);
            }

            if (index_SystemAirGroups != -1)
            {
                dataAccess.SetDataList(index_SystemAirGroups, airSystemGroups);
            }

            index = Params.IndexOfInputParam("_systemEnergyCentres");
            List<SystemEnergyCentre> systemEnergyCentres = new List<SystemEnergyCentre>();
            if (index == -1 || !dataAccess.GetDataList(index, systemEnergyCentres) || systemEnergyCentres == null || systemEnergyCentres.Count == 0)
            {
                return;
            }

            bool renameAirSystemGroups = true;
            index = Params.IndexOfInputParam("_renameGroups_");
            if (index == -1 || !dataAccess.GetData(index, ref renameAirSystemGroups))
            {
                renameAirSystemGroups = true;
            }

            systemEnergyCentres = systemEnergyCentres.ConvertAll(x => Core.Query.Clone(x));

            systemEnergyCentre = new SystemEnergyCentre(systemEnergyCentres[0].Name);

            List<ISystem> systems = new List<ISystem>();
            index = Params.IndexOfInputParam("airSystems_");
            if (index == -1 || !dataAccess.GetDataList(index, systems) || systems == null)
            {
                systems = new List<ISystem>();
            }

            List<AirSystem> airSystems = systems.FindAll(x => x is AirSystem).ConvertAll(x => Core.Query.Clone(x) as AirSystem);

            List<SystemPlantRoom> systemPlantRooms = new List<SystemPlantRoom>();
            index = Params.IndexOfInputParam("systemPlantRooms_");
            if (index == -1 || !dataAccess.GetDataList(index, systemPlantRooms) || systemPlantRooms == null)
            {
                systemPlantRooms = new List<SystemPlantRoom>();
            }

            List<ISystemJSAMObject> systemJSAMObjects = Check(systemEnergyCentres, airSystems, systemPlantRooms);
            if(systemJSAMObjects != null && systemJSAMObjects.Count != 0)
            {
                foreach(ISystemJSAMObject systemJSAMObject in systemJSAMObjects)
                {
                    string type = systemJSAMObject.GetType().Name;
                    string name = "???";
                    if(systemJSAMObject is Core.SAMObject)
                    {
                        name = ((Core.SAMObject)systemJSAMObject).Name;
                    }

                    Guid guid = Guid.Empty;
                    if(systemJSAMObject is Core.ISAMObject)
                    {
                        guid = ((Core.ISAMObject)systemJSAMObject).Guid;
                    }

                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("{0} (Name: {1}, Guid: {2}) is missing in provided SystemEnergyCentres.", type, name, guid));
                }
            }

            systemPlantRooms = systemPlantRooms.ConvertAll(x => Core.Query.Clone(x) as SystemPlantRoom);

            if (systemPlantRooms.Count != 0 && airSystems.Count != 0 && systemPlantRooms.Count != airSystems.Count)
            {
                if (!Core.Modify.MatchLength(systemPlantRooms, airSystems))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid number of inputs");
                    return;
                }
            }

            Analytical.Systems.Modify.Merge(systemEnergyCentre, systemEnergyCentres, airSystems, systemPlantRooms);

            if (index_SystemEnergyCentre != -1)
            {
                dataAccess.SetData(index_SystemEnergyCentre, systemEnergyCentre);
            }

            if(renameAirSystemGroups)
            {
                systemEnergyCentre.RenameAirSystemGroups();
                systemEnergyCentre.RenameSystemPlantRooms();
            }

            List<SystemPlantRoom> systemPlantRooms_Temp = systemEnergyCentre?.GetSystemPlantRooms();
            if (systemPlantRooms_Temp != null)
            {
                airSystemGroups = new List<AirSystemGroup>();
                foreach (SystemPlantRoom systemPlantRoom_Temp in systemPlantRooms_Temp)
                {
                    List<AirSystemGroup> airSystemGroups_SystemPlantRoom = systemPlantRoom_Temp?.GetSystemObjects<AirSystemGroup>();
                    if (airSystemGroups_SystemPlantRoom != null)
                    {
                        airSystemGroups.AddRange(airSystemGroups_SystemPlantRoom);
                    }
                }
            }

            if (index_SystemAirGroups != -1)
            {
                dataAccess.SetDataList(index_SystemAirGroups, airSystemGroups?.ConvertAll(x => new GooSystemGroup(x)));
            }
        }

        private static List<ISystemJSAMObject> Check(IEnumerable<SystemEnergyCentre> systemEnergyCentres, IEnumerable<AirSystem> airSystems, IEnumerable<SystemPlantRoom> systemPlantRooms)
        {
            List<AirSystem> airSystems_Temp = airSystems == null ? new List<AirSystem>() : new List<AirSystem>(airSystems);
            List<SystemPlantRoom> systemPlantRooms_Temp = systemPlantRooms == null ? new List<SystemPlantRoom>() : new List<SystemPlantRoom>(systemPlantRooms);

            if(systemEnergyCentres != null)
            {
                foreach (SystemEnergyCentre systemEnergyCentre in systemEnergyCentres)
                {
                    List<SystemPlantRoom> systemPlantRooms_SystemEnergyCentre = systemEnergyCentre.GetSystemPlantRooms();
                    if (systemPlantRooms_SystemEnergyCentre == null)
                    {
                        continue;
                    }

                    foreach(SystemPlantRoom systemPlantRoom_SystemEnergyCentre in systemPlantRooms_SystemEnergyCentre)
                    {
                        int index = systemPlantRooms_Temp.FindIndex(x => x.Guid == systemPlantRoom_SystemEnergyCentre.Guid);
                        while(index != -1)
                        {
                            systemPlantRooms_Temp.RemoveAt(index);
                            index = systemPlantRooms_Temp.FindIndex(x => x.Guid == systemPlantRoom_SystemEnergyCentre.Guid);
                        }

                        List<AirSystem> airSystems_SystemEnergyCentre = systemPlantRoom_SystemEnergyCentre.GetSystemObjects<AirSystem>();
                        if(airSystems_SystemEnergyCentre == null)
                        {
                            continue;
                        }

                        foreach(AirSystem airSystem_SystemEnergyCentre in airSystems_SystemEnergyCentre)
                        {
                            index = airSystems_Temp.FindIndex(x => x.Guid == airSystem_SystemEnergyCentre.Guid);
                            while (index != -1)
                            {
                                airSystems_Temp.RemoveAt(index);
                                index = airSystems_Temp.FindIndex(x => x.Guid == airSystem_SystemEnergyCentre.Guid);
                            }
                        }

                    }
                }
            }

            List<ISystemJSAMObject> result = new List<ISystemJSAMObject>();

            if (airSystems_Temp != null)
            {
                foreach (AirSystem airSystem in airSystems_Temp)
                {
                    result.Add(airSystem);
                }
            }

            if (systemPlantRooms_Temp != null)
            {
                foreach (SystemPlantRoom systemPlantRoom in systemPlantRooms_Temp)
                {
                    result.Add(systemPlantRoom);
                }
            }

            return result;
        }
    }
}