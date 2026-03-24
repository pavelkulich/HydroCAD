using RailCAD.Models.Geometry;

namespace RailCAD.Models.TerrainModel
{
    /// <summary>
    /// Naive search through all the triangles
    /// </summary>
    internal class NaiveStrategy : ITriangleSearchStrategy
    {
        private TerrainModel terrainModel;

        public NaiveStrategy(TerrainModel terrainModel)
        {
            this.terrainModel = terrainModel;
        }

        public RCTriangle FindTriangle(Point2d point)
        {
            int i = 0;  // deve
            foreach (RCTriangle triangle in this.terrainModel.Triangles)
            {
                i++;

                if (triangle.Contains2D(point))
                {
                    //cad.WriteMessage($"{i}/{terrainModel.Triangles.Count()}\n");
                    return triangle;
                }
            }
            return null;
        }
    }
}
