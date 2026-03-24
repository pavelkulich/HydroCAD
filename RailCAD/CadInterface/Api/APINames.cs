namespace RailCAD.CadInterface.Api
{
    internal class APINames
    {
        public const string BODY_SMAZ = "BODY_SMAZ";  // Deletes every N-th selected numbered point in a drawing.
        public const string BODYIN = "BODYIN";  // Import points from a text file to a drawing.
        public const string RCLICENCE = "RCLICENCE";  // (RCLICENCE "REGENERATE") Enables users to renew RailCAD licence.
        public const string SESTAV_MODEL = "SESTAV_MODEL";  // terrain model: triangulate
        public const string SMAZ_MODEL = "SMAZ_MODEL";  // terrain model: delete
        public const string TRAMPROFIL = "TRAMPROFIL:O";  // tram vehicle profile in curve
        public const string VZDALENOST_KRIVEK = "VZDALENOST_KRIVEK";  // shortest distance between 2 polylines

        public const string RCLIC = "RCLIC";  // railcad licence check
        public const string CSPTH = "cspth";  // terrain model: points heights
        public const string GENTRAMPROFILE = "GENTRAMPROFILE";  // draw vehicle profile in curve
        public const string DEFTANGREACT = "DEFTANGREACT";  // definition of the curve tangent modification reactor
        public const string LOADREACTORS = "LOADREACTORS";  // loads reactors from XRecords
        public const string FREEPOINTSNUMBERS = "FREEPOINTSNUMBERS";  // get list of all points numbers in drawing
    }
}
