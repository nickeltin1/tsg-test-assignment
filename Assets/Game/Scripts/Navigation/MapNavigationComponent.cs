using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public class MapNavigationComponent : MonoBehaviour
    {
        [SerializeField] private LineRenderer _pathRenderer;
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _cellMask;
        [SerializeField] private SelectedCellComponent _selectedCell;

        private bool _initialized;
        private Player _player;
        private MapData _map;
        private Path _path;
        private Pathfinder _pathfinder;
        private MapComponent _mapComponent;

        public async Task Init(Player player, MapData map, MapComponent mapComponent)
        {
            _mapComponent = mapComponent;
            _map = map;
            _player = player;
            _initialized = true;
            _path = new Path();
            _path.Updated += UpdatePathPreview;
            _selectedCell.Init();
            _player.SetPath(_path);
            _pathfinder = new Pathfinder(_map, mapComponent, _path);
            await Task.CompletedTask;
        }

        private void Update()
        {
            if (!_initialized) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, _cellMask))
            {
                var cell = _mapComponent.WorldToCell(hit.point);

                var state = SelectedCellComponent.State.ValidSelection;
                if (!_map.Contains(cell) || !_map.GetTile(cell).IsPassable)
                    state = SelectedCellComponent.State.InvalidSelection;

                var cellCenter = _mapComponent.CellToWorld(cell);
                _selectedCell.Refresh(cellCenter, state);

                if (Input.GetMouseButtonDown(0) && state == SelectedCellComponent.State.ValidSelection)
                {
                    _player.ResetPathDistance();
                    _pathfinder.RequestSearch(_mapComponent.WorldToCell(_player.transform.position), cell);
                }
            }
        }

        private void UpdatePathPreview()
        {
            _pathRenderer.positionCount = _path.Count;
            for (var i = 0; i < _path.Count; i++)
            {
                var pos = _path[i];
                pos.y = _pathRenderer.transform.position.y;
                _pathRenderer.SetPosition(i, pos);
            }
        }
    }
}
