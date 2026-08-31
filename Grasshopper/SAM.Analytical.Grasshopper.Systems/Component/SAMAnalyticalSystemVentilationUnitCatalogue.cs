// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Grasshopper.Systems
{
    /// <summary>
    /// Reads the ventilation-unit manufacturer catalogue and reports it as a selection-ready list plus what
    /// the catalogue holds that selection cannot yet use.
    /// <para>
    /// Reading and reporting only. Every decision - what counts as a usable capacity, why a template is
    /// unselectable - is <c>SAM.Analytical.Query</c>'s and <c>SAM.Analytical.Systems.Query</c>'s, so this
    /// component cannot drift from what <c>VentilationUnitCatalogueTests</c> exercises.
    /// </para>
    /// </summary>
    public class SAMAnalyticalSystemVentilationUnitCatalogue : GH_SAMVariableOutputParameterComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("8b6e1f3a-4d2c-4a91-9e7b-2c5f6a8d1e30");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public SAMAnalyticalSystemVentilationUnitCatalogue()
          : base("SAMAnalytical.SystemVentilationUnitCatalogue", "SAMAnalytical.SystemVentilationUnitCatalogue",
              "Reads the ventilation-unit manufacturer catalogue.\n" +
              "\n" +
              "ventilationUnitCapacityDescriptors feeds SAMAnalytical.PreparePartOIteration's ventilationUnitCapacityDescriptors_ " +
              "input - this component never selects a unit itself, it only exposes the catalogue selection reads from.\n" +
              "\n" +
              "A product whose maximum supply/extract airflow is unresolved (published performance data with no stated " +
              "maximum - the real Nuaire MRXBOXAB-ECO5-AECV / MR-ECO-COOL-V is exactly this today) is real and present in " +
              "the catalogue, but is deliberately left out of ventilationUnitCapacityDescriptors: guessing a maximum from " +
              "a performance table's largest published duty point is the specific mistake this seam exists to prevent. " +
              "It is reported instead through unselectableVentilationUnitTemplates and unselectableReasons, so an empty " +
              "ventilationUnitCapacityDescriptors here reads as 'nothing is selectable yet', never as 'the catalogue " +
              "failed to load' - those two states are reported differently (see the runtime messages).",
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
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "directory_", NickName = "directory_", Description = "Folder to read the ventilation-unit catalogue from.\n\nOptional: defaults to the installed SAM library location (the same default SAM.Analytical.Systems.Query.VentilationUnitTemplates uses).", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));

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
                result.Add(new GH_SAMParam(new GooObjectParam() { Name = "ventilationUnitCapacityDescriptors", NickName = "ventilationUnitCapacityDescriptors", Description = "Selectable products only - each one's identity, maximum supply/extract airflow and rank. Feed this into SAMAnalytical.PreparePartOIteration's ventilationUnitCapacityDescriptors_.\n\nA template without a resolved maximum supply/extract airflow is left out here rather than approximated - see unselectableVentilationUnitTemplates.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSAMObjectParam() { Name = "unselectableVentilationUnitTemplates", NickName = "unselectableVentilationUnitTemplates", Description = "Manufacturer products present in the catalogue but lacking a usable selection capacity. Still carries the template's full published performance data - it is just not offered to selection. Aligned item-for-item with unselectableReasons.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_String() { Name = "unselectableReasons", NickName = "unselectableReasons", Description = "Why each entry of unselectableVentilationUnitTemplates cannot be offered to a selection. Aligned item-for-item with unselectableVentilationUnitTemplates.", Access = GH_ParamAccess.list }, ParamVisibility.Binding));

                return [.. result];
            }
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            string directory = null;
            int index = Params.IndexOfInputParam("directory_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref directory);
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = null;
            }

            List<VentilationUnitTemplate> ventilationUnitTemplates = SAM.Analytical.Systems.Query.VentilationUnitTemplates(directory);
            if (ventilationUnitTemplates == null)
            {
                //Null, not empty: a directory that is missing, unreadable or fails schema validation is a
                //different fact from a catalogue that read fine and simply offers nothing selectable, and
                //the two must not collapse to the same "no descriptors" output.
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The ventilation unit catalogue could not be read - it is missing, unreadable, or fails schema validation. Check directory_ (or the installed SAM library, if left unconnected).");
                return;
            }

            List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = SAM.Analytical.Query.CapacityDescriptors(ventilationUnitTemplates);
            List<KeyValuePair<VentilationUnitTemplate, string>> unselectable = SAM.Analytical.Query.UnselectableVentilationUnitTemplates(ventilationUnitTemplates);

            if (ventilationUnitCapacityDescriptors.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, string.Format("The catalogue read {0} product template(s) and none of them are selectable yet - see unselectableReasons for why. This is a catalogue that loaded correctly, not a failed read.", ventilationUnitTemplates.Count));
            }

            index = Params.IndexOfOutputParam("ventilationUnitCapacityDescriptors");
            if (index != -1)
            {
                dataAccess.SetDataList(index, ventilationUnitCapacityDescriptors.ConvertAll(x => new GooObject(x)));
            }

            index = Params.IndexOfOutputParam("unselectableVentilationUnitTemplates");
            if (index != -1)
            {
                dataAccess.SetDataList(index, unselectable.ConvertAll(x => new GooSAMObject(x.Key)));
            }

            index = Params.IndexOfOutputParam("unselectableReasons");
            if (index != -1)
            {
                dataAccess.SetDataList(index, unselectable.ConvertAll(x => x.Value));
            }
        }
    }
}
