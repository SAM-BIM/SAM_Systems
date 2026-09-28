// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemEnergyCentreModifyFanByAirflows : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Initialises a new instance of the SystemEnergyCentre.ModifyFanByAirflows component.
        /// </summary>
        public SAMAnalyticalSystemEnergyCentreModifyFanByAirflows()
          : base("SystemEnergyCentre.ModifyFanByAirflows", "SystemEnergyCentre.ModifyFanByAirflows",
              "Updates selected fans in a SystemEnergyCentre using design flow source and design flow rate values.\n\nUse this component to modify the design flow type and design flow rate for selected system fans.\n\nIf systemEnergyCentre_ is not supplied, the component uses the SystemEnergyCentre stored in _analyticalModel. The component runs only when _run is true.",
              "SAM", "Systems")
        {
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new("53ff901d-4a76-4b3e-b9e1-6ca0c9d4b04d");

        /// <summary>
        /// The latest version of this component.
        /// </summary>
        public override string LatestComponentVersion => "1.0.2";

        /// <summary>
        /// Provides an icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_FanModify;

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

                    new GH_SAMParam(new GooSystemComponentParam()
                    {
                        Name = "_displaySystemFans",
                        NickName = "_displaySystemFans",
                        Description = "System fans to update in the SystemEnergyCentre.",
                        Access = GH_ParamAccess.list
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_String()
                    {
                        Name = "_designFlowSources",
                        NickName = "_designFlowSources",
                        Description = "Defines the design flow source for each fan.\nYou can use the enum name or integer value.\n0 = None\n1 = Value\n2 = All Attached Zones Flow Rate\n3 = All Attached Zones Fresh Air\n4 = Nearest Zone Flow Rate\n5 = Nearest Zone Fresh Air\n6 = Sized\n7 = All Attached Zones Sized",
                        Access = GH_ParamAccess.list,
                        Optional = true
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_Number()
                    {
                        Name = "_designFlowRates",
                        NickName = "_designFlowRates",
                        Description = "Design airflow rates for the listed fans.\nUsed to update the fan design flow value.",
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
                        Description = "Updated SystemEnergyCentre containing the modified fans.",
                        Access = GH_ParamAccess.item
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Systems.GooSystemComponentParam()
                    {
                        Name = "displaySystemFans",
                        NickName = "displaySystemFans",
                        Description = "Updated system fans returned by the component.",
                        Access = GH_ParamAccess.list
                    }, ParamVisibility.Binding),

                    new GH_SAMParam(new Param_Boolean()
                    {
                        Name = "Successful",
                        NickName = "Successful",
                        Description = "True if one or more fans were successfully updated; otherwise false.",
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
            int index_successful = Params.IndexOfOutputParam("Successful");
            if (index_successful != -1)
            {
                dataAccess.SetData(index_successful, false);
            }

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

            List<ISystemComponent> systemComponents = [];
            index = Params.IndexOfInputParam("_displaySystemFans");
            if (index == -1 || !dataAccess.GetDataList(index, systemComponents) || systemComponents is null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<SystemFan> systemFans = [.. systemComponents.FindAll(x => x is SystemFan).Cast<SystemFan>()];

            List<string> designFlowSourceNames = [];
            index = Params.IndexOfInputParam("_designFlowSources");
            if (index != -1)
            {
                if (!dataAccess.GetDataList(index, designFlowSourceNames) || designFlowSourceNames is null)
                {
                    designFlowSourceNames = [];
                }
            }

            List<double> designFlowRates = [];
            index = Params.IndexOfInputParam("_designFlowRates");
            if (index != -1)
            {
                if (!dataAccess.GetDataList(index, designFlowRates) || designFlowRates is null)
                {
                    designFlowRates = [];
                }
            }

            bool updated_SystemEnergyCentre = false;

            if (systemFans != null && systemFans.Count > 0)
            {
                for (int i = 0; i < systemFans.Count; i++)
                {
                    SystemFan systemFan = systemFans[i];
                    if (systemFan is null)
                    {
                        continue;
                    }

                    systemFan = Core.Query.Clone(systemFan);

                    if (designFlowSourceNames.Count != 0)
                    {
                        string designFlowSourceName = designFlowSourceNames[Core.Query.Clamp(i, 0, designFlowSourceNames.Count - 1)];
                        if (Core.Query.TryGetEnum(designFlowSourceName, out FlowRateType flowRateType))
                        {
                            systemFan.DesignFlowType = flowRateType;
                        }
                        else if (Core.Query.TryConvert(designFlowSourceName, out int id))
                        {
                            systemFan.DesignFlowType = (FlowRateType)id;
                        }
                    }

                    if (designFlowRates.Count != 0)
                    {
                        double designFlowRate = designFlowRates[Core.Query.Clamp(i, 0, designFlowRates.Count - 1)];

                        if (systemFan.DesignFlowRate is DesignConditionSizedFlowValue designConditionSizedFlowValue)
                        {
                            systemFan.DesignFlowRate = new DesignConditionSizedFlowValue(
                                designFlowRate,
                                designConditionSizedFlowValue.SizeFranction,
                                designConditionSizedFlowValue.SizingType,
                                designConditionSizedFlowValue.SizeValue1,
                                designConditionSizedFlowValue.SizeValue2,
                                designConditionSizedFlowValue.SizedFlowMethod,
                                designConditionSizedFlowValue.DesignConditionNames);
                        }
                        else if (systemFan.DesignFlowRate is SizedFlowValue sizedFlowValue)
                        {
                            systemFan.DesignFlowRate = new SizedFlowValue(designFlowRate, sizedFlowValue.SizeFranction);
                        }
                    }

                    systemFans[i] = systemFan;
                }

                List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre.GetSystemPlantRooms();
                if (systemPlantRooms != null && systemPlantRooms.Count != 0)
                {
                    foreach (SystemPlantRoom systemPlantRoom in systemPlantRooms)
                    {
                        bool updated_SystemPlantRoom = false;
                        foreach (SystemFan systemFan_Source in systemFans)
                        {
                            SystemFan systemFan_Destination = systemPlantRoom.Find<SystemFan>(x => x.Guid == systemFan_Source.Guid);
                            if (systemFan_Destination is null)
                            {
                                continue;
                            }

                            updated_SystemPlantRoom = true;
                            systemPlantRoom.Add(systemFan_Source);
                        }

                        if (updated_SystemPlantRoom)
                        {
                            systemEnergyCentre.Add(systemPlantRoom);
                            updated_SystemEnergyCentre = true;
                        }
                    }
                }

                if (updated_SystemEnergyCentre)
                {
                    analyticalModel = new AnalyticalModel(analyticalModel, new AdjacencyCluster(analyticalModel.AdjacencyCluster, true));
                    analyticalModel.SetValue(Analytical.Systems.AnalyticalModelParameter.SystemEnergyCentre, systemEnergyCentre);
                }
            }

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

            index = Params.IndexOfOutputParam("displaySystemFans");
            if (index != -1)
            {
                dataAccess.SetDataList(index, systemFans);
            }

            if (index_successful != -1)
            {
                dataAccess.SetData(index_successful, updated_SystemEnergyCentre);
            }
        }
    }
}