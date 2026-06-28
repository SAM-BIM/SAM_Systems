// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Mollier;
using SAM.Core.Systems;
using SAM.Geometry.Grasshopper.Mollier;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMSystemsCreatePlantRoomByMollier : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("c8f4d3b2-5e7a-4b9c-8d21-6f3a1b4c7e90");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public SAMSystemsCreatePlantRoomByMollier()
          : base(
                "SAMSystems.CreatePlantRoomByMollier",
                "SAMSystems.CreatePlantRoomByMollier",
                "Converts a sequence of psychrometric (Mollier) processes into a connected SystemPlantRoom.\n" +
                "\n" +
                "This is the same Mollier -> air-handling-system conversion as\n" +
                "SAMSystems.CreateEnergyCentreByMollier, but it returns the plantroom on its own (without\n" +
                "wrapping it in a SystemEnergyCentre), so it can be added to an existing energy centre or\n" +
                "combined with other plantrooms.\n" +
                "\n" +
                "Each Mollier process is mapped to the matching air-handling component (cooling coil, heating\n" +
                "coil, fan, heat-recovery exchanger, humidifier, mixing junction); duties, off-coil setpoints\n" +
                "and the cooling bypass factor are derived from the process end-states and the design airflow,\n" +
                "and the components are wired in process order along an air system.\n" +
                "\n" +
                "Supplying an extract chain models heat recovery with a supply and extract air path, sharing a\n" +
                "single SystemExchanger across both paths (twin-wheel).",
                "SAM",
                "Systems")
        {
        }

        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooMollierProcessParam() { Name = "_supplyMollierProcesses", NickName = "_supplyMollierProcesses", Description = "Ordered supply-side chain of SAM Mollier processes (e.g. mixing -> heat recovery -> cooling -> heating -> fan).\n\nThe list order defines the air flow direction and the resulting component connectivity. Each process should start where the previous one ended (process.End -> next process.Start).", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_supplyAirflow_", NickName = "_supplyAirflow_", Description = "Design supply volumetric airflow in cubic metres per second [m3/s].\n\nRequired to size component duties (duty = mass flow x enthalpy change, with mass flow = airflow x inlet moist-air density). Off-coil setpoints and bypass factors do not depend on it.\n\nLeave empty to create components and connectivity without computed duties.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooMollierProcessParam() { Name = "_extractMollierProcesses_", NickName = "_extractMollierProcesses_", Description = "Optional ordered extract-side chain of SAM Mollier processes (room air -> heat recovery -> extract fan).\n\nWhen supplied, a heat-recovery exchanger present in both chains is created once and shared across both air paths (supply on path 1, extract on path 2) - a twin-wheel unit modelled as a single device.\n\nLeave empty for a supply-only air handling unit.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "_extractAirflow_", NickName = "_extractAirflow_", Description = "Design extract volumetric airflow in cubic metres per second [m3/s], used to size the extract-side component duties. Only relevant when an extract chain is supplied.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "_name_", NickName = "_name_", Description = "Name for the created SystemPlantRoom.\n\nDefaults to \"Plant Room\" when left empty.", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                return result.ToArray();
            }
        }

        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSystemPlantRoomParam() { Name = "systemPlantRoom", NickName = "systemPlantRoom", Description = "The generated SAM SystemPlantRoom: air-handling components connected in the order of the supplied Mollier processes.\n\nAdd it to a SystemEnergyCentre (e.g. with SAMAnalytical.AddSystemEnergyCentre) to simulate.", Access = GH_ParamAccess.item }, ParamVisibility.Binding));
                return result.ToArray();
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index;

            List<IMollierProcess> supplyMollierProcesses = new List<IMollierProcess>();
            index = Params.IndexOfInputParam("_supplyMollierProcesses");
            if (index == -1 || !dataAccess.GetDataList(index, supplyMollierProcesses) || supplyMollierProcesses == null || supplyMollierProcesses.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            double supplyAirflow = double.NaN;
            index = Params.IndexOfInputParam("_supplyAirflow_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref supplyAirflow);
            }

            List<IMollierProcess> extractMollierProcesses = new List<IMollierProcess>();
            index = Params.IndexOfInputParam("_extractMollierProcesses_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, extractMollierProcesses);
            }

            double extractAirflow = double.NaN;
            index = Params.IndexOfInputParam("_extractAirflow_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref extractAirflow);
            }

            string name = null;
            index = Params.IndexOfInputParam("_name_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref name);
            }

            string plantRoomName = string.IsNullOrWhiteSpace(name) ? "Plant Room" : name;

            SystemPlantRoom systemPlantRoom;
            if (extractMollierProcesses != null && extractMollierProcesses.Count != 0)
            {
                systemPlantRoom = SAM.Analytical.Systems.Mollier.Create.SystemPlantRoom(supplyMollierProcesses, extractMollierProcesses, supplyAirflow, extractAirflow, plantRoomName);
            }
            else
            {
                systemPlantRoom = SAM.Analytical.Systems.Mollier.Create.SystemPlantRoom(supplyMollierProcesses, supplyAirflow, plantRoomName);
            }

            if (systemPlantRoom == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No SystemPlantRoom could be created from the supplied Mollier processes.");
            }

            index = Params.IndexOfOutputParam("systemPlantRoom");
            if (index != -1)
            {
                dataAccess.SetData(index, systemPlantRoom);
            }
        }
    }
}
