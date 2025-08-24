using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public class MapNavigationComponent : MonoBehaviour
    {
        [SerializeField] private Grid _grid;
        [SerializeField] private LineRenderer _pathRenderer;
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _cellMask;
        [SerializeField] private SelectedCellComponent _selectedCell;

        private bool _initialized;
        private Player _player;
        private MapData _map;

        public async Task Init(Player player, MapData map)
        {
            _map = map;
            _player = player;
            _initialized = true;
            _selectedCell.Init();
            await Task.CompletedTask;
        }

        private void Update()
        {
            if (!_initialized) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, _cellMask))
            {
                var cell = _grid.WorldToCell(hit.point);
                var mapPos = new Vector2Int(cell.x, -cell.y);

                var state = SelectedCellComponent.State.ValidSelection;
                if (!_map.Contains(mapPos) || !_map.GetTile(mapPos).IsPassable)
                    state = SelectedCellComponent.State.InvalidSelection;

                _selectedCell.Refresh(_grid.GetCellCenterWorld(cell), state);

                if (Input.GetMouseButtonDown(0) && state == SelectedCellComponent.State.ValidSelection)
                {
                    // Determine player's current map cell
                    var pCell = _grid.WorldToCell(_player.transform.position);
                    var pMap = new Vector2Int(pCell.x, -pCell.y);
                    Debug.Log($"Finding path from {pCell} to {pMap}");
                    var path = HexPathfinding.FindPath(_map, pMap, mapPos);
                    Debug.Log($"Path from {pCell} to {pMap} found {path.Count}");
                    DrawPath(path);
                    _player.SetPath(path, _grid);
                }
            }
        }

        private void DrawPath(List<MapData.Tile> path)
        {
            if (path == null || path.Count == 0)
            {
                _pathRenderer.positionCount = 0;
                return;
            }

            _pathRenderer.positionCount = path.Count;
            for (var i = 0; i < path.Count; i++)
            {
                var tile = path[i];
                var cell = new Vector3Int(tile.X, -tile.Y, 0);
                var pos = _grid.GetCellCenterWorld(cell);
                pos.y = _pathRenderer.transform.position.y;
                _pathRenderer.SetPosition(i, pos);
            }
        }
    }
}
