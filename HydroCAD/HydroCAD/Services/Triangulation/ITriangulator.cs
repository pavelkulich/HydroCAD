using HydroCAD.Models.TerrainModel;

namespace HydroCAD.Services.Triangulation
{
    public interface ITriangulator
    {
        TerrainModel Triangulate(bool considerTriangleAreaForNormals);
    }
}
