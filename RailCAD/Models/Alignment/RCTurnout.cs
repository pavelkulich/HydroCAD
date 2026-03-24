using System;
using System.Collections.Generic;
using System.Linq;

using RailCAD.Models.Geometry;

namespace RailCAD.Models.Alignment
{
    /// <summary>
    /// RailCAD turnout class.
    /// </summary>
    public class RCTurnout
    {
        public string Handle; // main turnout entity
        public String Shape;
        public String Rail;
        public String Angle;
        public double Radius;
        public String DriverSide;
        public String SleeperMaterial;
        public String Type;
        public String Number;
        public Point3d[] EndPoints = new Point3d[4]; // 4 end points (turnout: ZV BO KV1 KV2/ crossing: KV1 KV2 KV3 KV4)
        public String Transf;
        public String Note;
        public String PotSleeper; // zlabovy prazec
        public String AddedInfo;
        public String MainBranch; // "KV3"/ "KV4"
        public double Cant; // not yet used
        public String LockType;
        public String Fastening;
        public String FrogType;
        public String LabelRadius;
        public string Switch; // line or null (crossings)
        public List<string> Arms = new List<string>(); // 2 or 4 lines
        public List<string> EndLines = new List<string>(); // 2 or 4 lines
        public List<string> Driver = new List<string>(); // (prestavnik) line and block or null (crossings)
        public List<string> Label = new List<string>(); // 1 or more texts
        public List<RCCurve> Curves = new List<RCCurve>(); // 1 or 2 curves

        public RCTurnout(string handle)
        {
            this.Handle = handle;
        }

        public RCTurnout(String number, String Shape, Point3d[] pnts)
        {
            this.Number = number;
            this.Shape = Shape;
            this.EndPoints = pnts;
        }

        /// <summary>
        /// Constructor of turnout object.
        /// </summary>
        /// <param name="number">Number of turnout</param>
        /// <param name="Shape">Shape of turnout (basic "J", symetric "S", crossing turnout "C", crossing "K", half crossing turnout "B") </param>
        /// <param name="pnts">End points of turnout (normal turnout: start, turning point, two end points; crossing: 4 end points)</param>
        /// <param name="handle">Handle of the main turnout entity (circle)</param>
        public RCTurnout(String number, String Shape, Point3d[] pnts, string handle)
        {
            this.Number = number;
            this.Shape = Shape;
            this.Handle = handle;
            this.EndPoints = pnts;
        }

        /// <summary>
        /// Returns an array of turnout visualization entities.
        /// </summary>
        public List<string> GetEntities()
        {
            List<string> ents = new List<string>();
            if (this.Switch != null)
            {
                ents.Add(this.Switch);
            }
            if (this.Arms != null)
            {
                ents.AddRange(this.Arms);
            }
            if (this.EndLines != null)
            {
                ents.AddRange(this.EndLines);
            }
            if (this.Driver != null)
            {
                ents.AddRange(this.Driver);
            }
            if (this.Label != null)
            {
                ents.AddRange(this.Label);
            }
            return ents;
        }

        /// <summary>
        /// Returns a dictionary of ObjectIds and coresponding turnouts (objects).
        /// </summary>
        public static Dictionary<string, RCTurnout> GetCollection(List<RCTurnout> turnouts)
        {
            if (turnouts == null && turnouts.Count == 0)
            {
                return null;
            }
            Dictionary <string, RCTurnout> dict = turnouts.ToDictionary(p => p.Handle);
            return dict;
        }

        /// <summary>
        /// Finds turnout (object) by its main entity ObjectId in an array of turnouts.
        /// </summary>
        public static RCTurnout GetTurnout(List<RCTurnout> turnouts, string ent)
        {
            if (turnouts == null && turnouts.Count == 0)
            {
                return null;
            }
            Dictionary<string, RCTurnout> dict = turnouts.ToDictionary(p => p.Handle);
            return dict[ent];
        }
    }
}
