﻿using Grasshopper.Kernel;
using SAM.Core.Grasshopper.Systems.Properties;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Core.Grasshopper.Systems
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
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM3_0;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public SAMAnalyticalSystemPlantRoomRelatedObjects()
          : base("SystemPlantRoom.RelatedObjects", "SystemPlantRoom.RelatedObjects",
              "Related Objects in SystemPlantRoom",
              "SAM WIP", "Tas")
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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_systemObject", NickName = "_systemObject", Description = "SystemObject", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "type_", NickName = "type_", Description = "Type", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "systemObjects", NickName = "systemObjects", Description = "Related SystemObjects", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
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

            List<ISystemJSAMObject> result = null;
            if (type == null)
                result = systemPlantRoom.GetRelatedObjects(systemObject);
            else
                result = systemPlantRoom.GetRelatedObjects(systemObject, type);


            index = Params.IndexOfOutputParam("systemObjects");
            if (index != -1)
                dataAccess.SetDataList(index, result);

        }
    }
}