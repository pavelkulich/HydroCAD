using System;
using System.Linq;

using RailCAD.CadInterface;
using RailCAD.Models.Geometry;


namespace RailCAD.Models.TerrainModel
{
    /// <summary>
    /// Divides points into regions for faster localization of triangle subset to search in.
    /// In some cases, such as triangle in between more regions or triangle larger than region, may not converge.
    /// Thus, the fallback strategy is provided.
    /// </summary>
    internal class GridStrategy : ITriangleSearchStrategy
    {
        private TerrainModel terrainModel;
        private ITriangleSearchStrategy fallback;
        private GridRegion[,] regions;
        private int no_regions;
        private double minX;
        private double maxX;
        private double minY;
        private double maxY;
        private double rangeX;
        private double rangeY;

        public int used_fallbacks;

        public GridStrategy(TerrainModel terrainModel)
        {
            this.terrainModel = terrainModel;
            this.fallback = new NaiveStrategy(terrainModel);
            this.used_fallbacks = 0;

            // number of grid regions in one direction (X/Y)
            // determine according to the number of points (at least N points per region in each direction when assumed homogenous point distribution)
            this.no_regions = (int)Math.Ceiling(Math.Sqrt(terrainModel.Points.Count()) / 5);
            //this.no_regions = 20;

            this.GenerateRegions();
        }

        private void GenerateRegions()
        {
            this.DetermineBoundingBox();

            // initialize regions
            this.regions = new GridRegion[no_regions, no_regions];
            for (int x = 0; x < no_regions; x++) 
            {
                for (int y = 0; y < no_regions; y++)
                {
                    this.regions[x, y] = new GridRegion();
                }
            }

            CadModel.WriteMessageStatic($"Regions X = {regions.GetLength(0)}");
            CadModel.WriteMessageStatic($"Regions Y = {regions.GetLength(1)}");

            // assign triangles to regions
            foreach (RCTriangle triangle in this.terrainModel.Triangles)
            {
                foreach (RCPoint point in triangle.Points)
                {
                    // consider only vertices of the triangle (triangle edge can cross other regions, which are not considered -> fallback)
                    this.LocateRegion(point.Point2d).AddTriangle(triangle);
                }
            }
        }

        private void DetermineBoundingBox()
        {
            this.minX = Double.MaxValue;
            this.maxX = Double.MinValue;
            this.minY = Double.MaxValue;
            this.maxY = Double.MinValue;

            foreach (Point2d point in this.terrainModel.Points2d)
            {
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }

            double tol = 1e-6;
            minX -= tol;
            maxX += tol;
            minY -= tol;
            maxY += tol;
            this.rangeX = maxX - minX;
            this.rangeY = maxY - minY;

            //CadModel.WriteMessageStatic($"{minX},{maxX}");
        }

        private GridRegion LocateRegion(Point2d point)
        {
            int x = (int)((point.X - minX) / rangeX * no_regions);
            int y = (int)((point.Y - minY) / rangeY * no_regions);
            //CadModel.WriteMessageStatic($"locate region: {x},{y}");
            if (x < 0 || x >= no_regions) return null;
            if (y < 0 || y >= no_regions) return null;
            return this.regions[x, y];
        }

        public RCTriangle FindTriangle(Point2d point)
        {
            // find region and search through its triangles
            GridRegion region = this.LocateRegion(point);
            if (region == null) return null;

            foreach (RCTriangle triangle in region.Triangles)
            {
                if (triangle.Contains2D(point))
                {
                    return triangle;
                }
            }

            used_fallbacks++;
            return this.fallback.FindTriangle(point);
        }
    }
}
