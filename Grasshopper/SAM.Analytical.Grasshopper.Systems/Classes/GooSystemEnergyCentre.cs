// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper.Systems.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SAM.Core.Systems;
using SAM.Core.Grasshopper;
using Rhino;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class GooSystemEnergyCentre : GooJSAMObject<SystemEnergyCentre>
    {
        public GooSystemEnergyCentre()
            : base()
        {
        }

        public GooSystemEnergyCentre(SystemEnergyCentre systemEnergyCentre)
            : base(systemEnergyCentre)
        {
        }

        public override IGH_Goo Duplicate()
        {
            return new GooSystemEnergyCentre(Value);
        }

        public override string TypeName
        {
            get
            {
                return Value == null ? typeof(SystemEnergyCentre).Name : Value.GetType().Name;
            }
        }
    }

    public class GooSystemEnergyCentreParam : GH_PersistentParam<GooSystemEnergyCentre>
    {
        public override Guid ComponentGuid => new Guid("3f01a7cf-8268-496a-8247-602d1b4f109b");

        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_EnergyCentre;

        public GooSystemEnergyCentreParam()
            : base(typeof(SystemEnergyCentre).Name, typeof(SystemEnergyCentre).Name, typeof(SystemEnergyCentre).FullName.Replace(".", " "), "Params", "SAM")
        {
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GooSystemEnergyCentre> values)
        {
            throw new NotImplementedException();
        }

        protected override GH_GetterResult Prompt_Singular(ref GooSystemEnergyCentre value)
        {
            throw new NotImplementedException();
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Bake By Type", Menu_BakeByPanelType, Resources.SAM3_0, VolatileData.AllData(true).Any());
            Menu_AppendItem(menu, "Save As...", Menu_SaveAs, Resources.SAM3_0, VolatileData.AllData(true).Any());

            Menu_AppendSeparator(menu);

            base.AppendAdditionalMenuItems(menu);
        }

        private void Menu_BakeByPanelType(object sender, EventArgs e)
        {
            BakeGeometry_ByType(RhinoDoc.ActiveDoc);
        }

        public void BakeGeometry_ByType(RhinoDoc doc)
        {
            Modify.BakeGeometry_ByType(doc, VolatileData);
        }

        private void Menu_SaveAs(object sender, EventArgs e)
        {
            Core.Grasshopper.Query.SaveAs(VolatileData);
        }
    }
}