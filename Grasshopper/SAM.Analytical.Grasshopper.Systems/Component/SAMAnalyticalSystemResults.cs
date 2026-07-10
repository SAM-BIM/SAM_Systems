// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Types;
using SAM.Core.Grasshopper.Systems;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using SAM.Core.Grasshopper;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Grasshopper
{
    public class SAMAnalyticalSystemResults : GH_Component, IGH_VariableParameterComponent, IGH_SAMComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("b095f799-82ce-4a41-9c56-f9ccfa921065");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public string LatestComponentVersion => "1.0.2";

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override bool Obsolete
        {
            get
            {
                return Core.Grasshopper.Query.Obsolete(this);
            }
        }

        /// <summary>
        /// Initializes a new instance of the SAM_point3D class.
        /// </summary>
        public SAMAnalyticalSystemResults()
          : base("SAMAnalytical.SystemResults", "SAMAnalytical.SystemResults",
              "SAMAnalytical SystemResults \n* can be connected to all kinds of SystemResult ie. SystemSpaceResult, SystemCoolingCoilResult etc.",
              "SAM", "Systems")
        {
            SetValue("SAM_SAMVersion", Core.Query.CurrentVersion());
            SetValue("SAM_ComponentVersion", LatestComponentVersion);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);

            bool hasInputData = !Params.Input[0].VolatileData.IsEmpty;
            bool hasOutputParameters = Params.Output.Count > 0;

            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Get types", Menu_PopulateOutputParameters, Resources.SAM3_0, hasInputData, false);
            Menu_AppendItem(menu, "Remove unconnected types", Menu_RemoveUnconnectedParameters, Resources.SAM3_0, hasOutputParameters, false);

            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);

            Core.Grasshopper.Modify.AppendSourceCodeAdditionalMenuItem(this, menu);
            Core.Grasshopper.Modify.AppendNewComponentAdditionalMenuItem(this, menu);
        }

        private void PopulateOutputParameters(IEnumerable<GooObjectParam> gooParameterParams)
        {
            Dictionary<string, IList<IGH_Param>> dictionary = new Dictionary<string, IList<IGH_Param>>();
            foreach (IGH_Param param in Params.Output)
            {
                if (param.Recipients == null && param.Recipients.Count == 0)
                {
                    continue;
                }

                GooObjectParam gooParameterParam = param as GooObjectParam;
                if (gooParameterParam == null)
                {
                    continue;
                }

                dictionary.Add(gooParameterParam.Name, new List<IGH_Param>(gooParameterParam.Recipients));
            }

            while (Params.Output != null && Params.Output.Count() > 0)
            {
                Params.UnregisterOutputParameter(Params.Output[0]);
            }

            if (gooParameterParams != null)
            {
                foreach (GooObjectParam gooParameterParam in gooParameterParams)
                {
                    if (gooParameterParam == null)
                    {
                        continue;
                    }

                    AddOutputParameter(gooParameterParam);

                    IList<IGH_Param> @params = null;

                    if (!dictionary.TryGetValue(gooParameterParam.Name, out @params))
                    {
                        continue;
                    }

                    foreach (IGH_Param param in @params)
                    {
                        param.AddSource(gooParameterParam);
                    }
                }
            }

            Params.OnParametersChanged();
            ExpireSolution(true);
        }

        private void AddOutputParameter(IGH_Param param)
        {
            if (param.Attributes is null)
            {
                param.Attributes = new GH_LinkedParamAttributes(param, Attributes);
            }

            param.Access = GH_ParamAccess.list;
            Params.RegisterOutputParam(param);
        }

        private void Menu_PopulateOutputParameters(object sender, EventArgs e)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (object @object in Params.Input[0].VolatileData.AllData(true).OfType<object>())
            {
                object value = @object;
                if (@object is IGH_Goo)
                {
                    value = (@object as dynamic).Value;
                }

                SystemIndexedDoublesResult systemIndexedDoublesResult = value as SystemIndexedDoublesResult;
                if(value is SystemEnergyCentreResult)
                {
                    systemIndexedDoublesResult = (SystemIndexedDoublesResult)((SystemEnergyCentreResult)value);
                }

                List<string> keys = systemIndexedDoublesResult?.Keys;
                if(keys == null)
                {
                    continue;
                }

                foreach(string key in keys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    names.Add(key);
                }
            }

            RecordUndoEvent("Get Output Parameters");

            List<string> names_Temp = names.ToList();
            names_Temp.Sort();

            PopulateOutputParameters(names_Temp.ConvertAll(x => new GooObjectParam(x)));
        }

        private void Menu_RemoveUnconnectedParameters(object sender, EventArgs e)
        {
            RecordUndoEvent("Remove Unconnected Outputs");

            foreach (var output in Params.Output.ToArray())
            {
                if (output.Recipients.Count > 0)
                    continue;

                Params.UnregisterOutputParameter(output);
            }

            Params.OnParametersChanged();
            OnDisplayExpired(false);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager inputParamManager)
        {
            GooSystemResultParam gooSystemResultParam = new GooSystemResultParam() { Name = "_systemResults", NickName = "_systemResults", Description = "SAM Analytical SystemResults", Access = GH_ParamAccess.list };

            inputParamManager.AddParameter(gooSystemResultParam);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager outputParamManager)
        {

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="dataAccess">
        /// The DA object is used to retrieve from inputs and store in outputs.
        /// </param>
        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            List<ISystemResult> systemResults = new List<ISystemResult>();
            if (!dataAccess.GetDataList(0, systemResults) || systemResults == null)
            {
                return;
            }

            List<Tuple<int, string, List<GooObject>>> tuples = new List<Tuple<int, string, List<GooObject>>>();
            for (int i = 0; i < Params.Output.Count; ++i)
            {
                GooObjectParam gooParameterParam = Params.Output[i] as GooObjectParam;
                if (gooParameterParam == null || string.IsNullOrWhiteSpace(gooParameterParam.Name))
                {
                    continue;
                }

                tuples.Add(new Tuple<int, string, List<GooObject>>(i, gooParameterParam.Name, new List<GooObject>()));
            }

            for (int i = 0; i < systemResults.Count; i++)
            {
                ISystemResult systemResult = systemResults[i];
                if(systemResult == null)
                {
                    continue;
                }

                if(systemResult is SystemEnergyCentreResult)
                {
                    systemResult = (SystemIndexedDoublesResult)((SystemEnergyCentreResult)systemResult);
                }

                SystemIndexedDoublesResult systemIndexedDoublesResult = systemResult as SystemIndexedDoublesResult;
                if(systemIndexedDoublesResult == null)
                {
                    continue;
                }

                List<string> keys = systemIndexedDoublesResult.Keys;
                if(keys == null)
                {
                    continue;
                }

                foreach(string key in keys)
                {
                    List<GooObject> gooObjects = tuples.Find(x => key.Equals(x.Item2))?.Item3;
                    if (gooObjects == null)
                    {
                        continue;
                    }

                    gooObjects.Add(new GooObject(systemIndexedDoublesResult[key]));
                }
            }

            for (int i = 0; i < Params.Output.Count; ++i)
            {
                List<GooObject> gooObjects = tuples.Find(x => x.Item1 == i)?.Item3;
                dataAccess.SetDataList(i, gooObjects);
            }
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index) => false;

        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index) => side == GH_ParameterSide.Output;

        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index) => null;

        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index) => true;

        void IGH_VariableParameterComponent.VariableParameterMaintenance() { }


        public virtual void OnSourceCodeClick(object sender = null, object e = null)
        {
            Process.Start("https://github.com/HoareLea/SAM");
        }

        public string ComponentVersion
        {
            get
            {
                return GetValue("SAM_ComponentVersion", null);
            }
        }

        public string SAMVersion
        {
            get
            {
                return GetValue("SAM_SAMVersion", null);
            }
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            Message = ComponentVersion;
        }
    }
}

//using Grasshopper.Kernel;
//using SAM.Core.Grasshopper.Systems.Properties;
//using SAM.Core.Systems;
//using System;
//using System.Collections.Generic;

//namespace SAM.Core.Grasshopper.Systems
//{
//    public class SAMAnalyticalSystemResultValues : GH_SAMVariableOutputParameterComponent
//    {
//        /// <summary>
//        /// Gets the unique ID for this component. Do not change this ID after release.
//        /// </summary>
//        public override Guid ComponentGuid => new Guid("b095f799-82ce-4a41-9c56-f9ccfa921065");

//        /// <summary>
//        /// The latest version of this component
//        /// </summary>
//        public override string LatestComponentVersion => "1.0.0";

//        /// <summary>
//        /// Provides an Icon for the component.
//        /// </summary>
//        protected override System.Drawing.Bitmap Icon => Resources.SAM3_0;

//        public override GH_Exposure Exposure => GH_Exposure.primary;

//        /// <summary>
//        /// Initializes a new instance of the SAM_point3D class.
//        /// </summary>
//        public SAMAnalyticalSystemResultValues()
//          : base("SAMAnalytical.SystemResultValues", "SAMAnalytical.SystemResultValues",
//              "Related Objects in SystemPlantRoom",
//              "SAM", "Tas")
//        {
//        }

//        /// <summary>
//        /// Registers all the input parameters for this component.
//        /// </summary>
//        protected override GH_SAMParam[] Inputs
//        {
//            get
//            {
//                List<GH_SAMParam> result = new List<GH_SAMParam>();
//                result.Add(new GH_SAMParam(new GooSystemResultParam() { Name = "_systemResults", NickName = "_systemResults", Description = "SAM Analytical SystemResults", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
//                return result.ToArray();
//            }
//        }

//        /// <summary>
//        /// Registers all the output parameters for this component.
//        /// </summary>
//        protected override GH_SAMParam[] Outputs
//        {
//            get
//            {
//                List<GH_SAMParam> result = new List<GH_SAMParam>();
//                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "values", NickName = "values", Description = "Values", Access = GH_ParamAccess.tree }, ParamVisibility.Binding));
//                return result.ToArray();
//            }
//        }

//        /// <summary>
//        /// This is the method that actually does the work.
//        /// </summary>
//        /// <param name="dataAccess">
//        /// The DA object is used to retrieve from inputs and store in outputs.
//        /// </param>
//        protected override void SolveInstance(IGH_DataAccess dataAccess)
//        {
//            int index = -1;

//            SystemPlantRoom systemPlantRoom = null;
//            index = Params.IndexOfInputParam("_systemPlantRoom");
//            if (index == -1 || !dataAccess.GetData(index, ref systemPlantRoom) || systemPlantRoom == null)
//            {
//                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
//                return;
//            }

//            ISystemJSAMObject systemObject = null;
//            index = Params.IndexOfInputParam("_systemObject");
//            if (index == -1 || !dataAccess.GetData(index, ref systemObject) || systemObject == null)
//            {
//                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
//                return;
//            }

//            Type type = null;
//            index = Params.IndexOfInputParam("type_");
//            if (index != -1)
//            {
//                string fullTypeName = null;
//                if (dataAccess.GetData(index, ref fullTypeName))
//                {
//                    try
//                    {
//                        type = Type.GetType(fullTypeName);
//                    }
//                    catch
//                    {
//                        type = null;
//                    }
//                }
//            }

//            List<ISystemJSAMObject> result = null;
//            if (type == null)
//                result = systemPlantRoom.GetRelatedObjects(systemObject);
//            else
//                result = systemPlantRoom.GetRelatedObjects(systemObject, type);


//            index = Params.IndexOfOutputParam("systemObjects");
//            if (index != -1)
//                dataAccess.SetDataList(index, result);

//        }
//    }
//}