// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Text.Json.Nodes;
using SAM.Core;
using SAM.Core.Systems;
using SAM.Geometry.Planar;

namespace SAM.Geometry.Systems
{
    public class DisplaySystemPlantRoom : SystemPlantRoom
    {
        public DisplaySystemPlantRoom(DisplaySystemPlantRoom displaySystemPlantRoom)
            :base(displaySystemPlantRoom)
        {

        }

        public DisplaySystemPlantRoom(JsonObject jObject)
            : base(jObject)
        {

        }

        public DisplaySystemPlantRoom(string name)
            : base(name)
        {

        }

        /// <summary>
        /// Creates the connection between two components. When both components are display objects (carry
        /// laid-out geometry), the connection is materialised as a <see cref="DisplaySystemConnection"/> whose
        /// polyline is routed orthogonally between the two components' connector world points, so the link draws
        /// in the viewport. For non-display components the logical base behaviour is kept.
        /// </summary>
        protected override ISystemConnection CreateSystemConnection(ISystemComponent systemComponent_1, ISystemComponent systemComponent_2, ISystem system = null, int index_1 = -1, int index_2 = -1)
        {
            if(systemComponent_1 == null || systemComponent_2 == null)
            {
                return null;
            }

            if(!(systemComponent_1 is IDisplaySystemObject<SystemGeometryInstance> displaySystemObject_1) || !(systemComponent_2 is IDisplaySystemObject<SystemGeometryInstance> displaySystemObject_2))
            {
                return base.CreateSystemConnection(systemComponent_1, systemComponent_2, system, index_1, index_2);
            }

            SystemType systemType = system == null ? null : new SystemType(system);

            int index_1_Temp = index_1;
            int index_2_Temp = index_2;

            // Resolve the connector indexes (out of component 1, into component 2). Explicit indexes are honoured;
            // when an index is missing, pick the closest unconnected Out/In pair using the laid-out display
            // geometry (Query.TryGetIndexes returns connector list positions aligned with GetPoint2D).
            if (systemType != null && (index_1_Temp == -1 || index_2_Temp == -1))
            {
                if (Query.TryGetIndexes(this, systemComponent_1, systemComponent_2, out int index_1_Resolved, out int index_2_Resolved, systemType, Direction.Out))
                {
                    if (index_1_Temp == -1)
                    {
                        index_1_Temp = index_1_Resolved;
                    }

                    if (index_2_Temp == -1)
                    {
                        index_2_Temp = index_2_Resolved;
                    }
                }
            }

            SystemGeometryInstance systemGeometryInstance_1 = displaySystemObject_1.SystemGeometry;
            SystemGeometryInstance systemGeometryInstance_2 = displaySystemObject_2.SystemGeometry;

            Point2D point2D_1 = index_1_Temp == -1 ? null : systemGeometryInstance_1?.GetPoint2D(index_1_Temp);
            Point2D point2D_2 = index_2_Temp == -1 ? null : systemGeometryInstance_2?.GetPoint2D(index_2_Temp);

            // Fall back to the component centroid when a connector point cannot be resolved, so the link is still
            // drawn (the plan's graceful-degradation behaviour) rather than left invisible.
            if (point2D_1 == null)
            {
                point2D_1 = Centroid(displaySystemObject_1);
            }

            if (point2D_2 == null)
            {
                point2D_2 = Centroid(displaySystemObject_2);
            }

            SystemConnection systemConnection = systemType != null && systemType.IsValid() && index_1_Temp != -1 && index_2_Temp != -1
                ? new SystemConnection(systemType, systemComponent_1, index_1_Temp, systemComponent_2, index_2_Temp)
                : new SystemConnection(systemComponent_1, systemComponent_2);

            Point2D[] point2Ds = Route(point2D_1, point2D_2);
            if (point2Ds == null)
            {
                // No geometry to route: keep the logical connection so the components stay linked.
                return systemConnection;
            }

            return new DisplaySystemConnection(systemConnection, point2Ds);
        }

        /// <summary>
        /// Builds an orthogonal (manhattan) route between two connector points: a straight segment when they
        /// already share an axis, otherwise a two-bend route through the horizontal mid-point.
        /// </summary>
        private static Point2D[] Route(Point2D point2D_1, Point2D point2D_2)
        {
            if (point2D_1 == null || point2D_2 == null)
            {
                return null;
            }

            const double tolerance = 1e-6;
            if (Math.Abs(point2D_1.X - point2D_2.X) < tolerance || Math.Abs(point2D_1.Y - point2D_2.Y) < tolerance)
            {
                return new Point2D[] { point2D_1, point2D_2 };
            }

            double midX = (point2D_1.X + point2D_2.X) / 2.0;
            return new Point2D[] { point2D_1, new Point2D(midX, point2D_1.Y), new Point2D(midX, point2D_2.Y), point2D_2 };
        }

        private static Point2D Centroid(IDisplaySystemObject displaySystemObject)
        {
            return displaySystemObject?.BoundingBox2D?.GetCentroid();
        }
    }
}
