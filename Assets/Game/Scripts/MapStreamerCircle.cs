using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts
{
    public sealed class MapStreamerCircle : MonoBehaviour
    {
        [SerializeField] private Grid _grid;
        [SerializeField] private Transform _waterParent;
        [SerializeField] private Transform _groundParent;
        [SerializeField] private Transform _decorParent;

        [SerializeField] private float _worldRadius = 30f; // circle radius around boat (meters)
        // [SerializeField] private int _perFrameBudget = 128; // max spawns+releases per frame

        private MapData _map;
        private AddressableAssets.Assets _assets;

        private Transform _player;

        private AsyncGameObjectPool[] _waterPools;
        private AsyncGameObjectPool[] _groundPools;
        private AsyncGameObjectPool[] _decorPools;

        private readonly Dictionary<int, TileInstance> _active = new(); // key = map index

        private CancellationTokenSource _cts;
        private Task _loop;

        private struct TileInstance
        {
            public GameObject GroundOrWater; // spawned instance
            public AsyncGameObjectPool GroundOrWaterPool; // pool it came from

            public GameObject Decoration; // spawned instance (optional)
            public AsyncGameObjectPool DecorationPool; // pool it came from (optional)
        }

        public void Init(MapData map, AddressableAssets.Assets assets, Transform player)
        {
            _map = map;
            _assets = assets;
            _player = player;

            // build pools per prefab index
            var water = _assets.WaterTiles.LoadedObjects;
            var ground = _assets.GroundTiles.LoadedObjects;
            var decor = _assets.Decorations.LoadedObjects;

            _waterPools = new AsyncGameObjectPool[water.Count];
            _groundPools = new AsyncGameObjectPool[ground.Count];
            _decorPools = new AsyncGameObjectPool[decor.Count];

            for (var i = 0; i < water.Count; i++)
                _waterPools[i] = new AsyncGameObjectPool(water[i], _waterParent);
            for (var i = 0; i < ground.Count; i++)
                _groundPools[i] = new AsyncGameObjectPool(ground[i], _groundParent);
            for (var i = 0; i < decor.Count; i++)
                _decorPools[i] = new AsyncGameObjectPool(decor[i], _decorParent);

            _loop = StreamLoop();
        }


        private async Task StreamLoop()
        {
            while (true)
            {
                await StepAsync();
                await Task.Yield();
            }
        }

        private async Task StepAsync()
        {
            // circle window in cells
            var boatPos = _player.position;
            var cellCenter = _grid.WorldToCell(boatPos);
            // map uses (x, -y) when placing; convert boat cell to map coords:
            var cx = cellCenter.x;
            var cy = -cellCenter.y;

            // estimate search extents in cells from world radius
            var cellSize = _grid.cellSize;
            var cellDiag = Mathf.Max(0.001f,
                new Vector2(cellSize.x, cellSize.y == 0 ? cellSize.z : cellSize.y).magnitude);
            var rCells = Mathf.CeilToInt(_worldRadius / cellDiag);

            var desired = new HashSet<int>();

            // gather desired indices by world distance (true circle)
            for (var y = cy - rCells; y <= cy + rCells; y++)
            {
                if ((uint)y >= (uint)_map.Height) continue;
                for (var x = cx - rCells; x <= cx + rCells; x++)
                {
                    if ((uint)x >= (uint)_map.Width) continue;

                    var cell = new Vector3Int(x, -y, 0);
                    var pos = _grid.CellToWorld(cell);
                    if ((pos - boatPos).sqrMagnitude > _worldRadius * _worldRadius) continue;

                    desired.Add(_map.XYToIndex(x, y));
                }
            }

            
            _toRemove.Clear();
            foreach (var kv in _active)
            {
                if (!desired.Contains(kv.Key)) _toRemove.Add(kv.Key);
            }

            foreach (var idx in _toRemove)
            {
                var inst = _active[idx];
                
                if (inst.Decoration != null)
                {
                    await inst.DecorationPool.Release(inst.Decoration);
                    inst.Decoration = null;
                    inst.DecorationPool = null;
                }

                if (inst.GroundOrWater)
                {
                    await inst.GroundOrWaterPool.Release(inst.GroundOrWater);
                    inst.GroundOrWater = null;
                    inst.GroundOrWaterPool = null;
                }

                _active.Remove(idx);
            }
            
            foreach (var idx in desired)
            {
                if (_active.ContainsKey(idx)) continue;

                _map.IndexToXY(idx, out var x, out var y);
                var tile = _map[idx];
                var cell = new Vector3Int(x, -y, 0);
                var pos = _grid.CellToWorld(cell);

                var tileInstance = new TileInstance();

                if (tile.Type == MapData.TileType.Water)
                {
                    var pool = _waterPools[tile.WaterPrefabIndex];
                    tileInstance.GroundOrWaterPool = pool;
                    tileInstance.GroundOrWater = await pool.Get(pos, Quaternion.identity, _waterParent);
                }
                else
                {
                    var gPool = _groundPools[tile.GroundPrefabIndex];
                    tileInstance.GroundOrWaterPool = gPool;
                    tileInstance.GroundOrWater = await gPool.Get(pos, Quaternion.identity, _groundParent);

                    if (tile.DecorationPrefabIndex >= 0 && tile.DecorationPrefabIndex < _decorPools.Length)
                    {
                        var dPool = _decorPools[tile.DecorationPrefabIndex];
                        tileInstance.DecorationPool = dPool;
                        tileInstance.Decoration = await dPool.Get(pos, Quaternion.identity, _decorParent);
                    }
                }

                _active[idx] = tileInstance;
            }
        }
        
        private readonly List<int> _toRemove = new();
    }
}