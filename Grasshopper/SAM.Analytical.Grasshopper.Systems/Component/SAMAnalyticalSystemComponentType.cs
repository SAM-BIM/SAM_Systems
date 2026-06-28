using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Analytical.Systems;
using SAM.Core.Grasshopper;
using System;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMAnalyticalSystemComponentType : GH_SAMEnumComponent<AnalyticalSystemComponentType>
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("fd4670c1-70e6-4008-a469-1e8565085293");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.1";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Small;

        /// <summary>
        /// About SAM Enum Component
        /// </summary>
        public SAMAnalyticalSystemComponentType()
          : base("SAM.AnalyticalSystemComponentType", "SAM.AnalyticalSystemComponentType",
              "Selects an analytical air-handling component type (e.g. cooling coil, heating coil, fan,\n" +
              "exchanger, humidifier) from a drop-down list.\n" +
              "\n" +
              "Use it to drive type-dependent logic or to filter/identify components by type.",
              "SAM", "Systems")
        {
        }
    }
}