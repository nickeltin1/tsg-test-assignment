using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts
{
    public partial class MapStreamerComponent : MonoBehaviour
    {
        private struct TileInstance
        {
            public MapData.Tile MapTile;
            public GameObject Main;
            public AsyncGameObjectPool MainPool;
            public GameObject Decoration;
            public AsyncGameObjectPool DecorationPool;
        }
        
        [SerializeField] private Grid _grid;

        [SerializeField] private Transform _waterParent;
        [SerializeField] private Transform _groundParent;
        [SerializeField] private Transform _decorParent;
        
        [Tooltip("In tiles")]
        [SerializeField] private int _spawnRadius = 30;

        private bool _inited;
        
        private MapData _map;
        private AddressableAssets.Assets _assets;
        private Transform _player;

        
        private AsyncGameObjectPoolCollection _waterPool;
        private AsyncGameObjectPoolCollection _groundPool;
        private AsyncGameObjectPoolCollection _decorationsPool;
        
        private Task _loop;
        
        
        private RectInt _currentStreamArea;
        private RectInt _lastStreamArea;
        private List<RectInt> _addedAreas;
        private List<RectInt> _removedAreas;

        public void Init(MapData map, AddressableAssets.Assets assets, Transform player)
        {
            _map = map;
            _assets = assets;
            _player = player;

            _waterPool = _assets.WaterTiles.CreateAsyncPools(_waterParent);
            _groundPool = _assets.GroundTiles.CreateAsyncPools(_groundParent);
            _decorationsPool = _assets.Decorations.CreateAsyncPools(_decorParent);
            
            _addedAreas = new List<RectInt>();
            _removedAreas = new List<RectInt>();

            _inited = true;
            // _loop = StreamLoop();
        }

        private void Update()
        {
            if (!_inited) return;
            
            // Here we have a difference between rects, that determines what tiles should be loaded and unloaded
            _lastStreamArea = _currentStreamArea;
            var playerCellPosition = _grid.WorldToCell(_player.position);
            _currentStreamArea = MapStreamingMath.CalculateActiveArea(new Vector2Int(playerCellPosition.x, -playerCellPosition.y), _spawnRadius, _map.Width, _map.Height); 
            MapStreamingMath.CalculateAreaDifference(_lastStreamArea, _currentStreamArea, _addedAreas, _removedAreas);
        }

        // private async Task StreamLoop()
        // {
        //     while (enabled && gameObject.activeInHierarchy)
        //     {
        //         await StepAsync();
        //         await Task.Yield();
        //     }
        // }
        //
        // private async Task StepAsync()
        // {
        //    
        // }
    }
}