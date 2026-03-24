using System.Collections.Generic;
using System.Linq;
using HydroCAD.Models.Geometry;

namespace HydroCAD.Models.Network
{
    /// <summary>
    /// Represents an ordered sequence of pipe segments forming a single route (branch).
    /// The route is defined by a CAD polyline handle + a list of pipes.
    /// </summary>
    public class HCPipeRoute
    {
        public HCPipeRoute(string id, HCPolyline centreline, string handle = "0")
        {
            Id = id;
            Centreline = centreline;
            Handle = handle;
            Pipes = new List<HCPipe>();
            Manholes = new List<HCManhole>();
        }

        /// <summary>Route identifier.</summary>
        public string Id { get; set; }

        /// <summary>CAD polyline representing the pipe route centreline.</summary>
        public HCPolyline Centreline { get; set; }

        /// <summary>CAD entity handle of the polyline.</summary>
        public string Handle { get; set; }

        /// <summary>Ordered list of pipe segments along this route.</summary>
        public List<HCPipe> Pipes { get; private set; }

        /// <summary>Ordered list of manholes along this route (including start and end).</summary>
        public List<HCManhole> Manholes { get; private set; }

        /// <summary>Total route length (sum of pipe lengths).</summary>
        public double TotalLength => Pipes.Sum(p => p.Length);

        /// <summary>Starting invert level of the route.</summary>
        public double StartInvertLevel => Manholes.FirstOrDefault()?.InvertLevel ?? double.NaN;

        /// <summary>Ending invert level of the route.</summary>
        public double EndInvertLevel => Manholes.LastOrDefault()?.InvertLevel ?? double.NaN;

        public void AddPipe(HCPipe pipe) => Pipes.Add(pipe);
        public void AddManhole(HCManhole manhole) => Manholes.Add(manhole);

        public override string ToString() =>
            $"Route {Id}: {Pipes.Count} pipes, L={TotalLength:F2}m";
    }
}
