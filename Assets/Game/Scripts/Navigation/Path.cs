using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Splines;

namespace Game.Scripts.Navigation
{
    /// <summary>
    /// Wrapper around inner <see cref="Spline"/>
    /// Represents path for player to follow.
    /// </summary>
    public class Path
    {
        private static class Profiling
        {
            // public static ProfilerMarker PushPoint = new("Path.PushPoint");
            public static ProfilerMarker Evaluate = new("Path.Evaluate");
            public static ProfilerMarker SetPoints = new("Path.SetPoints");
            public static ProfilerMarker GetLength = new("Path.GetLength");
        }
        
        private NativeSpline _spline;
        
        public event Action Updated;

        // public event Action BuildStarted;
        // public event Action<float3> PointPushed;
        // public event Action PointPopped;
        
        public Path()
        {
            _spline = new NativeSpline();
        }

        ~Path()
        {
            _spline.Dispose();
        }
        
        public float Length
        {
            get
            {
                Profiling.GetLength.Begin();
                var result = _spline.GetLength();
                Profiling.GetLength.End();
                return result;
            }
        }

        public int Count => _spline.Count;

        public float3 this[int index] => _spline[index].Position;
        
        // public void Clear()
        // {
        //     // _spline.Clear();
        //     Updated?.Invoke();
        // }

        public void SetPoints(IReadOnlyList<BezierKnot> points)
        {
            if (_isBuilding)
                throw new Exception($"Path is building, {nameof(SetPoints)} call is permitted");
            
            // _spline.AddRange(points, TangentMode.Linear);
            _spline.Dispose();
            Profiling.SetPoints.Begin();
            _spline = new NativeSpline(points, false, float4x4.identity, Allocator.Persistent);
            Profiling.SetPoints.End();
            Updated?.Invoke();
        }

        private bool _isBuilding;

        // public void StartBuilding()
        // {
        //     if (_isBuilding)
        //         throw new Exception("Path is already building.");
        //     
        //     _isBuilding = true;
        //     Clear();
        //     BuildStarted?.Invoke();
        // }
        //
        // public void StopBuilding()
        // {
        //     if (!_isBuilding)
        //         throw new Exception("Path is not building");
        //     
        //     _isBuilding = false;
        //     Updated?.Invoke();
        // }
        //
        // public void PushPoint(float3 point)
        // {
        //     if (!_isBuilding)
        //         throw new Exception($"Path is not building, {nameof(PushPoint)} call is permitted");
        //  
        //     Profiling.PushPoint.Begin();
        //     _spline.Add(point, TangentMode.Linear);
        //     PointPushed?.Invoke(point);
        //     Profiling.PushPoint.End();
        // }

        // public void PopPoint()
        // {
        //     if (!_isBuilding)
        //         throw new Exception($"Path is not building, {nameof(PopPoint)} call is permitted");
        //     
        //     _spline.RemoveAt(_spline.Count - 1);
        //     PointPopped?.Invoke();
        // }
        
        public float NormalizedTimeToDistance(float t) => Length * t;
        
        public float DistanceToNormalizedTime(float distance)
        {
            var result = math.unlerp(0, Length, distance);
            return result;
        }

        public bool Evaluate(float t, out float3 position, out float3 tangent, out float3 upVector)
        {
            Profiling.Evaluate.Begin();
            var result = _spline.Evaluate(t, out position, out tangent, out upVector);
            Profiling.Evaluate.End();
            return result;
        }
        public bool EvaluateAtDistance(float distance, out float3 position, out float3 tangent, out float3 upVector)
        {
            return Evaluate(DistanceToNormalizedTime(distance), out position, out tangent, out upVector);
        }
    }
}