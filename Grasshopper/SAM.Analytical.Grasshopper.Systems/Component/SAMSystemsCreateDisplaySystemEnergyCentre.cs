// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using SAM.Geometry.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMSystemsCreateDisplaySystemEnergyCentre : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("a3d8c1f6-4e57-49b2-9c0a-7d2e5f8b41c9");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public SAMSystemsCreateDisplaySystemEnergyCentre()
          : base(
                "SAMSystems.CreateDisplaySystemEnergyCentre",
                "SAMSystems.CreateDisplaySystemEnergyCentre",
                "Converts a logical SystemEnergyCentre (for example the output of the Mollier -> SAM_Systems\n" +
                "bridge) into a previewable schematic.\n" +
                "\n" +
                "Each air-handling component is laid out (one row per air system, in flow order) and given a\n" +
                "drawing symbol from the DisplaySystemManager, and each connection is re-created as a routed\n" +
                "polyline. Connect 'displaySystemObjects' straight into the canvas to preview/bake the schematic.\n" +
                "\n" +
                "The 'report' output lists any component whose type has no symbol (it is skipped, not drawn).",
                "SAM",
                "Systems")
        {
        }

        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemEnergyCentreParam() { Name = "_systemEnergyCentre", NickName = "_systemEnergyCentre", Description = "The logical SystemEnergyCentre to convert into a previewable schematic.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_displaySystemManager_", NickName = "_displaySystemManager_", Description = "Optional symbol library mapping component types to drawing symbols.\n\nDefaults to the bundled SAM_DisplaySystemManager.JSON.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                return result.ToArray();
            }
        }

        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "displaySystemObjects", NickName = "displaySystemObjects", Description = "The laid-out display components and connections. Preview/bake these to see the schematic.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "displaySystemPlantRooms", NickName = "displaySystemPlantRooms", Description = "The generated DisplaySystemPlantRoom(s) holding the laid-out schematic.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "report", NickName = "report", Description = "One line per component that has no symbol and was skipped.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index;

            SystemEnergyCentre systemEnergyCentre = null;
            index = Params.IndexOfInputParam("_systemEnergyCentre");
            if (index == -1 || !dataAccess.GetData(index, ref systemEnergyCentre) || systemEnergyCentre == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            DisplaySystemManager displaySystemManager = null;
            index = Params.IndexOfInputParam("_displaySystemManager_");
            if (index != -1)
            {
                ISystemJSAMObject systemJSAMObject = null;
                if (dataAccess.GetData(index, ref systemJSAMObject))
                {
                    displaySystemManager = systemJSAMObject as DisplaySystemManager;
                }
            }

            DisplaySystemEnergyCentre displaySystemEnergyCentre = SAM.Analytical.Systems.Create.DisplaySystemEnergyCentre(systemEnergyCentre, out List<string> report, displaySystemManager);

            List<DisplaySystemPlantRoom> displaySystemPlantRooms = new List<DisplaySystemPlantRoom>();
            List<ISystemJSAMObject> displaySystemObjects = new List<ISystemJSAMObject>();
            if (displaySystemEnergyCentre != null)
            {
                List<DisplaySystemPlantRoom> displaySystemPlantRooms_Temp = displaySystemEnergyCentre.GetSystemPlantRooms();
                if (displaySystemPlantRooms_Temp != null)
                {
                    foreach (DisplaySystemPlantRoom displaySystemPlantRoom in displaySystemPlantRooms_Temp)
                    {
                        if (displaySystemPlantRoom == null)
                        {
                            continue;
                        }

                        displaySystemPlantRooms.Add(displaySystemPlantRoom);

                        List<ISystemComponent> systemComponents = displaySystemPlantRoom.GetSystemComponents();
                        if (systemComponents != null)
                        {
                            foreach (ISystemComponent systemComponent in systemComponents)
                            {
                                if (systemComponent is IDisplaySystemObject)
                                {
                                    displaySystemObjects.Add(systemComponent);
                                }
                            }
                        }
                    }
                }
            }

            index = Params.IndexOfOutputParam("displaySystemObjects");
            if (index != -1)
            {
                dataAccess.SetDataList(index, displaySystemObjects);
            }

            index = Params.IndexOfOutputParam("displaySystemPlantRooms");
            if (index != -1)
            {
                dataAccess.SetDataList(index, displaySystemPlantRooms);
            }

            index = Params.IndexOfOutputParam("report");
            if (index != -1)
            {
                dataAccess.SetDataList(index, report);
            }
        }
    }
}
