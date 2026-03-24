using System;
using System.Collections.Generic;

namespace DelaunatorSharp
{
    public class ConstrainedDelaunator : Delaunator
    {
        private const int MAX_FLIPS_PER_SEGMENT = 100;
        private const int MAX_REFINING_ITERATIONS = 2;

        public ConstrainedDelaunator(IPoint[] points, IList<Tuple<IPoint, IPoint>> segments)
            : base(points)
        {
            // base: create regular delaunay triangulation
            // + modify the triangulation by adding the constraints
            AddConstraints(segments);
        }

        private void AddConstraints(IList<Tuple<IPoint, IPoint>> segments)
        {
            // flipping triangles for latter segments does not check intersections in the previous segments
            // therefore new intersections may occur (e.g. when two constrain segments are near to each other)
            // to mitigate this issue, multiple iterations of adding constraints are done
            for (int iteration = 1; iteration <= MAX_REFINING_ITERATIONS; iteration++)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    // Let each constrained edge be defined by the vertices:
                    var c_p1 = segments[i].Item1;
                    var c_p2 = segments[i].Item2;

                    // Check if this constraint already exists in the triangulation, if so we are happy and dont need to worry about this edge
                    if (IsEdgeInListOfEdges(base.GetEdges(), c_p1, c_p2))
                    {
                        continue;
                    }

                    // Step 2. Find all edges in the current triangulation that intersects with this constraint
                    // Is returning unique edges only, so not one edge going in the opposite direction
                    Queue<IEdge> intersectingEdges = FindIntersectingEdges_BruteForce(base.GetEdges(), c_p1, c_p2);
                    //Console.WriteLine($"Intersecting edges: {intersectingEdges.Count}");

                    // Step 3. Remove intersecting edges by flipping triangles
                    RemoveIntersectingEdgesAndRestoreDelaunayTriangulation(c_p1, c_p2, intersectingEdges);

                    // Step 4. Try to restore delaunay triangulation (Because we have constraints we will never get a delaunay triangulation)
                }

                // Step 5. Remove superfluous triangles, such as the triangles "inside" the constraints (n/a: holes not supported)
            }
        }

        private void RemoveIntersectingEdgesAndRestoreDelaunayTriangulation(IPoint v_i, IPoint v_j, Queue<IEdge> intersectingEdges)
        {
            //While some edges still cross the constrained edge, do steps 3.1 and 3.2
            int counter = 0;
            while (intersectingEdges.Count > 0)
            {
                if (counter++ > MAX_FLIPS_PER_SEGMENT) return;  // infinite loop check

                //Step 3.1. Remove an edge from the list of edges that intersects the constrained edge
                IEdge e = intersectingEdges.Dequeue();

                ////The vertices belonging to the two triangles
                IPoint v_k = e.P;
                IPoint v_l = e.Q;
                IPoint v_3rd = Points[Triangles[PreviousHalfedge(e.Index)]];

                if (Halfedges[e.Index] == -1)
                    continue;

                IPoint v_opposite_pos = Points[Triangles[PreviousHalfedge(Halfedges[e.Index])]];  // The vertex belonging to the opposite triangle and isn't shared by the current edge

                bool canFlip = Geometry.IsQuadrilateralConvex(v_k, v_l, v_3rd, v_opposite_pos);
                
                if (!canFlip)
                {
                    intersectingEdges.Enqueue(e);
                    continue;
                }
                else
                {
                    //Flip the edge like we did when we created the delaunay triangulation
                    //Step 3.2. If the two triangles don't form a convex quadtrilateral
                    //place the edge back on the list of intersecting edges (because this edge cant be flipped) and go to step 3.1
                    IEdge newEdge = this.FlipTriangleEdge(e.Index);

                    //The new diagonal is defined by the vertices
                    IPoint v_m = newEdge.P;
                    IPoint v_n = newEdge.Q;

                    // If this new diagonal intersects with the constrained edge, add it to the list of intersecting edges
                    if (IsEdgeCrossingEdge(v_i, v_j, v_m, v_n))
                    {
                        intersectingEdges.Enqueue(e);
                    }
                }
            }
        }

        protected IEdge FlipTriangleEdge(int a)
        {
            // Modification of method Delaunator::Legalize()
            // No Delaunaly criterion check, just flip
            /* flip for the new pair of triangles
            *
            *           pl                    pl
            *          /||\                  /  \
            *       al/ || \bl            al/    \a
            *        /  ||  \              /      \
            *       /  a||b  \    flip    /___ar___\
            *     p0\   ||   /p1   =>   p0\---bl---/p1
            *        \  ||  /              \      /
            *       ar\ || /br             b\    /br
            *          \||/                  \  /
            *           pr                    pr
            */
            var b = Halfedges[a];

            int a0 = a - a % 3;
            var ar = a0 + (a + 2) % 3;

            var b0 = b - b % 3;
            var bl = b0 + (b + 2) % 3;

            var p0 = Triangles[ar];
            var p1 = Triangles[bl];

            Triangles[a] = p1;
            Triangles[b] = p0;

            bool illegal = true;

            if (illegal)
            {
                var hbl = Halfedges[bl];

                // edge swapped on the other side of the hull (rare); fix the halfedge reference
                if (hbl == -1)
                {
                    Console.WriteLine("rare swap");
                    //var e = hullStart;
                    //do
                    //{
                    //    if (hullTri[e] == bl)
                    //    {
                    //        hullTri[e] = a;
                    //        break;
                    //    }
                    //    e = hullPrev[e];
                    //} while (e != hullStart);
                }
                Link(a, hbl);
                Link(b, Halfedges[ar]);
                Link(ar, bl);
            }


            // return new edge
            var e = a;  // id of new edge is id of old edge
            var p = Points[Triangles[e]];
            var q = Points[Triangles[NextHalfedge(e)]];
            return new Edge(a, p, q);
        }

        private void Link(int a, int b)
        {
            Halfedges[a] = b;
            if (b != -1) Halfedges[b] = a;
        }

        // Find edges that intersect with a constraint - Method 1. Brute force by testing all unique edges
        // Find all edges of the current triangulation that intersects with the constraint edge between p1 and p2
        private static Queue<IEdge> FindIntersectingEdges_BruteForce(IEnumerable<IEdge> edges, IPoint c_p1, IPoint c_p2)
        {
            //Should be in a queue because we will later plop the first in the queue and add edges in the back of the queue 
            Queue<IEdge> intersectingEdges = new Queue<IEdge>();

            //We also need to make sure that we are only adding unique edges to the queue
            //In the half-edge data structure we have an edge going in the opposite direction
            //and we only need to add an edge going in one direction
            HashSet<IEdge> edgesInQueue = new HashSet<IEdge>();

            //Loop through all edges and see if they are intersecting with the constrained edge
            foreach (IEdge e in edges)
            {
                IPoint e_p2 = e.Q;  //The position the edge is going to
                IPoint e_p1 = e.P;  //The position the edge is coming from

                //Has this edge been added, but in the opposite direction?
                if (edgesInQueue.Contains(new Edge(0, e_p2, e_p1)))  // note: implements Edge.Equals() and subsequently Point.Equals()
                {
                    continue;
                }

                //Is this edge intersecting with the constraint?
                if (IsEdgeCrossingEdge(e_p1, e_p2, c_p1, c_p2))
                {
                    //If so add it to the queue of edges
                    intersectingEdges.Enqueue(e);
                    edgesInQueue.Add(e);
                }
            }

            return intersectingEdges;
        }

        //Is an edge (between p1 and p2) in a list with edges
        private static bool IsEdgeInListOfEdges(IEnumerable<IEdge> edges, IPoint p1, IPoint p2)
        {
            foreach (IEdge e in edges)
            {
                //The vertices positions of the current triangle
                IPoint e_p2 = e.P;
                IPoint e_p1 = e.Q;

                //Check if edge has the same coordinates as the constrained edge
                //We have no idea about direction so we have to check both directions
                //This is fast because we only need to test one coordinate and if that 
                //coordinate doesn't match the edges can't be the same
                //We can't use a dictionary because we flip edges constantly so it would have to change?
                if (AreTwoEdgesTheSame(p1, p2, e_p1, e_p2))
                {
                    return true;
                }
            }

            return false;
        }

        //Are two edges the same edge?
        private static bool AreTwoEdgesTheSame(IPoint e1_p1, IPoint e1_p2, IPoint e2_p1, IPoint e2_p2)
        {
            //Is e1_p1 part of this constraint?
            if ((e1_p1.Equals(e2_p1) || e1_p1.Equals(e2_p2)))  // note: implements Point.Equals()
            {
                //Is e1_p2 part of this constraint?
                if ((e1_p2.Equals(e2_p1) || e1_p2.Equals(e2_p2)))
                {
                    return true;
                }
            }

            return false;
        }

        //Is an edge crossing another edge? 
        private static bool IsEdgeCrossingEdge(IPoint e1_p1, IPoint e1_p2, IPoint e2_p1, IPoint e2_p2)
        {
            //We will here run into floating point precision issues so we have to be careful
            //To solve that you can first check the end points 
            //and modify the line-line intersection algorithm to include a small epsilon

            //First check if the edges are sharing a point, if so they are not crossing
            if (e1_p1.Equals(e2_p1) || e1_p1.Equals(e2_p2) || e1_p2.Equals(e2_p1) || e1_p2.Equals(e2_p2))   // note: implements Point.Equals()
            {
                return false;
            }

            //Then check if the lines are intersecting
            if (!Intersections.LineLine(new Edge(0, e1_p1, e1_p2), new Edge(0, e2_p1, e2_p2), includeEndPoints: false))
            {
                return false;
            }

            return true;
        }
    }
}
