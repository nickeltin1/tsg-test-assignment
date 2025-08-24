using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;

namespace Game.Scripts
{
    public class BootstrapComponent : MonoBehaviour
    {
        [SerializeField] private AddressableAssets.Assets _assets;
        [SerializeField] private MapComponent _map;
        [SerializeField] private MapStreamerComponent _mapStreamer;
        [SerializeField] private string _mapName;
        [SerializeField, Range(0,1)] private float _decorationSpawnChance = 0.3f;
        [SerializeField] private int _seed;

        private CancellationTokenSource _cancellationTokenSource;
        private MapData _mapData;
        
        private async void Awake()
        {
            await Init();
        }
        
        private async Task Init()
        {
           
            using (new StopwatchScope("Bootstrap"))
            {
                // Not necessary to explicitly init addressables but to keep init process clear lets do that
                using (new StopwatchScope("Addressables.Init"))
                {
                    var handle = Addressables.InitializeAsync();
                    await handle.Task;
                }

                using (new StopwatchScope("LoadingAssets"))
                {
                    await AddressableAssets.LoadAsync(_assets);
                }
                
                _mapData = _assets.Maps.FindMapByName(_mapName);
                _mapData.InitRandomTilesState(_assets, _seed, _decorationSpawnChance);

                var player = Instantiate(_assets.Boat.LoadedObject).GetComponent<Player>();
                
                using (new StopwatchScope("BuildingMap"))
                {
                    _mapStreamer.Init(_mapData, _assets, player.transform);
                    // await BuildMap();
                }
            }
        }

        [ContextMenu("Cancel map generation")]
        private void CancelMapGeneration()
        {
            Debug.Log("Canceling map generation");
            _cancellationTokenSource?.Cancel();
        }
        
        [ContextMenu("Clear map")]
        private async void ClearMap()
        {
            CancelMapGeneration();
            Debug.Log("Clearing map");
            await _map.ClearAsync();
        }
            
        [ContextMenu("Build Map")]
        private async Task BuildMap()
        {
            CancelMapGeneration();
            using (new StopwatchScope("RebuildingMap"))
            {
                //Can clear and instantiate at the same time
                var tasks = new List<Task>();
                tasks.Add(_map.ClearAsync());
                _cancellationTokenSource = new CancellationTokenSource();
                tasks.Add(_map.BuildAsync(_assets, _mapData, _cancellationTokenSource.Token));
                await Task.WhenAll(tasks.ToArray());
            }
        }
    }
}