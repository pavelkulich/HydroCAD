using System;
using System.Collections.Generic;
using System.Linq;

namespace RailCAD.Models.Alignment
{
    /// <summary>
    /// RailCAD axis class (collection of alingment segments: lines, curves and turnouts).
    /// </summary>
    public class RCAxis
    {
        public String name;
        public String userName;
        public double startKm;
        public double endKm;
        public double length;
        public double segmCount;
        public string polyline; // polyline for calculation of stationing
        public List<string> segments = new List<string>();

        public RCAxis(String name, String userName, double startKm)
        {
            this.name = name;
            this.userName = userName;
            this.startKm = startKm;
        }

        public RCAxis(String name, String userName, double startKm, List<string> segments)
        {
            this.name = name;
            this.userName = userName;
            this.startKm = startKm;
            this.segments = segments;
            this.segmCount = segments.Count;
        }

        /// <summary>
        /// Constructor of the axis (collection of curves, lines and turnouts) object.
        /// </summary>
        /// <param name="name">Name of axis (e.g. RC_T# - number is current date and time)</param>
        /// <param name="userName">User name of axis</param>
        /// <param name="startKm">Start kilometer of axis</param>
        /// <param name="endKm">End kilometer of axis</param>
        /// <param name="polyline">Handle of the axis polyline used for calculation of stationing</param>
        /// <param name="segments">List of axis segments handles (curves, lines and turnouts)</param>
        public RCAxis(String name, String userName, double startKm, double endKm, string polyline, List<string> segments)
        {
            this.name = name;
            this.userName = userName;
            this.startKm = startKm;
            this.endKm = endKm;
            this.polyline = polyline;
            this.segments = segments;
            this.segmCount = segments.Count;
        }
    }
}
