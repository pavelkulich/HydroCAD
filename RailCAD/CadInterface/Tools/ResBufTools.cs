#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
#endif

namespace RailCAD.CadInterface.Tools
{
    internal class ResBufTools
    {
        internal static void ReadResultBuffer(_AcDb.ResultBuffer resbuf)
        {
            _AcDb.TypedValue[] rvArr = resbuf.AsArray();
            CadModel.WriteMessageStatic($"arr length: {rvArr.Length}");

            foreach (_AcDb.TypedValue tv in rvArr)
            {
                CadModel.WriteMessageStatic($"{tv.Value} (code: {tv.TypeCode})");
            }
        }
    }
}
