﻿using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Core.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Core.Grasshopper.Systems
{
    public class GooSystemSpace : GooJSAMObject<SystemSpace>
    {
        public GooSystemSpace()
            : base()
        {
        }

        public GooSystemSpace(SystemSpace systemSpace)
            : base(systemSpace)
        {
        }

        public override IGH_Goo Duplicate()
        {
            return new GooSystemObject(Value);
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
                return Value == null ? typeof(SystemSpace).Name : Value.GetType().Name;
            }
        }
    }

    public class GooSystemSpaceParam : GH_PersistentParam<GooSystemSpace>
    {
        public override Guid ComponentGuid => new Guid("07d0a492-c278-494b-91ac-497956f677bd");

        protected override System.Drawing.Bitmap Icon => Resources.SAM3_0;

        public GooSystemSpaceParam()
            : base(typeof(SystemSpace).Name, typeof(SystemSpace).Name, typeof(SystemSpace).FullName.Replace(".", " "), "Params", "SAM")
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
            Menu_AppendItem(menu, "Save As...", Menu_SaveAs, VolatileData.AllData(true).Any());

            //Menu_AppendSeparator(menu);

            base.AppendAdditionalMenuItems(menu);
        }

        private void Menu_SaveAs(object sender, EventArgs e)
        {
            Core.Grasshopper.Query.SaveAs(VolatileData);
        }
    }
}