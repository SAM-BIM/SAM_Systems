﻿using SAM.Geometry.Object.Planar;
using SAM.Geometry.Systems;

namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        public static ISAMGeometry2DObject SAMGeometry2Dobject(IDisplaySystemObject displaySystemObject)
        {
            if(displaySystemObject == null)
            {
                return null;
            }

            if (displaySystemObject is IDisplaySystemObject<SystemGeometryInstance>)
            {
                IDisplaySystemObject<SystemGeometryInstance> displaySystemObject_Temp = displaySystemObject as IDisplaySystemObject<SystemGeometryInstance>;
                SystemGeometryInstance systemGeometryInstance = displaySystemObject_Temp.SystemGeometry;
                return systemGeometryInstance.GetGeometry();
            }

            if(displaySystemObject is IDisplaySystemObject<SystemPolyline>)
            {
                IDisplaySystemObject<SystemPolyline> displaySystemObject_Temp = displaySystemObject as IDisplaySystemObject<SystemPolyline>;
                SystemPolyline systemPolyline = displaySystemObject_Temp.SystemGeometry;
                return systemPolyline;
            }

            if (displaySystemObject is IDisplaySystemObject<SystemPolygon>)
            {
                IDisplaySystemObject<SystemPolygon> displaySystemObject_Temp = displaySystemObject as IDisplaySystemObject<SystemPolygon>;
                SystemPolygon systemPolygon = displaySystemObject_Temp.SystemGeometry;
                return systemPolygon;
            }

            return null;
        }
    }
}
