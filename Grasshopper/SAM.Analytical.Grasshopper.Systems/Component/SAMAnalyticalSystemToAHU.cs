// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

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
    public class SAMAnalyticalSystemToAHU : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("6df8ab41-f2bf-4a2a-84ab-427c072c655c");

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
        public SAMAnalyticalSystemToAHU()
          : base("SAMAnalytical.SystemToAHU", "SAMAnalytical.SystemToAHU",
              "Converts a system within a plantroom into an AirHandlingUnit (the older SAM.Analytical AHU model).\n" +
              "\n" +
              "Reads the components connected on the given system and assembles them into an equivalent\n" +
              "AirHandlingUnit, bridging the SAM_Systems model back to the analytical AHU representation.",
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
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "_systemPlantRoom", NickName = "_systemPlantRoom", Description = "The SAM SystemPlantRoom that contains the system to convert.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_system", NickName = "_system", Description = "The system (e.g. an AirSystem) whose connected components form the air-handling unit.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooAirHandlingUnitParam() { Name = "aHU", NickName = "aHU", Description = "The equivalent SAM.Analytical AirHandlingUnit assembled from the system's components.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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

            ISystemJSAMObject systemJSAMObject = null;
            index = Params.IndexOfInputParam("_system");
            if (index == -1 || !dataAccess.GetData(index, ref systemJSAMObject) || systemJSAMObject == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            AirSystem airSystem = systemJSAMObject as AirSystem;
            if(airSystem == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;

            }

            AirHandlingUnit airHandlingUnit = systemPlantRoom.ToSAM(airSystem);

            index = Params.IndexOfOutputParam("aHU");
            if (index != -1)
            {
                dataAccess.SetData(index, airHandlingUnit);
            }
        }
    }
}