using System;
using System.Collections.Generic;
using System.Linq;
using HydroCAD.Models.Network;

namespace HydroCAD.Profile
{
    /// <summary>
    /// Complete longitudinal profile dataset for one pipe route.
    /// Contains all sampled stations and drawing parameters.
    /// </summary>
    public class ProfileData
    {
        public ProfileData(string routeId)
        {
            RouteId = routeId;
            Stations = new List<ProfileStation>();
        }

        public string RouteId { get; set; }

        /// <summary>Ordered stations from start to end of the route.</summary>
        public List<ProfileStation> Stations { get; private set; }

        /// <summary>Total route length (m).</summary>
        public double TotalLength => Stations.Any() ? Stations.Last().Chainage : 0;

        // Drawing parameters
        /// <summary>Horizontal scale for the profile drawing (e.g. 500 means 1:500).</summary>
        public double HorizontalScale { get; set; } = 500;

        /// <summary>Vertical scale for the profile drawing (e.g. 100 means 1:100).</summary>
        public double VerticalScale { get; set; } = 100;

        /// <summary>Sampling interval along the route (m).</summary>
        public double SamplingInterval { get; set; } = 5.0;

        /// <summary>Minimum cover depth required (m).</summary>
        public double MinCoverDepth { get; set; } = 0.8;

        /// <summary>Pipe type description for labels.</summary>
        public PipeType PipeType { get; set; } = PipeType.GravitySewer;

        // Derived statistics
        public double MinGroundLevel => Stations.Where(s => !double.IsNaN(s.GroundLevel)).Min(s => s.GroundLevel);
        public double MaxGroundLevel => Stations.Where(s => !double.IsNaN(s.GroundLevel)).Max(s => s.GroundLevel);
        public double MinInvertLevel => Stations.Where(s => !double.IsNaN(s.InvertLevel)).Min(s => s.InvertLevel);
        public double MaxInvertLevel => Stations.Where(s => !double.IsNaN(s.InvertLevel)).Max(s => s.InvertLevel);
        public double MinCoverDepth_Actual => Stations.Where(s => !double.IsNaN(s.CoverDepth)).Min(s => s.CoverDepth);

        /// <summary>Returns the station at or nearest to the given chainage.</summary>
        public ProfileStation GetNearestStation(double chainage)
        {
            return Stations.OrderBy(s => Math.Abs(s.Chainage - chainage)).FirstOrDefault();
        }

        /// <summary>Returns only manhole stations.</summary>
        public IEnumerable<ProfileStation> ManholeStations => Stations.Where(s => s.IsManhole);
    }
}
