// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Core.Grasshopper.Systems.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Core.Grasshopper.Systems
{
    public class GooSystem : GooJSAMObject<Core.Systems.ISystem>
    {
        public GooSystem()
            : base()
        {
        }

        public GooSystem(Core.Systems.ISystem system)
            : base(system)
        {
        }

        public override IGH_Goo Duplicate()
        {
            return new GooSystem(Value);
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
                return Value == null ? typeof(Core.Systems.ISystem).Name : Value.GetType().Name;
            }
        }
    }

    public class GooSystemParam : GH_PersistentParam<GooSystem>
    {
        public override Guid ComponentGuid => new Guid("bceb3ae6-4542-42c3-8930-46982b2ab3c4");

        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_System;

        public override GH_Exposure Exposure => GH_Exposure.hidden;

        public GooSystemParam()
            : base(typeof(Core.Systems.ISystem).Name, typeof(Core.Systems.ISystem).Name, typeof(Core.Systems.ISystem).FullName.Replace(".", " "), "Params", "SAM")
        {
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GooSystem> values)
        {
            throw new NotImplementedException();
        }

        protected override GH_GetterResult Prompt_Singular(ref GooSystem value)
        {
            throw new NotImplementedException();
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Save As...", Menu_SaveAs, Resources.SAM3_0, VolatileData.AllData(true).Any());

            //Menu_AppendSeparator(menu);

            base.AppendAdditionalMenuItems(menu);
        }

        private void Menu_SaveAs(object sender, EventArgs e)
        {
            Query.SaveAs(VolatileData);
        }
    }
}