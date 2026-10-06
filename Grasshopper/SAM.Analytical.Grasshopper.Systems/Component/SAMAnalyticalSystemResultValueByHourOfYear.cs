// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemResultValueByHourOfYear : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("8e927e8f-58d2-4d07-8952-86f09cdfb6a6");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.3";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public SAMAnalyticalSystemResultValueByHourOfYear()
          : base("SAMAnalytical.SystemResultValueByHourOfYear", "SAMAnalytical.SystemResultValueByHourOfYear",
              "Reads a single value from an indexed system result at a given hour of the year.\n" +
              "\n" +
              "System results store one value per hour (0-8759). Supply the result and an hour index to read the\n" +
              "value for that hour, e.g. a coil load or temperature at a specific time step.",
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
                result.Add(new GH_SAMParam(new GooIndexedObjectsParam() { Name = "_result", NickName = "_result", Description = "An indexed (hourly) system result - a series of doubles, one per hour of the year.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                global::Grasshopper.Kernel.Parameters.Param_Integer integer = null;

                integer = new global::Grasshopper.Kernel.Parameters.Param_Integer() { Name = "_hourOfYear", NickName = "_hourOfYear", Description = "Hour Of Year index [0-8760]", Access = GH_ParamAccess.item };

                result.Add(new GH_SAMParam(integer, ParamVisibility.Binding));
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "value", NickName = "value", Description = "The result value at the requested hour of the year.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
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

            IIndexedObjects indexedObjects = null;
            index = Params.IndexOfInputParam("_result");
            if (index == -1 || !dataAccess.GetData(index, ref indexedObjects) || indexedObjects == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }



            int valueIndex = -1;
            index = Params.IndexOfInputParam("_hourOfYear");
            if (index == -1 || !dataAccess.GetData(index, ref valueIndex) || valueIndex == -1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            double result = double.NaN;

            IndexedDoubles indexedDoubles = indexedObjects as IndexedDoubles;
            if (indexedDoubles != null)
            {
                result = indexedDoubles[valueIndex];
            }

            index = Params.IndexOfOutputParam("value");
            if (index != -1)
                dataAccess.SetData(index, result);

        }
    }
}