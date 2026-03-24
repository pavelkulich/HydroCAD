using System.Collections.Generic;
using HydroCAD.Models.Geometry;

namespace HydroCAD.Models.TerrainModel.TriangleSearch
{
    internal class NaiveStrategy : ITriangleSearchStrategy
    {
        private readonly IEnumerable<HCTriangle> triangles;

        public NaiveStrategy(IEnumerable<HCTriangle> triangles)
        {
            this.triangles = triangles;
        }

        public HCTriangle FindTriangle(Point2d point)
        {
            foreach (var triangle in triangles)
            {
                if (triangle.Contains2D(point))
                    return triangle;
            }
            return null;
        }
    }
}
