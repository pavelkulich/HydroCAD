using System;
using System.Linq;
using HydroCAD.Models.Geometry;

namespace HydroCAD.Models.TerrainModel.TriangleSearch
{
    /// <summary>
    /// Divides terrain into a grid for faster triangle lookup.
    /// Falls back to NaiveStrategy when the region search fails.
    /// </summary>
    internal class GridStrategy : ITriangleSearchStrategy
    {
        private readonly TerrainModel terrainModel;
        private readonly ITriangleSearchStrategy fallback;
        private readonly GridRegion[,] regions;
        private readonly int no_regions;
        private readonly double minX, maxX, minY, maxY, rangeX, rangeY;

        public GridStrategy(TerrainModel terrainModel)
        {
            this.terrainModel = terrainModel;
            this.fallback = new NaiveStrategy(terrainModel.Triangles);

            no_regions = (int)Math.Ceiling(Math.Sqrt(terrainModel.Points.Count()) / 5);
            no_regions = Math.Max(no_regions, 1);

            // bounding box
            minX = double.MaxValue; maxX = double.MinValue;
            minY = double.MaxValue; maxY = double.MinValue;
            foreach (var point in terrainModel.Points2d)
            {
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }
            double tol = 1e-6;
            minX -= tol; maxX += tol; minY -= tol; maxY += tol;
            rangeX = maxX - minX;
            rangeY = maxY - minY;

            // initialize grid
            regions = new GridRegion[no_regions, no_regions];
            for (int x = 0; x < no_regions; x++)
                for (int y = 0; y < no_regions; y++)
                    regions[x, y] = new GridRegion();

            // assign triangles to regions by their vertices
            foreach (var triangle in terrainModel.Triangles)
                foreach (var point in triangle.Points)
                    LocateRegion(point.Point2d)?.Triangles.Add(triangle);
        }

        private GridRegion LocateRegion(Point2d point)
        {
            int x = (int)((point.X - minX) / rangeX * no_regions);
            int y = (int)((point.Y - minY) / rangeY * no_regions);
            if (x < 0 || x >= no_regions || y < 0 || y >= no_regions) return null;
            return regions[x, y];
        }

        public HCTriangle FindTriangle(Point2d point)
        {
            GridRegion region = LocateRegion(point);
            if (region != null)
            {
                foreach (var triangle in region.Triangles)
                    if (triangle.Contains2D(point)) return triangle;
            }
            return fallback.FindTriangle(point);
        }
    }
}
