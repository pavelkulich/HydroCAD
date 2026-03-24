namespace RailCAD.CadInterface.Tools
{
    internal class XDataAppNames
    {
        // numbered
        public const string RC_D = "RC_D";  // terrain model + number

        // constant
        public const string RC_SING = "RC_SING";  // fixed point (terrain model)
        public const string RC_BOD = "RC_BOD";  // numbered point
        public const string RC_SPOJNICE = "RC_SPOJNICE";  // segment (connecting line) bewteen 2 points
        public const string RC_SMER = "RC_SMER";  // horizontal curve data
        public const string RCAD = "RCAD";  // horizontal curve additional data
        public const string RC_TRASA = "RC_TRASA";  // horizontal curve data
        public const string RC_ENOBL = "RC_ENOBL";  // horizontal curve entities
        public const string VYHYBKA = "VYHYBKA";  // turnout (main entity) data
        public const string RC_ASSBOD = "RC_ASSBOD";  // link to curve or axis associated with points
        public const string RC_BODINFO = "RC_BODINFO";  // point data for height calculation
        public const string RC_TRAMPROFIL = "RC_TRAMPROFIL";  // tram profiles in curve
    }
}
