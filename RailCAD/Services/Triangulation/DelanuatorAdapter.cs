using System;
using System.Collections.Generic;
using System.Linq;

using DelaunatorSharp;
using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;

namespace RailCAD.Services.Triangulation
{
    public class DelanuatorAdapter : ITriangulator
    {
        private IList<RCPoint> points;
        private IList<RCLine> segments;
        private IList<IPoint> dPoints;
        private IList<Tuple<IPoint, IPoint>> dSegments;
        private Delaunator delaunator;

        public DelanuatorAdapter(IList<RCPoint> points, IList<RCLine> segments=null)
        {
            this.points = points;
            this.segments = segments;

            if (points != null)
            {
                this.dPoints = new List<IPoint>(points.Count);
                for (int i = 0; i < points.Count; i++)
                {
                    // todo: pevne body (#83)
                    if (points[i].Type == RC_BOD.ZAKLAD)
                    {
                        Point2d point2d = points[i].Point2d;
                        dPoints.Insert(i, new Point(point2d.X, point2d.Y));  // implicit vertex id obtained from the order of insertion
                    }
                }
            }
            if (segments != null)
            {
                this.dSegments = new List<Tuple<IPoint, IPoint>>(segments.Count);
                foreach (RCLine segment in segments)
                {
                    if (RC_SPOJNICE.POSSIBLE_TYPES_FOR_DEFINITION.HasFlag(segment.Type))
                    {
                        Point2d pt1 = segment.Pt1.Point2d;
                        Point2d pt2 = segment.Pt2.Point2d;
                        dSegments.Add(new Tuple<IPoint, IPoint>(new Point(pt1.X, pt1.Y), new Point(pt2.X, pt2.Y)));
                    }
                }
            }
        }

        public TerrainModel Triangulate(bool considerTriangleAreaForNormals)
        {
            // simple input validation
            if (!(dPoints.Count > 2)) return null;

            try
            {
                this.delaunator = new Delaunator(this.dPoints.ToArray());
                if (this.dSegments != null && this.dSegments.Count > 0)
                {
                    this.delaunator = new ConstrainedDelaunator(this.dPoints.ToArray(), this.dSegments);
                }
            }
            catch (Exception)
            {
                // no triangulation exists or it cannot be created
                // cad.WriteMessage(ex.ToString());
                return null;
            }

            // create triangulation model
            return new TerrainModel(this.points, this.CreateRCLines(), this.CreateRCTriangles(), considerTriangleAreaForNormals);
        }

        private IList<RCLine> CreateRCLines()
        {
            HashSet<RCLine> rcLines = new HashSet<RCLine>(); // hashset: ensure no two lines are in the same position

            // add selected fixed segments
            if (this.segments != null)
            {
                foreach (RCLine segment in this.segments)
                {
                    rcLines.Add(segment);
                }
            }

            if (this.delaunator != null)
            {
                // note: working with point indices to reference original RCPoint list and avoid creation of new objects (-> cannot use GetHullEdges)
                // create hull edges first
                for (int i = 0; i < this.delaunator.Hull.Length; i++)
                {
                    int inx0 = this.delaunator.Hull[i];
                    int inx1 = (i < this.delaunator.Hull.Length - 1) ? this.delaunator.Hull[i + 1] : this.delaunator.Hull[0];

                    rcLines.Add(new RCLine(this.points[inx0], this.points[inx1], RC_SPOJNICE.HRANICE));

                    // inform hranice points
                    this.points[inx0].SetIsHranice();
                    this.points[inx1].SetIsHranice();
                }

                // add all triangle edges, hull edges should not be added again as they are already contained in the list
                for (int i = 0; i < this.delaunator.Triangles.Length; i += 3)
                {
                    var triPts = new List<RCPoint>()
                    {
                        this.points[this.delaunator.Triangles[i]],
                        this.points[this.delaunator.Triangles[i+1]],
                        this.points[this.delaunator.Triangles[i+2]],
                    };

                    rcLines.Add(new RCLine(triPts[0], triPts[1], RC_SPOJNICE.TRIANG));
                    rcLines.Add(new RCLine(triPts[1], triPts[2], RC_SPOJNICE.TRIANG));
                    rcLines.Add(new RCLine(triPts[2], triPts[0], RC_SPOJNICE.TRIANG));
                }
            }
            return rcLines.ToList();
        }

        private IList<RCTriangle> CreateRCTriangles()
        {
            IList<RCTriangle> rcTriangles = null;
            if (this.delaunator != null)
            {
                rcTriangles = new List<RCTriangle>(this.delaunator.Triangles.Length / 3);

                int id = 0;
                for (int i = 0; i < this.delaunator.Triangles.Length; i += 3)
                {
                    // triangle points from Delaunator are orineted clockwise (GeometryHelper.Orientation)
                    var triPts = new List<RCPoint>()
                    {
                        this.points[this.delaunator.Triangles[i]],
                        this.points[this.delaunator.Triangles[i+1]],
                        this.points[this.delaunator.Triangles[i+2]],
                    };

                    rcTriangles.Add(new RCTriangle(++id, triPts));
                }
            }
            return rcTriangles;
        }
    }
}
