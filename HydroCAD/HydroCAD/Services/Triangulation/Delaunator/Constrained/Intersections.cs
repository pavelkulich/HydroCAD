namespace DelaunatorSharp
{
    public static class Intersections
    {
        // Are two lines intersecting?
        //http://thirdpartyninjas.com/blog/2008/10/07/line-segment-intersection/
        //Notice that there are more than one way to test if two line segments are intersecting
        //but this is the fastest according to https://www.habrador.com/tutorials/math/5-line-line-intersection/
        public static bool LineLine(Edge a, Edge b, bool includeEndPoints)
        {
            //To avoid floating point precision issues we can use a small value
            double epsilon = 1e-6;

            bool isIntersecting = false;

            double denominator = (b.Q.Y - b.P.Y) * (a.Q.X - a.P.X) - (b.Q.X - b.P.X) * (a.Q.Y - a.P.Y);

            //Make sure the denominator is != 0 (or the lines are parallel)
            if (denominator > 0f + epsilon || denominator < 0f - epsilon)
            {
                double u_a = ((b.Q.X - b.P.X) * (a.P.Y - b.P.Y) - (b.Q.Y - b.P.Y) * (a.P.X - b.P.X)) / denominator;
                double u_b = ((a.Q.X - a.P.X) * (a.P.Y - b.P.Y) - (a.Q.Y - a.P.Y) * (a.P.X - b.P.X)) / denominator;

                //Are the line segments intersecting if the end points are the same
                if (includeEndPoints)
                {
                    //The only difference between endpoints not included is the =, which will never happen so we have to subtract 0 by epsilon
                    double zero = 0f - epsilon;
                    double one = 1f + epsilon;

                    //Are intersecting if u_a and u_b are between 0 and 1 or exactly 0 or 1
                    if (u_a >= zero && u_a <= one && u_b >= zero && u_b <= one)
                    {
                        isIntersecting = true;
                    }
                }
                else
                {
                    double zero = 0f + epsilon;
                    double one = 1f - epsilon;

                    //Are intersecting if u_a and u_b are between 0 and 1
                    if (u_a > zero && u_a < one && u_b > zero && u_b < one)
                    {
                        isIntersecting = true;
                    }
                }

            }

            return isIntersecting;
        }
    }
}
