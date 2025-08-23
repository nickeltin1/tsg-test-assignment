using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts
{
    public class MapComponent : MonoBehaviour
    {
        [SerializeField] private Grid _grid;
        [SerializeField] private Transform _waterParent;
        [SerializeField] private Transform _groundParent;
        [SerializeField] private Transform _decorParent;
        
        private readonly List<GameObject> _tiles = new();

        /// <summary>
        /// Instantiates three layers of map in parallel
        /// - water
        /// - ground
        /// - decorations
        /// </summary>
        public async Task BuildAsync(AddressableAssets.Assets assets, MapData map, CancellationToken cancellationToken)
        {
            var instantiateTasks = new List<Task>();
            
            for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new Vector3Int(x, -y, 0);
                var pos = _grid.CellToWorld(cell);
                
                var tile = map[x, y];
                if (tile.Type == MapData.TileType.Water)
                {
                    instantiateTasks.Add(ScheduleInstantiation(assets.WaterTiles[tile.WaterPrefabIndex], pos, _waterParent, cancellationToken, _tiles));
                }
                else
                {
                    instantiateTasks.Add(ScheduleInstantiation(assets.GroundTiles[tile.GroundPrefabIndex], pos, _groundParent, cancellationToken, _tiles));
                    if (tile.DecorationPrefabIndex != -1)
                    {
                        instantiateTasks.Add(ScheduleInstantiation(assets.Decorations[tile.DecorationPrefabIndex], pos, _decorParent, cancellationToken, _tiles));
                    }
                }
            }
            
            await Task.WhenAll(instantiateTasks);
        }

        /// <summary>
        /// Not really async, just spreading out destroy calls.
        /// Unity actually spreads objects cleanup between frames either way.
        /// </summary>
        public async Task ClearAsync()
        {
            var oldTiles = new List<GameObject>(_tiles);
            _tiles.Clear();
            for (var index = 0; index < oldTiles.Count; index++)
            {
                var tile = oldTiles[index];
                Destroy(tile);
                // Destroy in batches
                if (index % 10 == 0)
                {
                    await Task.Yield();
                }
            }
        }

        public void Clear()
        {
            foreach (var tile in _tiles) Destroy(tile);
            
            _tiles.Clear();
        }
        
        // public async Task BuildAsyncBatch(AddressableAssets.Assets assets, string mapName)
        // {
        //     var map = assets.Maps.FindMapByName(mapName);
        //     var instantiatePositions = new Vector3[map.Length];
        //     
        //     for (var i = 0; i < map.Length; i++)
        //     {
        //         map.IndexToXY(i, out var x, out var y);
        //         var cell = new Vector3Int(x, -y, 0);
        //         var pos = _grid.CellToWorld(cell);
        //         instantiatePositions[i] = pos;
        //         // var tile = map[x, y];
        //         // if (tile.Type == MapData.TileType.Water)
        //         // {
        //         //     instantiatePositions.Add(ScheduleInstantiation(assets.WaterTiles.GetRandom(), pos, _waterParent));
        //         // }
        //         // else
        //         // {
        //         //     instantiatePositions.Add(ScheduleInstantiation(assets.GroundTiles.GetRandom(), pos, _groundParent));
        //         //     instantiatePositions.Add(ScheduleInstantiation(assets.Decorations.GetRandom(), pos, _decorParent));
        //         // }
        //     }
        //     
        //     var asyncOperation = InstantiateAsync(assets.GroundTiles.GetRandom(), 
        //         instantiatePositions.Length, 
        //         instantiatePositions.AsSpan(), ReadOnlySpan<Quaternion>.Empty);
        //     await asyncOperation;
        //     // await Task.WhenAll(instantiateTasks);
        // }
        //
        // public void Build(AddressableAssets.Assets assets, string mapName)
        // {
        //     var map = assets.Maps.FindMapByName(mapName);
        //     
        //     for (var y = 0; y < map.Height; y++)
        //     for (var x = 0; x < map.Width; x++)
        //     {
        //         var cell = new Vector3Int(x, -y, 0);
        //         var pos = _grid.CellToWorld(cell);
        //         
        //         var tile = map[x, y];
        //         if (tile.Type == MapData.TileType.Water)
        //         {
        //             Instantiate(assets.WaterTiles.GetRandom(), pos, Quaternion.identity, _waterParent);
        //         }
        //         else
        //         {
        //             Instantiate(assets.GroundTiles.GetRandom(), pos, Quaternion.identity, _groundParent);
        //             Instantiate(assets.Decorations.GetRandom(), pos, Quaternion.identity, _decorParent);
        //         }
        //     }
        // }

        public static async Task ScheduleInstantiation(GameObject original, Vector3 position, Transform parent, CancellationToken cancellationToken, List<GameObject> instances)
        {
            var @params = new InstantiateParameters()
            {
                parent = parent
            };
            var asyncOperation = InstantiateAsync(original, position, Quaternion.identity, @params, cancellationToken);
            await asyncOperation;
            instances.Add(asyncOperation.Result[0]);
        }
    }
}
