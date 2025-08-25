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

        public event Action BuildStarted;
        public event Action<float3> PointPushed;
        public event Action PointPopped;
        
        public Path()
        {
            _spline = new Spline();
        }
        
        public float Length => _spline.GetLength();
        public int Count => _spline.Count;

        public float3 this[int index] => _spline[index].Position;
        
        public void Clear()
        {
            _spline.Clear();
            Updated?.Invoke();
        }

        public void SetPoints(IEnumerable<float3> points)
        {
            if (_isBuilding)
                throw new Exception($"Path is building, {nameof(SetPoints)} call is permitted");
            
            _spline.AddRange(points, TangentMode.Linear);
            Updated?.Invoke();
        }

        private bool _isBuilding;

        public void StartBuilding()
        {
            if (_isBuilding)
                throw new Exception("Path is already building.");
            
            _isBuilding = true;
            Clear();
            BuildStarted?.Invoke();
        }

        public void StopBuilding()
        {
            if (!_isBuilding)
                throw new Exception("Path is not building");
            
            _isBuilding = false;
            Updated?.Invoke();
        }
        
        public void PushPoint(float3 point)
        {
            if (!_isBuilding)
                throw new Exception($"Path is not building, {nameof(PushPoint)} call is permitted");
            
            _spline.Add(point, TangentMode.Linear);
            PointPushed?.Invoke(point);
        }

        public void PopPoint()
        {
            if (!_isBuilding)
                throw new Exception($"Path is not building, {nameof(PopPoint)} call is permitted");
            
            _spline.RemoveAt(_spline.Count - 1);
            PointPopped?.Invoke();
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