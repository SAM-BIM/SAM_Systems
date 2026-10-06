// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemConnectedSystemComponents : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new ("11f8fdbc-0270-44bf-a2d5-aca2e10a39f7");

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
        public SAMAnalyticalSystemConnectedSystemComponents()
          : base("SAMAnalytical.ConnectedSystemComponents", "SAMAnalytical.ConnectedSystemComponents",
              "Gets the components directly connected to a given component along a system.\n" +
              "\n" +
              "Starting from one component, this returns its immediate neighbours on the chosen system (e.g. an\n" +
              "AirSystem) - those one step upstream (In), one step downstream (Out), or both when no direction\n" +
              "is given. Use it to walk the connectivity of a plantroom one hop at a time.",
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
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "_systemPlantRoom", NickName = "_systemPlantRoom", Description = "The SAM SystemPlantRoom whose connectivity is queried.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new Core.Grasshopper.Systems.GooSystemParam() { Name = "_system", NickName = "_system", Description = "The system whose connections to follow (e.g. an AirSystem or LiquidSystem). A component can belong to several systems; this selects which network to traverse.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemComponentParam() { Name = "_systemComponent", NickName = "_systemComponent", Description = "The component to find the neighbours of.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "direction_", NickName = "direction_", Description = "Flow direction to search: \"In\" (upstream) or \"Out\" (downstream).\n\nOptional: leave empty to return neighbours in both directions.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));

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
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemComponentParam() { Name = "systemComponents", NickName = "systemComponents", Description = "The components directly connected to the input component along the chosen system and direction(s).", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            index = Params.IndexOfInputParam("_system");
            Core.Systems.ISystem system = null;
            if (index == -1 || !dataAccess.GetData(index, ref system) || system == null)
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

            Direction? direction = null;
            index = Params.IndexOfInputParam("direction_");
            if (index != -1)
            {
                string directionString = null;
                if (dataAccess.GetData(index, ref directionString) && directionString != null)
                {
                    direction = Core.Query.Enum<Direction>(directionString);
                }
            }

            List<ISystemComponent> systemComponents = new List<ISystemComponent>();

            if(direction == null || direction == Direction.Undefined)
            {
                systemPlantRoom.GetNextSystemComponents<ISystemComponent>(systemComponent, system, Direction.In)?.ForEach(x => systemComponents.Add(x));
                systemPlantRoom.GetNextSystemComponents<ISystemComponent>(systemComponent, system, Direction.Out)?.ForEach(x => systemComponents.Add(x));
            }
            else
            {
                systemPlantRoom.GetNextSystemComponents<ISystemComponent>(systemComponent, system, direction.Value)?.ForEach(x => systemComponents.Add(x));
            }

            index = Params.IndexOfOutputParam("systemComponents");
            if (index != -1)
            {
                dataAccess.SetDataList(index, systemComponents);
            }

        }
    }
}