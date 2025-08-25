using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Scripts
{
    public partial class MapStreamerComponent : MonoBehaviour
    {
        public enum OperationType
        {
            Sequential,
            Parallel,
        }
        
        private struct TileInstance
        {
            public MapData.Tile MapTile;
            public GameObject Main;
            public AsyncGameObjectPool MainPool;
            public GameObject Decoration;
            public AsyncGameObjectPool DecorationPool;

            public readonly void Release()
            {
                MainPool.Release(Main);
                if (Decoration != null) DecorationPool.Release(Decoration);
            }
        }
        
        private struct SpawnRequest
        {
            public AsyncGameObjectPool Pool;
            public int MapIndex;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool IsDecoration;
        }
        
        [Header("Streaming settings")]
        [Tooltip("In tiles")]
        [SerializeField, Range(1, 50)] private int _spawnRadius = 30;
        [Tooltip("Maximum amount of tiles removed per step")]
        [SerializeField, Range(1, 2000)] private int _cleanupBatchCount = 50;
        [Tooltip("Tile spawns is batched by all model types, is this bathes comes sequentially or in parallel?")]
        [SerializeField] private OperationType _spawnOperationType = OperationType.Parallel;
        [Tooltip("First happens the spawn task, second the cleanup. If set to parallel both will run at the same time")]
        [SerializeField] private OperationType _spawnAndCleanupOperationType = OperationType.Parallel;
        
        [Header("Map")]
        [SerializeField] private string _mapName = "maze";
        [SerializeField, Range(0,1)] private float _decorationSpawnChance = 0.5f;
        [SerializeField] private int _seed = 0;
        
        private bool _initialized = false;
        
        private AddressableAssets.Assets _assets;
        
        private MapData _map;
        private Transform _player;
        private MapComponent _mapComponent;
        
        private AsyncGameObjectPoolCollection _waterPool;
        private AsyncGameObjectPoolCollection _groundPool;
        private AsyncGameObjectPoolCollection _decorationsPool;
        
        private Task _loop;
        
        private RectInt _currentStreamArea;
        private RectInt _lastStreamArea;
        private List<RectInt> _addedAreas;
        private List<RectInt> _removedAreas;

        private Dictionary<AsyncGameObjectPool, List<SpawnRequest>> _poolToSpawnRequests;
        private Dictionary<int, TileInstance> _activeTiles;

        public MapData Map => _map;

        public async Task Init(AddressableAssets.Assets assets, Transform player, MapComponent mapComponent)
        {
            _mapComponent = mapComponent;
            _assets = assets;
            _player = player;
            
            
            _map = _assets.Maps.FindMapByName(_mapName);
            _map.InitRandomTilesState(_assets, _seed, _decorationSpawnChance);

            _waterPool = _assets.WaterTiles.CreateAsyncPools(_mapComponent.WaterParent);
            _groundPool = _assets.GroundTiles.CreateAsyncPools(_mapComponent.GroundParent);
            _decorationsPool = _assets.Decorations.CreateAsyncPools(_mapComponent.DecorationsParent);
            
            _addedAreas = new List<RectInt>();
            _removedAreas = new List<RectInt>();
            _poolToSpawnRequests = new Dictionary<AsyncGameObjectPool, List<SpawnRequest>>();
            _activeTiles = new Dictionary<int, TileInstance>();
            
            _loop = StreamLoop();
            _initialized = true;
            await Task.CompletedTask;
        }

        private async Task StreamLoop()
        {
            while (enabled && gameObject.activeInHierarchy)
            {
                await Step();
                await Task.Yield();
            }
        }
        
        private async Task Step()
        {
            // Here we have a difference between rects, that determines what tiles should be loaded and unloaded
            _lastStreamArea = _currentStreamArea;
            var playerCellPosition = _mapComponent.WorldToCell(_player.position);
            _currentStreamArea = MapStreamingMath.CalculateActiveArea(playerCellPosition, _spawnRadius, _map.Width, _map.Height);
            MapStreamingMath.CalculateAreaDifference(_lastStreamArea, _currentStreamArea, _addedAreas, _removedAreas);

            // Spawn new tiles and delete unused tiles in parallel since can afford that with our rect differences
            ScheduleSpawns();
            
            var spawnTask = _spawnOperationType == OperationType.Sequential 
                ? ExecuteSpawnRequestsSequential() 
                : ExecuteSpawnRequestsParallel();
            
            var cleanupTask = CleanupTiles();

            if (_spawnAndCleanupOperationType == OperationType.Sequential)
            {
                await spawnTask;
                await cleanupTask;
            }
            else
            {
                await Task.WhenAll(spawnTask, cleanupTask);
            }
        }
        
        private async Task CleanupTiles()
        {
            var i = 0;
            foreach (var removedArea in _removedAreas)
            {
                foreach (var tilePosition in removedArea.allPositionsWithin)
                {
                    var tileIndex = _map.XYToIndex(tilePosition);
                    
                    if (!_activeTiles.TryGetValue(tileIndex, out var tileInstance))
                        continue;

                    
                    tileInstance.Release();
                    _activeTiles.Remove(tileIndex);
                    
                    i++;
                    // Not exactly needed... 
                    if (i % _cleanupBatchCount == 0) await Task.Yield();
                }
            }
        }
        
        #region Spawning
        
        /// <summary>
        /// Its more effective if <see cref="Object.InstantiateAsync"/> called as batch.
        /// Therefore, need to schedule spawns sorted for each pool, to batch as much work as possible to them.
        /// Alternatively we can use singe tile with all possible content in it, and just disable-enable it afterward.
        /// Also, possible to use jobs for parallelized scheduling
        /// </summary>
        private void ScheduleSpawns()
        {
            _poolToSpawnRequests.Clear();

            foreach (var addedArea in _addedAreas)
            {
                foreach (var tilePosition in addedArea.allPositionsWithin)
                {
                    var tileIndex = _map.XYToIndex(tilePosition);
                    
                    if (_activeTiles.ContainsKey(tileIndex))
                        continue;

                    _map.IndexToXY(tileIndex, out var position);
                    var tile = _map[tileIndex];

                    var pos = _mapComponent.CellToWorld(position);

                    var spawnReqest = new SpawnRequest
                    {
                        MapIndex = tileIndex,
                        Position = pos,
                        Rotation = Quaternion.identity,
                        IsDecoration = false
                    };

                    if (tile.Type == MapData.TileType.Water)
                    {
                        spawnReqest.Pool = _waterPool[tile.WaterPrefabIndex];
                        AddSpawnRequest(spawnReqest);
                    }
                    else
                    {
                        spawnReqest.Pool = _groundPool[tile.GroundPrefabIndex];
                        AddSpawnRequest(spawnReqest);

                        if (tile.DecorationPrefabIndex != -1)
                        {
                            spawnReqest.IsDecoration = true;
                            spawnReqest.Pool = _decorationsPool[tile.DecorationPrefabIndex];
                            AddSpawnRequest(spawnReqest);
                        }
                    }
                }
            }
        }
        
        private void AddSpawnRequest(SpawnRequest spawnRequest)
        {
            if (!_poolToSpawnRequests.TryGetValue(spawnRequest.Pool, out var list))
            {
                list = ListPool<SpawnRequest>.Get();
                _poolToSpawnRequests.Add(spawnRequest.Pool, list);
            }

            list.Add(spawnRequest);
        }
        
        private async Task ExecuteSpawnRequestsParallel()
        {
            var spawnTasks = ListPool<Task>.Get();
            foreach (var (pool, spawnRequests) in _poolToSpawnRequests)
            {
                spawnTasks.Add(ExecuteSpawnRequestsForPool(pool, spawnRequests));
            }
            await Task.WhenAll(spawnTasks);
            ListPool<Task>.Release(spawnTasks);
        }

        private async Task ExecuteSpawnRequestsSequential()
        {
            foreach (var (pool, spawnRequests) in _poolToSpawnRequests)
            {
                await ExecuteSpawnRequestsForPool(pool, spawnRequests);
            }
        }

        private async Task ExecuteSpawnRequestsForPool(AsyncGameObjectPool pool, List<SpawnRequest> spawnRequests)
        {
            var count = spawnRequests.Count;
            
            // Not sure using pos/rot arrays for spawn is required, but it puts newly spawned objects right in place lets keep it like that
            var positions = ArrayPool<Vector3>.Shared.Rent(count);
            var rotations = ArrayPool<Quaternion>.Shared.Rent(count);

            for (var i = 0; i < count; i++)
            {
                positions[i] = spawnRequests[i].Position;
                rotations[i] = spawnRequests[i].Rotation;
            }
            
            var instances = await pool.GetBatch(count, positions, rotations);
            
            ArrayPool<Vector3>.Shared.Return(positions);
            ArrayPool<Quaternion>.Shared.Return(rotations);
            
            for (var i = 0; i < count; i++)
            {
                var spawnedGameObject = instances[i];
// #if UNITY_EDITOR
//                 spawnedGameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;
// #endif
                var spawnRequest = spawnRequests[i];
                
                if (!_activeTiles.TryGetValue(spawnRequest.MapIndex, out var tileInstance))
                    tileInstance = new TileInstance();
                
                if (spawnRequest.IsDecoration)
                {
                    tileInstance.Decoration = spawnedGameObject;
                    tileInstance.DecorationPool = pool;
                }
                else
                {
                    tileInstance.Main = spawnedGameObject;
                    tileInstance.MainPool = pool;
                }
                
                _activeTiles[spawnRequest.MapIndex] = tileInstance;
            }
            
         
            ListPool<SpawnRequest>.Release(spawnRequests);
        }
        
        #endregion
    }
}