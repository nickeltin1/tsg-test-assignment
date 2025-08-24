using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private float _movementSpeed = 3f;
        [SerializeField] private float _rotationSpeed = 6f; // how fast the boat turns
        [SerializeField] private float _arriveSqrThreshold = 0.01f;
        [SerializeField] private Transform _model;

        private readonly List<MapData.Tile> _path = new();
        private Grid _grid;
        private int _pathIndex = -1;

        public void SetPath(List<MapData.Tile> path, Grid grid)
        {
            _grid = grid;

            _path.Clear();
            if (path != null && path.Count > 0)
                _path.AddRange(path);

            _pathIndex = _path.Count > 0 ? 0 : -1;
        }

        public void Stop()
        {
            _path.Clear();
            _pathIndex = -1;
        }

        private void Update()
        {
            if (_pathIndex < 0 || _pathIndex >= _path.Count || _grid == null)
                return;

            var tile = _path[_pathIndex];
            var cell = new Vector3Int(tile.X, -tile.Y, 0);
            var target = _grid.GetCellCenterWorld(cell);

            var pos = transform.position;

            // movement
            if ((pos - target).sqrMagnitude <= _arriveSqrThreshold)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    _pathIndex = -1; // finished
                }
                return;
            }

            Vector3 dir = (target - pos).normalized;

            // move
            transform.position = Vector3.MoveTowards(
                pos,
                target,
                _movementSpeed * Time.deltaTime);

            // rotate smoothly toward direction of travel (keep upright on Y)
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                _model.rotation = Quaternion.Slerp(
                    _model.rotation,
                    targetRot,
                    _rotationSpeed * Time.deltaTime);
            }
        }
    }
}
