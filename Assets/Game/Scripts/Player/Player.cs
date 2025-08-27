using Game.Scripts.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts
{
    /// <summary>
    /// Currently just a path follower
    /// Uses distance to move along the <see cref="Path"/>
    /// </summary>
    public class Player : MonoBehaviour
    {
        [SerializeField] private Transform _model;
        [SerializeField] private float _movementSpeed = 3f;   
        [SerializeField] private float _rotationLerp = 6f;
        
        private Path _path;
        private float _distance;
        
        public void SetPath(Path path)
        {
            _path = path;
            ResetPathDistance();
        }

        public void ResetPathDistance()
        {
            _distance = 0f;
        }

        private void Update()
        {
            if (_path == null) return;
            
            EvaluatePosition(_distance);
            
            _distance += _movementSpeed * Time.deltaTime;
            _distance = Mathf.Clamp(_distance, 0f, _path.Length);
        }

        private void EvaluatePosition(float distance)
        {
            _path.EvaluateAtDistance(distance, out var position, out var tangent, out var up);
            transform.position = position;
            var dir = (Vector3)tangent;
            if (dir.sqrMagnitude > 0)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized, up);
                var t = _rotationLerp * Time.deltaTime;
                _model.rotation = Quaternion.Slerp(_model.rotation, targetRot, t);
            }
        }
    }
}
