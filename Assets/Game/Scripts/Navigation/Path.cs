using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace Game.Scripts.Navigation
{
    /// <summary>
    /// Wrapper around inner <see cref="Spline"/>
    /// Represents path for player to follow.
    /// </summary>
    public class Path
    {
        private readonly Spline _spline;
        
        public event Action Updated;
        
        public Path()
        {
            _spline = new Spline();
        }
        
        public float Length => _spline.GetLength();
        public int Count => _spline.Count;

        public float3 this[int index] => _spline[index].Position;


        public void Clear() => _spline.Clear();

        public void AddPoints(IEnumerable<float3> points)
        {
            _spline.AddRange(points, TangentMode.Linear);
            Updated?.Invoke();
        }
        
        public void AddPoint(float3 point)
        {
            _spline.Add(point, TangentMode.Linear);
            Updated?.Invoke();
        }

        public float NormalizedTimeToDistance(float t) => Length * t;
        
        public float DistanceToNormalizedTime(float distance) => math.unlerp(0, Length, distance);

        public bool Evaluate(float t, out float3 position, out float3 tangent, out float3 upVector)
        {
            return _spline.Evaluate(t, out position, out tangent, out upVector);
        }
        public bool EvaluateAtDistance(float distance, out float3 position, out float3 tangent, out float3 upVector)
        {
            return Evaluate(DistanceToNormalizedTime(distance), out position, out tangent, out upVector);
        }
    }
}