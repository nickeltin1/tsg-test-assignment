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
                var cellPosition = _grid.WorldToCell(hit.point);
                var mapPosition = new Vector2Int(cellPosition.x, -cellPosition.y);
                var cellState = SelectedCellComponent.State.ValidSelection;
                if (!_map.Contains(mapPosition) || !_map.GetTile(mapPosition).IsPassable)
                {
                    cellState = SelectedCellComponent.State.InvalidSelection;
                }
                _selectedCell.Refresh(_grid.GetCellCenterWorld(cellPosition), cellState);
            }
        }
    }
}