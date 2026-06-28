// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using SAM.Geometry.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemCreateDisplaySystemManager : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("8b67bdca-b7f6-4707-b90a-d15959f08316");

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
        public SAMAnalyticalSystemCreateDisplaySystemManager()
          : base("SAMAnalytical.CreateDisplaySystemManager", "SAMAnalytical.CreateDisplaySystemManager",
              "Builds a DisplaySystemManager that maps each air-handling component type to the geometry symbol\n" +
              "used to draw it on the schematic.\n" +
              "\n" +
              "Supply a list of geometry symbols and a matching list of component types (paired by position);\n" +
              "the resulting manager tells the schematic which symbol represents each component type.",
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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "_systemGeometrySymbols", NickName = "_systemGeometrySymbols", Description = "The geometry symbols to use for the components, one per component type (paired by list position with _analyticalSystemComponentTypes).", Access = GH_ParamAccess.list }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_String param_String = null;

                param_String = new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_analyticalSystemComponentTypes", NickName = "_analyticalSystemComponentTypes", Description = "The component types each symbol represents (paired by list position with _systemGeometrySymbols). Use the SAM.AnalyticalSystemComponentType picker.", Access = GH_ParamAccess.list };

                result.Add(new GH_SAMParam(param_String, ParamVisibility.Binding));
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
                result.Add(new GH_SAMParam(new GooSystemObjectParam() { Name = "displaySystemManager", NickName = "displaySystemManager", Description = "The DisplaySystemManager mapping component types to their drawing symbols.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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
            index = Params.IndexOfInputParam("_systemGeometrySymbols");
            if (index == -1 || !dataAccess.GetDataList(index, systemObjects) || systemObjects == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<string> texts = new List<string>();
            index = Params.IndexOfInputParam("_analyticalSystemComponentTypes");
            if (index == -1 || !dataAccess.GetDataList(index, texts) || texts == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            int count = Math.Min(systemObjects.Count, texts.Count);

            SystemGeometrySymbolManager systemGeometrySymbolManager = new SystemGeometrySymbolManager();
            for (int i =0; i < count; i++)
            {
                if (!Core.Query.TryGetEnum(texts[i], out AnalyticalSystemComponentType analyticalSystemComponentType) || analyticalSystemComponentType == AnalyticalSystemComponentType.Undefined)
                {
                    continue;
                }

                Type type = Analytical.Systems.Query.Type(analyticalSystemComponentType);
                if(type == null)
                {
                    continue;
                }

                SystemGeometrySymbol systemGeometrySymbol = systemObjects[i] as SystemGeometrySymbol;
                if (systemGeometrySymbol == null)
                {
                    continue;
                }

                systemGeometrySymbolManager.Add(type, systemGeometrySymbol);
            }

            DisplaySystemManager result = new DisplaySystemManager();
            result.SystemGeometrySymbolManager = systemGeometrySymbolManager;

            index = Params.IndexOfOutputParam("displaySystemManager");
            if (index != -1)
            {
                dataAccess.SetData(index, result);
            }

        }
    }
}