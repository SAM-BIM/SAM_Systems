using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SAM.Core.Grasshopper.Systems.Properties;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Core.Grasshopper.Systems
{
    public class GooSystemResult : GooJSAMObject<ISystemResult>
    {
        public GooSystemResult()
            : base()
        {
        }

        public GooSystemResult(ISystemResult systemResult)
            : base(systemResult)
        {
        }

        public override IGH_Goo Duplicate()
        {
            return new GooSystemResult(Value);
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
                return Value == null ? typeof(ISystemResult).Name : Value.GetType().Name;
            }
        }
    }

    public class GooSystemResultParam : GH_PersistentParam<GooSystemResult>
    {
        public override Guid ComponentGuid => new Guid("ab9deb98-f591-469b-8b47-aa69df35000f");

        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_Result;

        public override GH_Exposure Exposure => GH_Exposure.hidden;

        public GooSystemResultParam()
            : base(typeof(ISystemResult).Name, typeof(ISystemResult).Name, typeof(ISystemResult).FullName.Replace(".", " "), "Params", "SAM")
        {
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GooSystemResult> values)
        {
            throw new NotImplementedException();
        }

        protected override GH_GetterResult Prompt_Singular(ref GooSystemResult value)
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