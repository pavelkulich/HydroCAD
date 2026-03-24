using HydroCAD.Models.Geometry;

namespace HydroCAD.Models.TerrainModel.TriangleSearch
{
    internal interface ITriangleSearchStrategy
    {
        HCTriangle FindTriangle(Point2d point);
    }
}
