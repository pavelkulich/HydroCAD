using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace RailCAD.Tests
{
    internal class Utils
    {
        internal static void AssertEqualsWithTol(double expected, double actual, string message = "", double tolerance = 1e-6)
        {
            Assert.IsTrue(Math.Abs(expected - actual) < tolerance, message);
        }
    }
}
