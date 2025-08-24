using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts
{
    public sealed class MapStreamerComponent : MonoBehaviour
    {
        [Header("Scene Refs")]
        [SerializeField] private Grid _grid;
        [SerializeField] private Transform _waterParent;
        [SerializeField] private Transform _groundParent;
        [SerializeField] private Transform _decorParent;

        [Header("Streaming")]
        [SerializeField] private float _worldRadius = 30f; // circle radius around boat (meters)

        private MapData _map;
        private AddressableAssets.Assets _assets;
        private Transform _player;
        
        
        private AsyncGameObjectPoolCollection _waterPools;
        private AsyncGameObjectPoolCollection _groundPools;
        private AsyncGameObjectPoolCollection _decorPools;
        
        private readonly HashSet<int> _tilesToAdd = new();
        private readonly List<int> _tilesToRemove = new();
        
        private readonly Dictionary<AsyncGameObjectPool, List<PendingSpawn>> _pendingByPool = new();
        private readonly Dictionary<AsyncGameObjectPool, List<GameObject>> _releaseByPool = new();
        
        private readonly Stack<List<PendingSpawn>> _pendingSpawnPoolLists = new();
        private readonly Stack<List<GameObject>> _releaseListCache = new();

        private readonly Dictionary<int, TileInstance> _activeTiles = new();

        private Task _loop;
        private bool _running;

        private struct TileInstance
        {
            public GameObject GroundOrWater;
            public AsyncGameObjectPool GroundOrWaterPool;

            public GameObject Decoration;
            public AsyncGameObjectPool DecorationPool;
        }

        private struct PendingSpawn
        {
            public int MapIndex;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool IsDecoration;
        }

        public void Init(MapData map, AddressableAssets.Assets assets, Transform player)
        {
            _map = map;
            _assets = assets;
            _player = player;

            _waterPools = _assets.WaterTiles.CreateAsyncPools(_waterParent);
            _groundPools = _assets.GroundTiles.CreateAsyncPools(_groundParent);
            _decorPools = _assets.Decorations.CreateAsyncPools(_decorParent);

            _running = true;
            _loop = StreamLoop();
        }

        private void OnDisable()
        {
            _running = false;
            _loop = null;
        }

        private async Task StreamLoop()
        {
            while (_running)
            {
                await StepAsync();
                await Task.Yield();
            }
        }

        private async Task StepAsync()
        {
            if (_map == null || _player == null || _grid == null)
                return;
            
            var boatPos = _player.position;
            var cellCenter = _grid.WorldToCell(boatPos);
            var cx = cellCenter.x;
            var cy = -cellCenter.y; // map uses (x, -y)

            var cellSize = _grid.cellSize;
            var cellDiag = Mathf.Max(
                0.001f,
                new Vector2(cellSize.x, cellSize.y == 0 ? cellSize.z : cellSize.y).magnitude
            );
            var rCells = Mathf.CeilToInt(_worldRadius / cellDiag);

            _tilesToAdd.Clear();
            for (int y = cy - rCells; y <= cy + rCells; y++)
            {
                if ((uint)y >= (uint)_map.Height) continue;

                for (int x = cx - rCells; x <= cx + rCells; x++)
                {
                    if ((uint)x >= (uint)_map.Width) continue;

                    var cell = new Vector3Int(x, -y, 0);
                    var pos  = _grid.CellToWorld(cell);
                    if ((pos - boatPos).sqrMagnitude > _worldRadius * _worldRadius) continue;

                    _tilesToAdd.Add(_map.XYToIndex(x, y));
                }
            }
            
            _tilesToRemove.Clear();
            foreach (var kv in _activeTiles)
            {
                if (!_tilesToAdd.Contains(kv.Key)) _tilesToRemove.Add(kv.Key);
            }

            _releaseByPool.Clear();
            foreach (var idx in _tilesToRemove)
            {
                var inst = _activeTiles[idx];

                if (inst.Decoration && inst.DecorationPool != null)
                    AddRelease(inst.DecorationPool, inst.Decoration);

                if (inst.GroundOrWater && inst.GroundOrWaterPool != null)
                    AddRelease(inst.GroundOrWaterPool, inst.GroundOrWater);

                _activeTiles.Remove(idx);
            }

            // Execute releases per pool (tight arrays)
            foreach (var (pool, list) in _releaseByPool)
            {
                if (pool != null && list.Count > 0)
                {
                    try
                    {
                        var toRelease = new GameObject[list.Count];
                        for (var i = 0; i < list.Count; i++) toRelease[i] = list[i];
                        await pool.ReleaseBatch(toRelease);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
                list.Clear();
                _releaseListCache.Push(list);
            }
            
            _pendingByPool.Clear();

            foreach (var idx in _tilesToAdd)
            {
                if (_activeTiles.ContainsKey(idx)) continue;

                _map.IndexToXY(idx, out var x, out var y);
                var tile = _map[idx];
                var cell = new Vector3Int(x, -y, 0);
                var pos  = _grid.CellToWorld(cell);

                var pendingSpawn = new PendingSpawn
                {
                    MapIndex = idx,
                    Position = pos,
                    Rotation = Quaternion.identity,
                    IsDecoration = false
                };
                
                if (tile.Type == MapData.TileType.Water)
                {
                    AddPending(_waterPools[tile.WaterPrefabIndex], pendingSpawn);
                }
                else
                {
                    AddPending(_groundPools[tile.GroundPrefabIndex], pendingSpawn);

                    if (tile.DecorationPrefabIndex != -1)
                    {
                        pendingSpawn.IsDecoration = true;
                        AddPending(_decorPools[tile.DecorationPrefabIndex], pendingSpawn);
                    }
                }
            }

            // Perform batched spawns per pool (tight arrays)
            foreach (var (pool, list) in _pendingByPool)
            {
                if (pool == null || list.Count == 0)
                {
                    RecyclePendingList(list); 
                    continue;
                }

                // Build exact-length arrays (Length == count) to match pool expectations
                var count = list.Count;
                var positions = new Vector3[count];
                var rotations = new Quaternion[count];

                for (var i = 0; i < count; i++)
                {
                    positions[i] = list[i].Position;
                    rotations[i] = list[i].Rotation;
                }

                try
                {
                    var instances = await pool.GetBatch(count, positions, rotations);

                    for (int i = 0; i < count; i++)
                    {
                        var ps = list[i];
                        var go = instances[i];

                        if (!_activeTiles.TryGetValue(ps.MapIndex, out var ti))
                            ti = new TileInstance();

                        if (ps.IsDecoration)
                        {
                            ti.Decoration = go;
                            ti.DecorationPool = pool;
                        }
                        else
                        {
                            ti.GroundOrWater = go;
                            ti.GroundOrWaterPool = pool;
                        }

                        _activeTiles[ps.MapIndex] = ti;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }

                RecyclePendingList(list);
            }
        }

        private void AddPending(AsyncGameObjectPool pool, PendingSpawn spawn)
        {
            if (!_pendingByPool.TryGetValue(pool, out var list))
            {
                list = _pendingSpawnPoolLists.Count > 0
                    ? _pendingSpawnPoolLists.Pop()
                    : new List<PendingSpawn>(32);
                _pendingByPool.Add(pool, list);
            }
            list.Add(spawn);
        }

        private void RecyclePendingList(List<PendingSpawn> list)
        {
            list.Clear();
            _pendingSpawnPoolLists.Push(list);
        }

        private void AddRelease(AsyncGameObjectPool pool, GameObject go)
        {
            if (!_releaseByPool.TryGetValue(pool, out var list))
            {
                list = _releaseListCache.Count > 0
                    ? _releaseListCache.Pop()
                    : new List<GameObject>(32);
                _releaseByPool.Add(pool, list);
            }
            list.Add(go);
        }
    }
}
