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
    public class SAMAnalyticalSystemCreateDisplaySystemConnectorManager : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("1c03806e-54d8-4136-923c-81c263aeee20");

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
        public SAMAnalyticalSystemCreateDisplaySystemConnectorManager()
          : base("SAMAnalytical.CreateDisplaySystemConnectorManager", "SAMAnalytical.CreateDisplaySystemConnectorManager",
              "Groups a set of display system connectors into a single DisplaySystemConnectorManager.\n" +
              "\n" +
              "The manager bundles all the ports of a component for display and connection on the schematic.",
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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_displaySystemConnectors", NickName = "_displaySystemConnectors", Description = "The display system connectors (ports) to bundle into one manager, e.g. from SAMAnalytical.SystemConnector.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "displaySystemConnectorManager", NickName = "displaySystemConnectorManager", Description = "The DisplaySystemConnectorManager bundling the supplied connectors.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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

            List<ISystemObject> systemObjects = new List<ISystemObject>();
            index = Params.IndexOfInputParam("_displaySystemConnectors");
            if (index == -1 || !dataAccess.GetDataList(index, systemObjects) || systemObjects == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<DisplaySystemConnector> displaySystemConnectors = new List<DisplaySystemConnector>();
            foreach(ISystemObject systemObject in displaySystemConnectors)
            {
                if(systemObject is DisplaySystemConnector)
                {
                    displaySystemConnectors.Add((DisplaySystemConnector)systemObject);
                }
            }

            index = Params.IndexOfOutputParam("displaySystemConnectorManager");
            if (index != -1)
            {
                dataAccess.SetData(index, new DisplaySystemConnectorManager(displaySystemConnectors));
            }

        }
    }
}