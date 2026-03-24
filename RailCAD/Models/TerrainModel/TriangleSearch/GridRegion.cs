using System.Collections.Generic;

using RailCAD.Models.Geometry;

namespace RailCAD.Models.TerrainModel
{
    internal class GridRegion
    {
        private IList<RCTriangle> triangles;   // referencing triangles

        //public GridRegion(double minX, double maxX, double minY, double maxY) { }
        public GridRegion()
        {
            this.triangles = new List<RCTriangle>();
        }

        public void AddTriangle(RCTriangle triangle) 
        {
            triangles.Add(triangle);
        }

        public IList<RCTriangle> Triangles
        {
            get { return this.triangles; }
        }
    }
}
