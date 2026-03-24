namespace HydroCAD.Models.Geometry
{
    public class PolylineVertex
    {
        public Point2d Point { get; set; }
        public double Bulge { get; set; }

        public PolylineVertex(Point2d point, double bulge = 0.0)
        {
            Point = point;
            Bulge = bulge;
        }
    }
}
