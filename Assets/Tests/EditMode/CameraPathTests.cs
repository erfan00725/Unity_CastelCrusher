using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.EditMode
{
    public class CameraPathTests
    {
        // ---- CatmullRom.Sample -------------------------------------------------

        [Test]
        public void Sample_StartsAtFirstPoint_EndsAtLastPoint()
        {
            var points = new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 1, 0),
            };

            List<Vector3> samples = CatmullRom.Sample(points, 10);

            // (count - 1) segments × samplesPerSegment, plus the final point
            Assert.AreEqual((points.Count - 1) * 10 + 1, samples.Count);
            Assert.AreEqual(points[0], samples[0]);
            Assert.AreEqual(points[points.Count - 1], samples[samples.Count - 1]);
        }

        [Test]
        public void Sample_CollinearPoints_StaysOnTheLine()
        {
            // A straight line is the one case where the exact answer is known:
            // the spline must NOT overshoot it. 4 collinear points 1 unit apart,
            // 10 samples per segment → samples every 0.1 units from 0 to 3.
            var points = new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(2, 0, 0), new Vector3(3, 0, 0),
            };

            List<Vector3> samples = CatmullRom.Sample(points, 10);

            Assert.AreEqual(new Vector3(1.5f, 0, 0), samples[15], "midpoint drifted off the line");
            Assert.AreEqual(2f, samples[20].x, 1e-5f);   // first sample of segment 2
        }

        [Test]
        public void Sample_FewerThanTwoPoints_ReturnsEmpty()
        {
            Assert.AreEqual(0, CatmullRom.Sample(new List<Vector3>(), 10).Count);
            Assert.AreEqual(0, CatmullRom.Sample(new List<Vector3> { Vector3.zero }, 10).Count);
        }

        // ---- PolylinePath ------------------------------------------------------

        [Test]
        public void StraightPath_EndpointsAreExact()
        {
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(10, 0, 0),
            });

            Assert.AreEqual(10f, path.TotalLength, 1e-4f);
            Assert.AreEqual(new Vector3(0, 0, 0), path.Evaluate(0f));
            Assert.AreEqual(new Vector3(10, 0, 0), path.Evaluate(1f));
        }

        [Test]
        public void StraightPath_MidpointIsHalfTheDistance()
        {
            // The arc-length promise: t = 0.5 means "half the DISTANCE travelled",
            // which on a straight 10-unit path is exactly x = 5.
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(10, 0, 0),
            });

            Assert.AreEqual(5f, path.Evaluate(0.5f).x, 1e-4f);
        }

        [Test]
        public void BentPath_MidpointIsHalfTheTotalDistance()
        {
            // Two legs: A→B is 3 units, B→C is 4 units. Half of 7 units = 3.5,
            // which lands 0.5 units past B along the second leg.
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(3, 4, 0),
            });

            Vector3 mid = path.Evaluate(0.5f);
            Assert.AreEqual(3f, mid.x, 1e-4f);
            Assert.AreEqual(0.5f, mid.y, 1e-4f);
        }

        [Test]
        public void EmptyOrSinglePoint_DoesNotThrow()
        {
            // An anchor with no path points must not be able to crash the camera.
            var empty = new PolylinePath(new Vector3[0]);
            Assert.AreEqual(0f, empty.TotalLength);
            Assert.AreEqual(Vector3.zero, empty.Evaluate(0.5f));

            var single = new PolylinePath(new[] { new Vector3(2, 3, 4) });
            Assert.AreEqual(0f, single.TotalLength);
            Assert.AreEqual(new Vector3(2, 3, 4), single.Evaluate(0.5f));
        }
    }
}