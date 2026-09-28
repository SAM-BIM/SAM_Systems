using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SAM.Core.Systems;
using SAM.Core.Grasshopper;
using SAM.Analytical.Grasshopper.Systems.Properties;
using Rhino;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class GooSystemComponent : GooJSAMObject<ISystemComponent>
    {
        public GooSystemComponent()
            : base()
        {
        }

        public GooSystemComponent(ISystemComponent systemComponent)
            : base(systemComponent)
        {
        }

        public override IGH_Goo Duplicate()
        {
            return new GooSystemComponent(Value);
        }

        public override string TypeName
        {
            get
            {
                return Value == null ? typeof(ISystemComponent).Name : Value.GetType().Name;
            }
        }
    }

    public class GooSystemComponentParam : GH_PersistentParam<GooSystemComponent>
    {
        public override Guid ComponentGuid => new Guid("2d34ab26-ac12-4ba4-984c-50cdfd7ae237");

        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_Ahu;

        public GooSystemComponentParam()
            : base(typeof(ISystemComponent).Name, typeof(ISystemComponent).Name, typeof(ISystemComponent).FullName.Replace(".", " "), "Params", "SAM")
        {
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GooSystemComponent> values)
        {
            throw new NotImplementedException();
        }

        protected override GH_GetterResult Prompt_Singular(ref GooSystemComponent value)
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