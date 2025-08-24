using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts
{
    [Obsolete]
    public sealed class MapStreamerOld : MonoBehaviour
    {
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

        // Pools
        private AsyncGameObjectPoolCollection _waterPools;
        private AsyncGameObjectPoolCollection _groundPools;
        private AsyncGameObjectPoolCollection _decorPools;

        // Working sets / caches
        private readonly HashSet<int> _tilesToAdd = new();
        private readonly List<int> _tilesToRemove = new();

        private readonly Dictionary<AsyncGameObjectPool, List<PendingSpawn>> _pendingByPool = new();
        private readonly Dictionary<AsyncGameObjectPool, List<GameObject>> _releaseByPool = new();

        private readonly Stack<List<PendingSpawn>> _pendingSpawnPoolLists = new();
        private readonly Stack<List<GameObject>> _releaseListCache = new();

        private readonly Dictionary<int, TileInstance> _activeTiles = new();

        private Task _loop;
        private bool _running;

       

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
            ComputeDesiredTiles(_tilesToAdd);
            ComputeTilesToRemove(_tilesToAdd, _tilesToRemove);
            ScheduleReleases(_tilesToRemove);
            await ExecuteReleasesAsync();
            ScheduleSpawns(_tilesToAdd);
            await ExecuteSpawnsAsync();
        }
        
        
        private void ComputeDesiredTiles(HashSet<int> outDesired)
        {
            outDesired.Clear();

            var boatPos = _player.position;
            var cell = _grid.WorldToCell(boatPos);
            var cx = cell.x;
            var cy = -cell.y; // map uses (x, -y)

            var cellSize = _grid.cellSize;
            var diagVec = new Vector2(cellSize.x, cellSize.y == 0 ? cellSize.z : cellSize.y);
            var cellDiag = Mathf.Max(0.001f, diagVec.magnitude);
            var rCells = Mathf.CeilToInt(_worldRadius / cellDiag);
            var r2 = _worldRadius * _worldRadius;

            for (var y = cy - rCells; y <= cy + rCells; y++)
            {
                if ((uint)y >= (uint)_map.Height) continue;

                for (var x = cx - rCells; x <= cx + rCells; x++)
                {
                    if ((uint)x >= (uint)_map.Width) continue;

                    var gridCell = new Vector3Int(x, -y, 0);
                    var pos = _grid.CellToWorld(gridCell);
                    if ((pos - boatPos).sqrMagnitude > r2) continue;

                    outDesired.Add(_map.XYToIndex(x, y));
                }
            }
        }
        
        private void ComputeTilesToRemove(HashSet<int> desired, List<int> outToRemove)
        {
            outToRemove.Clear();
            foreach (var kv in _activeTiles)
                if (!desired.Contains(kv.Key))
                    outToRemove.Add(kv.Key);
        }
        
        private void ScheduleReleases(List<int> toRemove)
        {
            _releaseByPool.Clear();

            foreach (var idx in toRemove)
            {
                var inst = _activeTiles[idx];

                if (inst.Decoration && inst.DecorationPool != null)
                    AddRelease(inst.DecorationPool, inst.Decoration);

                if (inst.GroundOrWater && inst.GroundOrWaterPool != null)
                    AddRelease(inst.GroundOrWaterPool, inst.GroundOrWater);

                _activeTiles.Remove(idx);
            }
        }

        private async Task ExecuteReleasesAsync()
        {
            foreach (var (pool, list) in _releaseByPool)
            {
                await pool.ReleaseBatch(list);
                RecycleReleaseList(list);
            }
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

        private void RecycleReleaseList(List<GameObject> list)
        {
            list.Clear();
            _releaseListCache.Push(list);
        }
        
        
        
        private void ScheduleSpawns(HashSet<int> desired)
        {
            _pendingByPool.Clear();

            foreach (var idx in desired)
            {
                if (_activeTiles.ContainsKey(idx))
                    continue;

                _map.IndexToXY(idx, out var x, out var y);
                var tile = _map[idx];

                var pos = _grid.CellToWorld(new Vector3Int(x, -y, 0));

                var baseSpawn = new PendingSpawn
                {
                    MapIndex = idx,
                    Position = pos,
                    Rotation = Quaternion.identity,
                    IsDecoration = false
                };

                if (tile.Type == MapData.TileType.Water)
                {
                    AddPending(_waterPools[tile.WaterPrefabIndex], baseSpawn);
                }
                else
                {
                    AddPending(_groundPools[tile.GroundPrefabIndex], baseSpawn);

                    if (tile.DecorationPrefabIndex != -1)
                    {
                        baseSpawn.IsDecoration = true;
                        AddPending(_decorPools[tile.DecorationPrefabIndex], baseSpawn);
                    }
                }
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
        
        private async Task ExecuteSpawnsAsync()
        {
            foreach (var (pool, list) in _pendingByPool)
            {
                if (pool == null || list.Count == 0)
                {
                    RecyclePendingList(list);
                    continue;
                }

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
                    ApplySpawnResults(list, instances, pool);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }

                RecyclePendingList(list);
            }
        }

        private void ApplySpawnResults(List<PendingSpawn> spawns, GameObject[] instances, AsyncGameObjectPool pool)
        {
            for (var i = 0; i < spawns.Count; i++)
            {
                var ps = spawns[i];
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
    }
}