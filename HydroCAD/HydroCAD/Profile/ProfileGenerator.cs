using System;
using System.Collections.Generic;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.Network;
using HydroCAD.Models.TerrainModel;

namespace HydroCAD.Profile
{
    /// <summary>
    /// Generates a longitudinal profile (ProfileData) from a pipe route and terrain model.
    /// This class is CAD-independent.
    /// </summary>
    public class ProfileGenerator
    {
        private readonly TerrainModel terrainModel;

        public ProfileGenerator(TerrainModel terrainModel)
        {
            this.terrainModel = terrainModel;
        }

        /// <summary>
        /// Generates profile data by sampling the terrain along the polyline centreline
        /// and calculating pipe invert levels.
        /// </summary>
        /// <param name="route">The pipe route whose centreline is sampled.</param>
        /// <param name="settings">Profile generation settings.</param>
        /// <returns>ProfileData containing one station per sample point.</returns>
        public ProfileData Generate(HCPipeRoute route, ProfileSettings settings)
        {
            if (route?.Centreline == null) return null;

            var profileData = new ProfileData(route.Id)
            {
                HorizontalScale = settings.HorizontalScale,
                VerticalScale = settings.VerticalScale,
                SamplingInterval = settings.SamplingInterval,
                MinCoverDepth = settings.MinCoverDepth,
                PipeType = settings.PipeType,
            };

            // 1. Sample terrain along the centreline
            var samples = route.Centreline.SampleAtInterval(settings.SamplingInterval);

            // Add manhole positions to sample points
            var allSamples = new List<(double station, Point2d point, bool isManhole, string manholeId)>();
            foreach (var (station, point) in samples)
                allSamples.Add((station, point, false, null));

            foreach (var mh in route.Manholes)
            {
                var (closestPt, distAlong) = route.Centreline.GetClosestPointTo(mh.Position);
                allSamples.Add((distAlong, closestPt, true, mh.Id));
            }

            allSamples.Sort((a, b) => a.station.CompareTo(b.station));

            // Remove near-duplicates (within 0.1 m)
            var deduplicated = DeduplicateSamples(allSamples, 0.1);

            // 2. Query terrain heights
            foreach (var sample in deduplicated)
            {
                double groundLevel = terrainModel?.GetPointHeight(sample.point) ?? double.NaN;
                var station = new ProfileStation(sample.station, sample.point)
                {
                    GroundLevel = groundLevel,
                    IsManhole = sample.isManhole,
                    ManholeId = sample.manholeId,
                    PipeDiameter = settings.PipeDiameter,
                };
                profileData.Stations.Add(station);
            }

            // 3. Calculate pipe invert levels
            CalculateInvertLevels(profileData, settings);

            return profileData;
        }

        /// <summary>
        /// Calculates invert levels for all stations using starting level and gradient,
        /// or using cover depth constraint when starting from terrain.
        /// </summary>
        private static void CalculateInvertLevels(ProfileData profileData, ProfileSettings settings)
        {
            if (profileData.Stations.Count == 0) return;

            double startInvert;

            if (!double.IsNaN(settings.StartInvertLevel))
            {
                startInvert = settings.StartInvertLevel;
            }
            else
            {
                // Derive starting invert from ground level and minimum cover
                double firstGroundLevel = profileData.Stations[0].GroundLevel;
                if (double.IsNaN(firstGroundLevel)) return;
                double pipeDiamM = settings.PipeDiameter / 1000.0;
                startInvert = firstGroundLevel - settings.MinCoverDepth - pipeDiamM;
            }

            if (!double.IsNaN(settings.Gradient))
            {
                // Fixed gradient: invert decreases by gradient * distance
                foreach (var station in profileData.Stations)
                {
                    station.InvertLevel = startInvert - settings.Gradient * station.Chainage;
                }
            }
            else
            {
                // Auto-calculate gradient to maintain MinCoverDepth at every station
                // Strategy: use minimum feasible invert (keeping cover >= MinCoverDepth),
                // while also maintaining a minimum slope.
                double totalLength = profileData.TotalLength;
                double pipeDiamM = settings.PipeDiameter / 1000.0;
                double minSlope = settings.MinGradient; // minimum hydraulic gradient (m/m)

                // Pass 1: calculate required invert at each station to satisfy cover constraint
                double[] requiredInverts = new double[profileData.Stations.Count];
                for (int i = 0; i < profileData.Stations.Count; i++)
                {
                    var s = profileData.Stations[i];
                    if (!double.IsNaN(s.GroundLevel))
                        requiredInverts[i] = s.GroundLevel - settings.MinCoverDepth - pipeDiamM;
                    else
                        requiredInverts[i] = startInvert;
                }

                // Pass 2: assign invert levels, starting from startInvert, respecting min slope
                profileData.Stations[0].InvertLevel = Math.Min(startInvert, requiredInverts[0]);

                for (int i = 1; i < profileData.Stations.Count; i++)
                {
                    var prev = profileData.Stations[i - 1];
                    var curr = profileData.Stations[i];
                    double dL = curr.Chainage - prev.Chainage;

                    double invertBySlope = prev.InvertLevel - minSlope * dL;
                    double invertBycover = requiredInverts[i];

                    curr.InvertLevel = Math.Min(invertBySlope, invertBycover);
                }
            }
        }

        private static List<(double station, Point2d point, bool isManhole, string manholeId)>
            DeduplicateSamples(List<(double station, Point2d point, bool isManhole, string manholeId)> samples, double tol)
        {
            var result = new List<(double, Point2d, bool, string)>();
            double lastStation = double.MinValue;

            foreach (var s in samples)
            {
                if (s.station - lastStation < tol && !s.isManhole) continue;
                result.Add(s);
                lastStation = s.station;
            }

            return result;
        }
    }

    /// <summary>
    /// Settings for the profile generation.
    /// </summary>
    public class ProfileSettings
    {
        /// <summary>Pipe internal diameter in mm.</summary>
        public double PipeDiameter { get; set; } = 300;

        public PipeType PipeType { get; set; } = PipeType.GravitySewer;

        /// <summary>Starting pipe invert level (m). NaN = calculate from terrain + MinCoverDepth.</summary>
        public double StartInvertLevel { get; set; } = double.NaN;

        /// <summary>Fixed hydraulic gradient (m/m). NaN = auto-calculate.</summary>
        public double Gradient { get; set; } = double.NaN;

        /// <summary>Minimum hydraulic gradient used when auto-calculating (m/m).</summary>
        public double MinGradient { get; set; } = 0.003; // 3‰

        /// <summary>Minimum cover depth above pipe crown (m).</summary>
        public double MinCoverDepth { get; set; } = 0.8;

        /// <summary>Sampling interval along the route (m).</summary>
        public double SamplingInterval { get; set; } = 5.0;

        /// <summary>Horizontal drawing scale.</summary>
        public double HorizontalScale { get; set; } = 500;

        /// <summary>Vertical drawing scale (amplified vs. horizontal).</summary>
        public double VerticalScale { get; set; } = 100;
    }
}
