// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemConnectedSensor : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new ("8341425d-783f-49ad-a8db-d5269d128800");

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
        public SAMAnalyticalSystemConnectedSensor()
          : base("SAMAnalytical.ConnectedSensor", "SAMAnalytical.ConnectedSensor",
              "Finds the SystemSensor that a given system controller reads from, within a plantroom.\n" +
              "\n" +
              "A controller references a sensor by id; this component resolves that reference and returns the\n" +
              "actual SystemSensor object from the plantroom so it can be inspected or rewired.",
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
                List<GH_SAMParam> result = [];
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "_systemPlantRoom", NickName = "_systemPlantRoom", Description = "The SAM SystemPlantRoom that contains the controller and its sensor.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_systemController", NickName = "_systemController", Description = "The system controller whose referenced sensor should be resolved.", Access = GH_ParamAccess.item}, ParamVisibility.Binding));

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
                List<GH_SAMParam> result = [];
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "systemSensor", NickName = "systemSensor", Description = "The SystemSensor referenced by the controller (the sensor it reads its measured value from).", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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

            SystemPlantRoom systemPlantRoom = null;
            index = Params.IndexOfInputParam("_systemPlantRoom");
            if (index == -1 || !dataAccess.GetData(index, ref systemPlantRoom) || systemPlantRoom == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            SystemSensorController systemSensorController = null;
            index = Params.IndexOfInputParam("_systemController");
            if (index == -1 || !dataAccess.GetData(index, ref systemSensorController) || systemSensorController == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            ISystemSensor systemSensor = systemPlantRoom.GetSystemObject<ISystemSensor>(x => x.Guid.ToString() == systemSensorController.SensorReference);

            index = Params.IndexOfOutputParam("systemSensor");
            if (index != -1)
            {
                dataAccess.SetData(index, systemSensor);
            }

        }
    }
}