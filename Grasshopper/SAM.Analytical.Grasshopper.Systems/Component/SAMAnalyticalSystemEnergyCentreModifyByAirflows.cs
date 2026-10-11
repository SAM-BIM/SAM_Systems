// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemEnergyCentreModifyByAirflows : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new("31a4c1d3-6cef-4c5b-a9b1-46684c947ea9");

        /// <summary>
        /// The latest version of this component.
        /// </summary>
        public override string LatestComponentVersion => "1.0.3";

        /// <summary>
        /// Provides an icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_AirflowSet;

        /// <summary>
        /// Initialises a new instance of the SystemEnergyCentre.ModifyByAirflows component.
        /// </summary>
        public SAMAnalyticalSystemEnergyCentreModifyByAirflows()
          : base("SystemEnergyCentre.ModifyByAirflows", "SystemEnergyCentre.ModifyByAirflows",
              "Updates airflow and fresh air settings for spaces in a SystemEnergyCentre.\n\n" +
              "Use this component to modify airflow and fresh air values for spaces identified by name.\n\n" +
              "Each space is handled independently using the *Modifies inputs:\n" +
              "  0 = do not change (existing value is preserved)\n" +
              "  1 = set value from the corresponding flow rate input\n" +
              "  2 = reset (clears the value in SAM)\n\n" +
              "Note on option 2 vs Tas export:\n" +
              "In SAM/Grasshopper, a reset space will report its airflow as 0 when queried. However, when the model is exported to a Tas TPD file the entry is written with sizingType = None, so Tas displays None (undefined) rather than 0.\n" +
              "If you want Tas to display an explicit zero, do not use 2 — instead use 1 with a flow rate of 0. That writes sizingType = Value with value = 0, which Tas displays as 0.\n\n" +
              "Summary of Tas TPD behaviour:\n" +
              "  Modify = 0           -> Tas value unchanged\n" +
              "  Modify = 1, rate > 0 -> sizingType = Value, displays the rate\n" +
              "  Modify = 1, rate = 0 -> sizingType = Value, displays 0\n" +
              "  Modify = 2           -> sizingType = None, displays None\n\n" +
              "If systemEnergyCentre_ is not supplied, the component uses the SystemEnergyCentre stored in _analyticalModel. The component runs only when _run is true.",
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
                Param_Boolean param_Boolean = new()
                {
                    Name = "_run",
                    NickName = "_run",
                    Description = "Set to true to run the update.\nIf false, the component does nothing.",
                    Access = GH_ParamAccess.item
                };

                param_Boolean.SetPersistentData(false);

                List<GH_SAMParam> result =
                [
                    new GH_SAMParam(new GooAnalyticalModelParam()
                    {
                        Name = "_analyticalModel",
                        NickName = "_analyticalModel",
                        Description = "Base SAM AnalyticalModel.\nUsed to read or update the SystemEnergyCentre.",
                        Access = GH_ParamAccess.item
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new GooSystemEnergyCentreParam()
                    {
                        Name = "systemEnergyCentre_",
                        NickName = "systemEnergyCentre_",
                        Description = "Optional SystemEnergyCentre to update.\nIf not supplied, the component uses the SystemEnergyCentre stored in _analyticalModel.",
                        Access = GH_ParamAccess.item,
                        Optional = true
                    }, ParamVisibility.Voluntary),

                    new GH_SAMParam(new Param_GenericObject()
                    {
                        Name = "spaceNames_",
                        NickName = "spaceNames_",
                        Description = "Names of the spaces to update in the SystemEnergyCentre.",
                        Access = GH_ParamAccess.list
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_Number()
                    {
                        Name = "_airflowFlowRates",
                        NickName = "_airflowFlowRates",
                        Description = "Airflow flow rates for the listed spaces [l/s].\nUsed when _airflowModifies = 1.",
                        Access = GH_ParamAccess.list,
                        Optional = true
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_Number()
                    {
                        Name = "_airflowModifies",
                        NickName = "_airflowModifies",
                        Description = "Defines how airflow is handled for each space.\nDefault is 0.\n" +
                                      "0 = do not change\n" +
                                      "1 = set value from _airflowFlowRates\n" +
                                      "2 = reset (SAM reports the value as 0; on Tas TPD export this is written as sizingType = None, so Tas displays None — not 0).\n\n" +
                                      "To make Tas display 0, use 1 with a flow rate of 0 instead of using 2.",
                        Access = GH_ParamAccess.list,
                        Optional = true
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_Number()
                    {
                        Name = "_airflowFreshAirRates",
                        NickName = "_airflowFreshAirRates",
                        Description = "Fresh air flow rates for the listed spaces [l/s].\nUsed when _airflowFreshAirModifies = 1.",
                        Access = GH_ParamAccess.list,
                        Optional = true
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_Number()
                    {
                        Name = "_airflowFreshAirModifies",
                        NickName = "_airflowFreshAirModifies",
                        Description = "Defines how fresh air is handled for each space.\nDefault is 0.\n" +
                                      "0 = do not change\n" +
                                      "1 = set value from _airflowFreshAirRates\n" +
                                      "2 = reset (SAM reports the value as 0; on Tas TPD export this is written as sizingType = None, so Tas displays None — not 0).\n\n" +
                                      "To make Tas display 0, use 1 with a flow rate of 0 instead of using 2.",
                        Access = GH_ParamAccess.list,
                        Optional = true
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(param_Boolean)
                ];
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
                return
                [
                    new GH_SAMParam(new GooAnalyticalModelParam()
                    {
                        Name = "AnalyticalModel",
                        NickName = "AnalyticalModel",
                        Description = "Updated SAM AnalyticalModel with the modified SystemEnergyCentre.",
                        Access = GH_ParamAccess.item
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new GooSystemEnergyCentreParam()
                    {
                        Name = "SystemEnergyCentre",
                        NickName = "SystemEnergyCentre",
                        Description = "Updated SystemEnergyCentre containing the modified space airflow values.",
                        Access = GH_ParamAccess.item
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_String()
                    {
                        Name = "SpaceNames",
                        NickName = "SpaceNames",
                        Description = "Names of the spaces that were successfully updated.",
                        Access = GH_ParamAccess.list
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_Boolean()
                    {
                        Name = "Successful",
                        NickName = "Successful",
                        Description = "True if one or more spaces were successfully updated; otherwise false.",
                        Access = GH_ParamAccess.item
                    }, ParamVisibility.Binding)
                ];
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
            int index;

            bool run = false;
            index = Params.IndexOfInputParam("_run");
            if (index == -1 || !dataAccess.GetData(index, ref run))
            {
                run = false;
            }

            if (!run)
            {
                return;
            }

            index = Params.IndexOfInputParam("_analyticalModel");
            AnalyticalModel analyticalModel = null;
            if (!dataAccess.GetData(index, ref analyticalModel) || analyticalModel == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            index = Params.IndexOfInputParam("systemEnergyCentre_");
            SystemEnergyCentre systemEnergyCentre = null;
            if (index != -1)
            {
                dataAccess.GetData(index, ref systemEnergyCentre);
            }

            if (systemEnergyCentre is null)
            {
                systemEnergyCentre = analyticalModel.GetValue<SystemEnergyCentre>(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre);
            }

            if (systemEnergyCentre is null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }


            List<GH_ObjectWrapper> gH_ObjectWrappers = [];

            index = Params.IndexOfInputParam("spaceNames_");
            if (index == -1 || !dataAccess.GetDataList(index, gH_ObjectWrappers) || gH_ObjectWrappers is null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<string> spacesNames = [];
            foreach (GH_ObjectWrapper gH_ObjectWrapper in gH_ObjectWrappers)
            {
                object @object = gH_ObjectWrapper.Value;
                if (@object is IGH_Goo)
                {
                    @object = (@object as dynamic).Value;
                }

                if (@object is string name)
                {
                    spacesNames.Add(name);
                }
                else if (@object is Space space)
                {
                    spacesNames.Add(space.Name);
                }
            }

            if (spacesNames is null || spacesNames.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<double> airflows = [];
            index = Params.IndexOfInputParam("_airflowFlowRates");
            if (index != -1)
            {
                if (!dataAccess.GetDataList(index, airflows) || airflows is null)
                {
                    airflows = [];
                }
            }

            List<double> freshAirs = [];
            index = Params.IndexOfInputParam("_airflowFreshAirRates");
            if (index != -1)
            {
                if (!dataAccess.GetDataList(index, freshAirs) || freshAirs is null)
                {
                    freshAirs = [];
                }
            }

            List<double> airflows_Code = [];
            index = Params.IndexOfInputParam("_airflowModifies");
            if (index != -1)
            {
                if (!dataAccess.GetDataList(index, airflows_Code) || airflows_Code is null)
                {
                    airflows_Code = [];
                }
            }

            List<double> freshAirs_Code = [];
            index = Params.IndexOfInputParam("_airflowFreshAirModifies");
            if (index != -1)
            {
                if (!dataAccess.GetDataList(index, freshAirs_Code) || freshAirs_Code is null)
                {
                    freshAirs_Code = [];
                }
            }

            List<string> spaceNames_Updated = [];

            if (airflows is null)
            {
                airflows = [];
            }

            if (airflows_Code is null)
            {
                airflows_Code = [];
            }

            if (freshAirs is null)
            {
                freshAirs = [];
            }

            if (freshAirs_Code is null)
            {
                freshAirs_Code = [];
            }

            Dictionary<string, Tuple<double?, double?>> dictionary = [];
            for (int i = 0; i < spacesNames.Count; i++)
            {
                string name = spacesNames[i];
                if (name == null)
                {
                    continue;
                }

                int code;

                // Air Flow
                code = 0;
                if (airflows_Code.Count != 0)
                {
                    code = System.Convert.ToInt32(airflows_Code[Core.Query.Clamp(i, 0, airflows_Code.Count - 1)]);
                }

                double? airflow = null;
                if (code == 1 && airflows is not null && airflows.Count != 0)
                {
                    airflow = airflows[Core.Query.Clamp(i, 0, airflows.Count - 1)];
                }
                else if (code == 2)
                {
                    airflow = double.NaN;
                }

                // Fresh Air
                code = 0;
                if (freshAirs_Code.Count != 0)
                {
                    code = System.Convert.ToInt32(freshAirs_Code[Core.Query.Clamp(i, 0, freshAirs_Code.Count - 1)]);
                }

                double? freshAir = null;
                if (code == 1 && freshAirs.Count != 0)
                {
                    freshAir = freshAirs[Core.Query.Clamp(i, 0, freshAirs.Count - 1)];
                }
                else if (code == 2)
                {
                    freshAir = double.NaN;
                }

                dictionary[spacesNames[i]] = new Tuple<double?, double?>(airflow, freshAir);
            }

            spaceNames_Updated = Analytical.Systems.Modify.UpdateSpaceAirflows(systemEnergyCentre, dictionary);

            index = Params.IndexOfOutputParam("AnalyticalModel");
            if (index != -1)
            {
                dataAccess.SetData(index, analyticalModel);
            }

            index = Params.IndexOfOutputParam("SystemEnergyCentre");
            if (index != -1)
            {
                dataAccess.SetData(index, systemEnergyCentre);
            }

            index = Params.IndexOfOutputParam("SpaceNames");
            if (index != -1)
            {
                dataAccess.SetDataList(index, spaceNames_Updated);
            }

            index = Params.IndexOfOutputParam("Successful");
            if (index != -1)
            {
                dataAccess.SetData(index, spaceNames_Updated != null && spaceNames_Updated.Count != 0);
            }
        }
    }
}