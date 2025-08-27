using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public class MapNavigationComponent : MonoBehaviour
    {
        [SerializeField] private LineRenderer _pathRenderer;
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _cellMask;
        [SerializeField] private SelectedCellComponent _selectedCell;
        [SerializeField] private SelectedCellComponent _pathfindVisualizationCell;

        private bool _initialized;
        private Player _player;
        private Path _path;
        private Pathfinder _pathfinder;
        private MapComponent _mapComponent;

        public async Task Init(Player player, MapComponent mapComponent)
        {
            _mapComponent = mapComponent;
            _player = player;
            
            _path = new Path();
            _path.Updated += UpdateFullPath;
            _path.BuildStarted += PathOnBuildStarted;
            _path.PointPushed += PathOnPointPushed;
            _path.PointPopped += PathOnPointPopped;
            
            _selectedCell.Init();
            _pathfindVisualizationCell.Init();
            _player.SetPath(_path);
            _pathfinder = new Pathfinder(mapComponent, _path, destroyCancellationToken);
            _initialized = true;
            await Task.CompletedTask;
        }

        private void PathOnPointPopped()
        {
            _pathRenderer.positionCount--;
        }

        private void PathOnPointPushed(float3 pos)
        {
            _pathRenderer.positionCount++;
            pos.y = _pathRenderer.transform.position.y;
            _pathRenderer.SetPosition(_pathRenderer.positionCount - 1, pos);
        }
        
        private void UpdateFullPath()
        {
            _pathRenderer.positionCount = _path.Count;
            for (var index = 0; index < _path.Count; index++)
            {
                var pos = _path[index];
                pos.y = _pathRenderer.transform.position.y;
                _pathRenderer.SetPosition(index, pos);
            }
        }

        private void PathOnBuildStarted()
        {
            _pathRenderer.positionCount = 0;
        }

        private void Update()
        {
            if (!_initialized) return;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out var hit, 100f, _cellMask))
            {
                var cell = _mapComponent.WorldToCell(hit.point);

                var state = SelectedCellComponent.State.ValidSelection;
                if (!_mapComponent.MapData.Contains(cell) || !_mapComponent.MapData.GetTile(cell).IsPassable)
                    state = SelectedCellComponent.State.InvalidSelection;

                var cellCenter = _mapComponent.CellToWorld(cell);
                _selectedCell.Refresh(cellCenter, state);

                if (Input.GetMouseButtonDown(0) && state == SelectedCellComponent.State.ValidSelection)
                {
                    FindPath(cell);
                    // _player.ResetPathDistance();
                }
            }
        }
        
        

        private async void FindPath(Vector2Int cell)
        {
            // _pathfindVisualizationCell.gameObject.SetActive(true);
            
            var searchTask = await _pathfinder.RequestSearchAsync(_mapComponent.WorldToCell(_player.transform.position), cell);
            _player.ResetPathDistance();
            
            // searchTask.OnNodeInspected += node =>
            // {
            //     _pathfindVisualizationCell.Refresh(_mapComponent.CellToWorld(node),
            //         SelectedCellComponent.State.SearchingPath);
            // };
            //
            // searchTask.OnSearchEnded += state =>
            // {
            //     _pathfindVisualizationCell.gameObject.SetActive(false);
            // };
        }
    }
}
