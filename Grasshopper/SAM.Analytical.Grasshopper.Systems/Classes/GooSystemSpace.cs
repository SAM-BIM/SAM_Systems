// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Core.Grasshopper;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Rhino;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class GooSystemSpace : GooJSAMObject<ISystemSpace>
    {
        public GooSystemSpace()
            : base()
        {
        }

        public GooSystemSpace(ISystemSpace systemSpace)
            : base(systemSpace)
        {
        }

        public override IGH_Goo Duplicate()
        {
            return new GooSystemSpace(Value);
        }

        public override bool CastFrom(object source)
        {
            return base.CastFrom(source);
        }

        public override bool CastTo<Y>(ref Y target)
        {
            return base.CastTo(ref target);
        }

        public override string TypeName
        {
            get
            {
                return Value == null ? typeof(ISystemSpace).Name : Value.GetType().Name;
            }
        }
    }

    public class GooSystemSpaceParam : GH_PersistentParam<GooSystemSpace>
    {
        public override Guid ComponentGuid => new Guid("1ea2ed4a-1fd0-44ff-a4cb-a910aefe2fc9");

        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_Space;

        public override GH_Exposure Exposure => GH_Exposure.hidden;

        public GooSystemSpaceParam()
            : base(typeof(ISystemSpace).Name, typeof(ISystemSpace).Name, typeof(ISystemSpace).FullName.Replace(".", " "), "Params", "SAM")
        {
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GooSystemSpace> values)
        {
            throw new NotImplementedException();
        }

        protected override GH_GetterResult Prompt_Singular(ref GooSystemSpace value)
        {
            throw new NotImplementedException();
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Bake By Type", Menu_BakeByPanelType, Resources.SAM3_0, VolatileData.AllData(true).Any());
            Menu_AppendItem(menu, "Save As...", Menu_SaveAs, Resources.SAM3_0, VolatileData.AllData(true).Any());

            //Menu_AppendSeparator(menu);

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