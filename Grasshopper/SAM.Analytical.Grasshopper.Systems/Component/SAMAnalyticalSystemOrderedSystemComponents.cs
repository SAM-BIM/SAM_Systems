// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemOrderedSystemComponents : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new ("32b7d078-f950-4e41-8b19-b93de65c6b01");

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
        public SAMAnalyticalSystemOrderedSystemComponents()
          : base("SAMAnalytical.OrderedSystemComponents", "SAMAnalytical.OrderedSystemComponents",
              "Walks a system from a starting component and returns the full chain of components in flow order.\n" +
              "\n" +
              "Unlike ConnectedSystemComponents (which returns only immediate neighbours), this follows the\n" +
              "connections all the way along the chosen system in the given direction, so you get the ordered\n" +
              "sequence of components (e.g. the air path through an air-handling unit).",
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
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "_systemPlantRoom", NickName = "_systemPlantRoom", Description = "The SAM SystemPlantRoom whose connectivity is traversed.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemComponentParam() { Name = "_systemComponent", NickName = "_systemComponent", Description = "The component to start walking from.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new Core.Grasshopper.Systems.GooSystemParam() { Name = "_system_", NickName = "_system_", Description = "The system whose connections to follow (e.g. an AirSystem).\n\nOptional: if omitted, the component's system is inferred.", Optional = true, Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "direction_", NickName = "direction_", Description = "Direction to walk: \"In\" (upstream) or \"Out\" (downstream).\n\nOptional: defaults to Out.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));

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
                result.Add(new GH_SAMParam(new GooSystemComponentParam() { Name = "systemComponents", NickName = "systemComponents", Description = "The ordered chain of components from the start component along the system and direction.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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

            index = Params.IndexOfInputParam("_system_");
            Core.Systems.ISystem system = null;
            if (index != -1)
            {
                dataAccess.GetData(index, ref system);
            }

            if(system == null)
            {
                List<Core.Systems.ISystem> systems = systemPlantRoom.GetRelatedObjects<Core.Systems.ISystem>(systemComponent);
                system = systems?.FirstOrDefault();
            }

            if(system == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No system detected for given system component");
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

            List<ISystemComponent> systemComponents = null;
            if(direction == null || !direction.HasValue)
            {
                systemComponents = [];
                systemPlantRoom.GetOrderedSystemComponents(systemComponent, system, Direction.In).ForEach(x => systemComponents.Add(x));
                systemComponents.Reverse();
                systemComponents.Add(systemComponent);
                systemPlantRoom.GetOrderedSystemComponents(systemComponent, system, Direction.Out).ForEach(x => systemComponents.Add(x));
            }
            else
            {
                systemComponents = systemPlantRoom.GetOrderedSystemComponents(systemComponent, system, direction.Value);
            }

            index = Params.IndexOfOutputParam("systemComponents");
            if (index != -1)
            {
                dataAccess.SetDataList(index, systemComponents);
            }

        }
    }
}