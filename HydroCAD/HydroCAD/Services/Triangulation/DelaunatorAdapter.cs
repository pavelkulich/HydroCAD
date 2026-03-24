using System;
using System.Collections.Generic;
using System.Linq;

using DelaunatorSharp;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.TerrainModel;

namespace HydroCAD.Services.Triangulation
{
    public class DelaunatorAdapter : ITriangulator
    {
        private readonly IList<HCPoint> points;
        private readonly IList<HCLine> segments;
        private readonly IList<IPoint> dPoints;
        private readonly IList<Tuple<IPoint, IPoint>> dSegments;
        private Delaunator delaunator;

        public DelaunatorAdapter(IList<HCPoint> points, IList<HCLine> segments = null)
        {
            this.points = points;
            this.segments = segments;

            if (points != null)
            {
                dPoints = new List<IPoint>(points.Count);
                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i].Type == HC_BOD.BASIC)
                    {
                        Point2d p = points[i].Point2d;
                        dPoints.Insert(i, new Point(p.X, p.Y));
                    }
                }
            }

            if (segments != null)
            {
                dSegments = new List<Tuple<IPoint, IPoint>>(segments.Count);
                foreach (HCLine seg in segments)
                {
                    if (HC_SPOJNICE.POSSIBLE_TYPES_FOR_DEFINITION.HasFlag(seg.Type))
                    {
                        Point2d p1 = seg.Pt1.Point2d;
                        Point2d p2 = seg.Pt2.Point2d;
                        dSegments.Add(new Tuple<IPoint, IPoint>(new Point(p1.X, p1.Y), new Point(p2.X, p2.Y)));
                    }
                }
            }
        }

        public TerrainModel Triangulate(bool considerTriangleAreaForNormals)
        {
            if (dPoints == null || dPoints.Count <= 2) return null;

            try
            {
                delaunator = new Delaunator(dPoints.ToArray());
                if (dSegments != null && dSegments.Count > 0)
                    delaunator = new ConstrainedDelaunator(dPoints.ToArray(), dSegments);
            }
            catch (Exception)
            {
                return null;
            }

            return new TerrainModel(points, CreateHCLines(), CreateHCTriangles(), considerTriangleAreaForNormals);
        }

        private IList<HCLine> CreateHCLines()
        {
            var hcLines = new HashSet<HCLine>();

            // fixed segments first
            if (segments != null)
            {
                foreach (HCLine seg in segments)
                {
                    if (HC_SPOJNICE.POSSIBLE_TYPES_FOR_DEFINITION.HasFlag(seg.Type))
                        hcLines.Add(seg);
                }
            }

            // triangulation edges
            foreach (IEdge edge in delaunator.GetEdges())
            {
                int i1 = (int)edge.P.GetHashCode();
                int i2 = (int)edge.Q.GetHashCode();

                if (i1 < 0 || i1 >= points.Count || i2 < 0 || i2 >= points.Count) continue;

                HCPoint pt1 = points[i1];
                HCPoint pt2 = points[i2];

                long key = HCLine.MakeLineKey(pt1.Number, pt2.Number);
                bool isFixed = hcLines.Any(l => HCLine.MakeLineKey(l.Pt1.Number, l.Pt2.Number) == key);

                if (!isFixed)
                    hcLines.Add(HCLine.CreateLineAutoType(pt1, pt2));
            }

            return hcLines.ToList();
        }

        private IList<HCTriangle> CreateHCTriangles()
        {
            var hcTriangles = new List<HCTriangle>();
            int id = 0;

            foreach (ITriangle tri in delaunator.GetTriangles())
            {
                int i0 = (int)tri.A;
                int i1 = (int)tri.B;
                int i2 = (int)tri.C;

                if (i0 < 0 || i0 >= points.Count || i1 < 0 || i1 >= points.Count || i2 < 0 || i2 >= points.Count)
                    continue;

                HCPoint pt0 = points[i0];
                HCPoint pt1 = points[i1];
                HCPoint pt2 = points[i2];

                var oriented = Common.GeometryHelper.OrientClockwise(pt0, pt1, pt2);
                if (oriented != null)
                    hcTriangles.Add(new HCTriangle(++id, oriented));
            }

            return hcTriangles;
        }
    }
}
