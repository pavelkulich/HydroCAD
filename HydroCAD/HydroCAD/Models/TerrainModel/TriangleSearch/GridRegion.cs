using System.Collections.Generic;
using HydroCAD.Models.Geometry;

namespace HydroCAD.Models.TerrainModel.TriangleSearch
{
    internal class GridRegion
    {
        public List<HCTriangle> Triangles { get; } = new List<HCTriangle>();
    }
}
