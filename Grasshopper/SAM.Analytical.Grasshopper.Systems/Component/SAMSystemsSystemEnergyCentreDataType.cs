// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Grasshopper;
using System;
using SAM.Analytical.Systems;
using SAM.Analytical.Grasshopper.Systems.Properties;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class SAMSystemsSystemEnergyCentreDataType : GH_SAMEnumComponent<SystemEnergyCentreDataType>
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("2ce0997f-a5e2-457d-9baa-39fbc2a5ebb6");

        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.4";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_EnergyCentreValue;

        /// <summary>
        /// Panel Type
        /// </summary>
        public SAMSystemsSystemEnergyCentreDataType()
          : base("SAMSystems.SystemEnergyCentreDataType", "SAMSystems.SystemEnergyCentreDataType",
              "Select SystemEnergyCentreDataType \n* to be used with Tas.SystemResults",
              "SAM", "Systems")
        {
        }
    }
}