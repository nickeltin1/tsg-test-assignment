using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts
{
    public sealed class MapStreamerCircle : MonoBehaviour
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

        private AsyncGameObjectPool[] _waterPools;
        private AsyncGameObjectPool[] _groundPools;
        private AsyncGameObjectPool[] _decorPools;

        // Active tile instances keyed by map index
        private readonly Dictionary<int, TileInstance> _active = new();

        // Stable mapping: which parent belongs to which pool instance
        private readonly Dictionary<AsyncGameObjectPool, Transform> _poolParent = new(128);

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

            // Build pools per prefab index
            var water = _assets.WaterTiles.LoadedObjects;
            var ground = _assets.GroundTiles.LoadedObjects;
            var decor = _assets.Decorations.LoadedObjects;

            _waterPools = new AsyncGameObjectPool[water.Count];
            _groundPools = new AsyncGameObjectPool[ground.Count];
            _decorPools  = new AsyncGameObjectPool[decor.Count];

            _poolParent.Clear();

            for (int i = 0; i < water.Count; i++)
            {
                var p = new AsyncGameObjectPool(water[i], _waterParent);
                _waterPools[i] = p;
                _poolParent[p] = _waterParent;
            }
            for (int i = 0; i < ground.Count; i++)
            {
                var p = new AsyncGameObjectPool(ground[i], _groundParent);
                _groundPools[i] = p;
                _poolParent[p] = _groundParent;
            }
            for (int i = 0; i < decor.Count; i++)
            {
                var p = new AsyncGameObjectPool(decor[i], _decorParent);
                _decorPools[i] = p;
                _poolParent[p] = _decorParent;
            }

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
                try
                {
                    await StepAsync();
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
                await Task.Yield();
            }
        }

        private async Task StepAsync()
        {
            if (_map == null || _player == null || _grid == null)
                return;

            // --- Compute desired set (true circle in world space) ---
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

            _desired.Clear();
            for (int y = cy - rCells; y <= cy + rCells; y++)
            {
                if ((uint)y >= (uint)_map.Height) continue;

                for (int x = cx - rCells; x <= cx + rCells; x++)
                {
                    if ((uint)x >= (uint)_map.Width) continue;

                    var cell = new Vector3Int(x, -y, 0);
                    var pos  = _grid.CellToWorld(cell);
                    if ((pos - boatPos).sqrMagnitude > _worldRadius * _worldRadius) continue;

                    _desired.Add(_map.XYToIndex(x, y));
                }
            }

            // -------- REMOVALS: active \ desired (bucket by pool → ReleaseBatch) --------
            _toRemove.Clear();
            foreach (var kv in _active)
                if (!_desired.Contains(kv.Key))
                    _toRemove.Add(kv.Key);

            _releaseByPool.Clear();
            foreach (var idx in _toRemove)
            {
                var inst = _active[idx];

                if (inst.Decoration && inst.DecorationPool != null)
                    AddRelease(inst.DecorationPool, inst.Decoration);

                if (inst.GroundOrWater && inst.GroundOrWaterPool != null)
                    AddRelease(inst.GroundOrWaterPool, inst.GroundOrWater);

                _active.Remove(idx);
            }

            // Execute releases per pool (tight arrays)
            foreach (var kv in _releaseByPool)
            {
                var pool = kv.Key;
                var list = kv.Value;
                if (pool != null && list.Count > 0)
                {
                    try
                    {
                        var toRelease = new GameObject[list.Count];
                        for (int i = 0; i < list.Count; i++) toRelease[i] = list[i];
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

            // -------- SPAWNS: desired \ active (bucket by pool → GetBatch) --------
            _pendingByPool.Clear();

            foreach (var idx in _desired)
            {
                if (_active.ContainsKey(idx)) continue;

                _map.IndexToXY(idx, out var x, out var y);
                var tile = _map[idx];
                var cell = new Vector3Int(x, -y, 0);
                var pos  = _grid.CellToWorld(cell);

                if (tile.Type == MapData.TileType.Water)
                {
                    var pool = SafePool(_waterPools, tile.WaterPrefabIndex);
                    if (pool != null)
                        AddPending(pool, new PendingSpawn
                        {
                            MapIndex = idx,
                            Position = pos,
                            Rotation = Quaternion.identity,
                            IsDecoration = false
                        });
                }
                else
                {
                    var gPool = SafePool(_groundPools, tile.GroundPrefabIndex);
                    if (gPool != null)
                        AddPending(gPool, new PendingSpawn
                        {
                            MapIndex = idx,
                            Position = pos,
                            Rotation = Quaternion.identity,
                            IsDecoration = false
                        });

                    if (tile.DecorationPrefabIndex >= 0 && tile.DecorationPrefabIndex < _decorPools.Length)
                    {
                        var dPool = SafePool(_decorPools, tile.DecorationPrefabIndex);
                        if (dPool != null)
                            AddPending(dPool, new PendingSpawn
                            {
                                MapIndex = idx,
                                Position = pos,
                                Rotation = Quaternion.identity,
                                IsDecoration = true
                            });
                    }
                }
            }

            // Perform batched spawns per pool (tight arrays)
            foreach (var kv in _pendingByPool)
            {
                var pool = kv.Key;
                var list = kv.Value;

                if (pool == null || list.Count == 0) { RecyclePendingList(list); continue; }

                // Build exact-length arrays (Length == count) to match pool expectations
                int count = list.Count;
                var positions = new Vector3[count];
                var rotations = new Quaternion[count];

                for (int i = 0; i < count; i++)
                {
                    positions[i] = list[i].Position;
                    rotations[i] = list[i].Rotation;
                }

                var parent = _poolParent.TryGetValue(pool, out var p) ? p : _groundParent;

                try
                {
                    var instances = await pool.GetBatch(count, positions, rotations, parent);

                    for (int i = 0; i < count; i++)
                    {
                        var ps = list[i];
                        var go = instances[i];

                        if (!_active.TryGetValue(ps.MapIndex, out var ti))
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

                        _active[ps.MapIndex] = ti;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }

                RecyclePendingList(list);
            }
        }

        // ----------------- helpers -----------------

        private static AsyncGameObjectPool SafePool(AsyncGameObjectPool[] arr, int idx)
        {
            return (idx >= 0 && idx < arr.Length) ? arr[idx] : null;
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

        // temp containers (reused across frames)
        private readonly HashSet<int> _desired = new();
        private readonly List<int> _toRemove = new();

        private readonly Dictionary<AsyncGameObjectPool, List<PendingSpawn>> _pendingByPool = new(32);
        private readonly Stack<List<PendingSpawn>> _pendingSpawnPoolLists = new();

        private readonly Dictionary<AsyncGameObjectPool, List<GameObject>> _releaseByPool = new(16);
        private readonly Stack<List<GameObject>> _releaseListCache = new();
    }
}
