using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core.Grasshopper;
using SAM.Core.Grasshopper.Systems;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemPlantRoomRelatedObjects : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("7e6476fa-9fcc-448a-82ee-9f08fbdc03de");

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
        public SAMAnalyticalSystemPlantRoomRelatedObjects()
          : base("SystemPlantRoom.RelatedObjects", "SystemPlantRoom.RelatedObjects",
              "Gets the objects related to a given object within a plantroom, optionally filtered by type.\n" +
              "\n" +
              "Returns everything the supplied object is related to (components, connections, systems, sensors,\n" +
              "groups, results, ...). Provide an optional type to return only related objects of that kind.",
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
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "_systemPlantRoom", NickName = "_systemPlantRoom", Description = "SAM Analytical SystemPlantRoom", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_systemObject", NickName = "_systemObject", Description = "The object whose related objects should be returned.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "type_", NickName = "type_", Description = "Optional type-name filter; only related objects of this type are returned (e.g. component, connection, system, sensor, group). Leave empty to return all related objects.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "systemObjects", NickName = "systemObjects", Description = "The related system objects (filtered by type when one is given).", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            SystemPlantRoom systemPlantRoom = null;
            index = Params.IndexOfInputParam("_systemPlantRoom");
            if (index == -1 || !dataAccess.GetData(index, ref systemPlantRoom) || systemPlantRoom == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            ISystemJSAMObject systemObject = null;
            index = Params.IndexOfInputParam("_systemObject");
            if (index == -1 || !dataAccess.GetData(index, ref systemObject) || systemObject == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Null data item");
                return;
            }

            Type type = null;
            index = Params.IndexOfInputParam("type_");
            if (index != -1)
            {
                string fullTypeName = null;
                if (dataAccess.GetData(index, ref fullTypeName))
                {
                    try
                    {
                        type = Type.GetType(fullTypeName);
                    }
                    catch
                    {
                        type = null;
                    }
                }
            }

            List<ISystemJSAMObject> result = type == null ? systemPlantRoom.GetRelatedObjects(systemObject) : systemPlantRoom.GetRelatedObjects(systemObject, type);
            if (systemObject.GetType() == typeof(LiquidSystem))
            {
                List<ISystemComponent> systemComponents = systemPlantRoom.GetSystemComponents();
                if (systemComponents != null)
                {
                    foreach (ISystemComponent systemComponent in systemComponents)
                    {
                        List<ISystem> systems = systemPlantRoom.GetRelatedObjects<ISystem>(systemComponent);
                        if (systems != null && systems.Count > 0)
                        {
                            continue;
                        }

                        if(result == null)
                        {
                            result = new List<ISystemJSAMObject>();
                        }

                        result.Add(systemComponent);
                    }
                }
            }

            index = Params.IndexOfOutputParam("systemObjects");
            if (index != -1)
            {
                dataAccess.SetDataList(index, result);
            }

        }
    }
}