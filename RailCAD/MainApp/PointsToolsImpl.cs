using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RailCAD.CadInterface;
using RailCAD.CadInterface.Tools;
using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;
using RailCAD.Views;

namespace RailCAD.MainApp
{
    partial class RCApp
    {
        private static void DeletePointsPercentage(ICadModel cad)
        {
            // Select points using dialog
            IList<RCPoint> points = cad.SelectPoints();
            if (points == null)
                return;

            cad.DeletePointsPercentage(points);
        }

        private static void ImportPnts(ICadModel cad)
        {
            // Import points using dialog
            cad.ImportPoints();
        }

        private static void DeleteTerrainModel(ICadModel cad)
        {
            cad.DeleteTerrainModel();
        }

        private static void GetFreePointsNumbers(ICadModel cad)
        {
            IList<RCPoint> allPoints = cad.SelectAllPoints();
            if (allPoints == null) return;
            IList<int> numbers = RCPoint.GetPointsNumbers(allPoints);
            IList<int> freeNumbers = GetFreePointsNumbers(numbers);

            cad.SetLispResp(ResBufIO.WritePointNumbersResp, freeNumbers);
        }

        /// <summary>
        /// Creates list of free points numbers
        /// </summary>
        /// <param name="source">List of points</param>
        /// <returns>List of points in ascending order that are not yet taken.</returns>
        private static IList<int> GetFreePointsNumbers(IEnumerable<int> source)
        {
            if (source == null || source.Count() == 0) return null;

            int min = source.Min();
            int max = source.Max() + 1;
            var set = new HashSet<int>(source);
            var missing = new List<int>(max - min);

            for (int i = min; i <= max; i++)
            {
                if (!set.Contains(i))
                    missing.Add(i);
            }

            if (missing.Count > 0)
                return missing;
            return null;
        }
    }
}
