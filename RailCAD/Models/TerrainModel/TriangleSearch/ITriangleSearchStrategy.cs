using RailCAD.Models.Geometry;

namespace RailCAD.Models.TerrainModel
{
    public interface ITriangleSearchStrategy
    {
        RCTriangle FindTriangle(Point2d point);
    }
}
