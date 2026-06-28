// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemConnectedSystemGroups : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new ("761a15c3-a6da-491f-b959-7d991a46bb52");

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
        public SAMAnalyticalSystemConnectedSystemGroups()
          : base("SAMAnalytical.ConnectedSystemGroups", "SAMAnalytical.ConnectedSystemGroups",
              "Gets the system groups that a given component belongs to within a plantroom.\n" +
              "\n" +
              "System groups are logical groupings of components (e.g. an air-handling unit). This returns every\n" +
              "group related to the supplied component.",
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
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "_systemPlantRoom", NickName = "_systemPlantRoom", Description = "The SAM SystemPlantRoom that holds the component and its groups.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemComponentParam() { Name = "_systemComponent", NickName = "_systemComponent", Description = "The component whose owning system groups should be returned.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "systemGroups", NickName = "systemGroups", Description = "The system groups the component belongs to.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            index = Params.IndexOfInputParam("_systemPlantRoom");
            SystemPlantRoom systemPlantRoom = null;
            if (index == -1 || !dataAccess.GetData(index, ref systemPlantRoom) || systemPlantRoom == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("_systemComponent");
            ISystemComponent systemComponent = null;
            if (index == -1 || !dataAccess.GetData(index, ref systemComponent) || systemComponent == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<ISystemJSAMObject> systemGroups = systemPlantRoom.GetRelatedObjects<ISystemGroup>(systemComponent)?.ConvertAll(x => x as ISystemJSAMObject);

            index = Params.IndexOfOutputParam("systemGroups");
            if (index != -1)
            {
                dataAccess.SetDataList(index, systemGroups);
            }

        }
    }
}