using RailCAD.Models.TerrainModel;

namespace RailCAD.Services.Triangulation
{
    /// <summary>
    /// Common inteface for triangulation library, in case some other library is used in the future.
    /// </summary>
    public interface ITriangulator
    {
        TerrainModel Triangulate(bool considerTriangleAreaForNormalss);
    }
}
