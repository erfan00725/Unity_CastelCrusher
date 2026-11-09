using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Core
{
    public class CatmullRom
    {
        // Samples the curve through `controlPoints` (>= 2) into a dense polyline.
        public static List<Vector3> Sample(IReadOnlyList<Vector3> controlPoints, int samplesPerSegment)
        {
            List<Vector3> results = new List<Vector3>();
            
            if (controlPoints == null || controlPoints.Count < 2)
            {
                return results;
            }

            if (samplesPerSegment < 1)
            {
                samplesPerSegment = 1;
            }

            int count = controlPoints.Count;

            for (int i = 0; i < count - 1; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(0, i - 1)];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = controlPoints[Mathf.Min(count - 1, i + 2)];

                for (int j = 0; j < samplesPerSegment; j++)
                {
                    float t = j / (float)samplesPerSegment;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    
                    Vector3 p = 0.5f * (
                        (2f * p1) +
                        (-p0 + p2) * t +
                        (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                        (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                    );
                    results.Add(p);
                }

            }
            
            results.Add(controlPoints[count - 1]);

            return results;
        }
    }

    public class PolylinePath
    {
        private Vector3[] _points;
        private float[] _cumDist;
        public float TotalLength { get; }
        
        public PolylinePath(IReadOnlyList<Vector3> points)
        {
            int count = points.Count;
            _points = new Vector3[count];
            _cumDist = new float[count];

            _points[0] = points[0];
            _cumDist[0] = 0f;

            for (int i = 1; i < count; i++)
            {
                _points[i] = points[i];
                _cumDist[i] = _cumDist[i - 1] + Vector3.Distance(_points[i - 1], _points[i]);
            }

            TotalLength = _cumDist[count - 1];
        }
        
        public Vector3 Evaluate(float t)
        {
            if (TotalLength <= 0)
            {
                return _points[0];
            }
            
            t = Mathf.Clamp01(t);
            float target = t * TotalLength;

            int i = 0;
            while (i < _cumDist.Length - 2 && _cumDist[i + 1] < target)
            {
                i++;
            }

            float segmentLen = _cumDist[i + 1] - _cumDist[i];

            if (segmentLen <= 0)
            {
                return _points[i];
            }

            float local = (target - _cumDist[i]) / segmentLen;

            return Vector3.Lerp(_points[i], _points[i+1], local);
        }   // t in [0,1], constant speed by arc l ength
        
        // A -> B -> C 
    }
}