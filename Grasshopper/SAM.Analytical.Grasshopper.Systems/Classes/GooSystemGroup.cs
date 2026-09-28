using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper.Systems.Properties;
using SAM.Core.Grasshopper;
using SAM.Core.Systems;
using SAM.Geometry.Object.Planar;
using SAM.Geometry.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SAM.Geometry.Planar;

namespace SAM.Analytical.Grasshopper.Systems
{
    public class GooSystemGroup : GooJSAMObject<ISystemGroup>, IGH_PreviewData, IGH_BakeAwareData
    {
        public GooSystemGroup()
            : base()
        {
        }

        public GooSystemGroup(ISystemGroup systemGroup)
            : base(systemGroup)
        {
        }

        public override IGH_Goo Duplicate()
        {
            return new GooSystemGroup(Value);
        }

        public override bool CastFrom(object source)
        {
            object @object = source;
            if(@object is IGH_Goo)
            {
                @object = (@object as dynamic).Value;
            }

            if(@object is ISystemGroup)
            {
                Value = (ISystemGroup)@object;
                return true;
            }

            return base.CastFrom(@object);
        }

        public override bool CastTo<Y>(ref Y target)
        {
            return base.CastTo(ref target);
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            ISAMGeometry2DObject sAMGeometry2DObject = null;

            if(Value is IDisplaySystemObject)
            {
                sAMGeometry2DObject = Analytical.Systems.Query.SAMGeometry2Dobject((IDisplaySystemObject)Value);
            }

            if(sAMGeometry2DObject != null)
            {
                Geometry.Grasshopper.Modify.DrawViewportWires(sAMGeometry2DObject, args, args.Color);
            }
        }

        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {

        }

        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            return Modify.BakeGeometry(Value, doc, att, out obj_guid);
        }

        public override string TypeName
        {
            get
            {
                return Value == null ? typeof(ISystemObject).Name : Value.GetType().Name;
            }
        }

        public BoundingBox ClippingBox
        {
            get
            {
                if (Value == null)
                {
                    return BoundingBox.Empty;
                }

                IDisplaySystemObject displaySystemObject = Value as IDisplaySystemObject;
                if(displaySystemObject == null)
                {
                    return BoundingBox.Empty;
                }

                ISAMGeometry2DObject sAMGeometry2DObject = Analytical.Systems.Query.SAMGeometry2Dobject(displaySystemObject);

                List<BoundingBox2D> boundingBox2Ds = Geometry.Object.Convert.ToSAM_ISAMGeometry(sAMGeometry2DObject)?.FindAll(x => x is IBoundable2D).ConvertAll(x => ((IBoundable2D)x).GetBoundingBox());
                if(boundingBox2Ds == null || boundingBox2Ds.Count == 0)
                {
                    return BoundingBox.Empty;
                }

                BoundingBox2D boundingBox2D = new BoundingBox2D(boundingBox2Ds);

                Geometry.Spatial.Plane plane = Geometry.Spatial.Plane.WorldXY;

                return Geometry.Rhino.Convert.ToRhino(new Geometry.Spatial.BoundingBox3D(Geometry.Spatial.Query.Convert(plane, boundingBox2D.Min), Geometry.Spatial.Query.Convert(plane, boundingBox2D.Max)));
            }
        }
    }

    public class GooSystemGroupParam : GH_PersistentParam<GooSystemGroup>, IGH_PreviewObject, IGH_BakeAwareObject
    {
        public override Guid ComponentGuid => new Guid("3243e174-d434-45bf-bc75-0854c3bb7735");

        protected override System.Drawing.Bitmap Icon => Resources.SAM_GH_SystemPlural;

        public bool Hidden { get; set; }

        //public override GH_Exposure Exposure => GH_Exposure.hidden;

        public bool IsPreviewCapable => !VolatileData.IsEmpty;

        public BoundingBox ClippingBox => Preview_ComputeClippingBox();

        public bool IsBakeCapable => true;

        public GooSystemGroupParam()
            : base(typeof(ISystemGroup).Name, typeof(ISystemGroup).Name, typeof(ISystemGroup).FullName.Replace(".", " "), "Params", "SAM")
        {
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GooSystemGroup> values)
        {
            throw new NotImplementedException();
        }

        protected override GH_GetterResult Prompt_Singular(ref GooSystemGroup value)
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

        public void DrawViewportWires(IGH_PreviewArgs args)
        {
            Preview_DrawWires(args);
        }

        public void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            Preview_DrawMeshes(args);
        }

        public void BakeGeometry(RhinoDoc doc, List<Guid> obj_ids)
        {
            BakeGeometry(doc, doc.CreateDefaultAttributes(), obj_ids);
        }

        public void BakeGeometry(RhinoDoc doc, ObjectAttributes att, List<Guid> obj_ids)
        {
            foreach (var value in VolatileData.AllData(true))
            {
                Guid uuid = default;
                (value as IGH_BakeAwareData)?.BakeGeometry(doc, att, out uuid);
                obj_ids.Add(uuid);
            }
        }
    }
}