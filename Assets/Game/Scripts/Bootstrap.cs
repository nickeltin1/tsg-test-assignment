using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Scripts
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private AddressableAssets.Assets _assets;
        [SerializeField] private MapComponent _map;
        [SerializeField] private string _mapName;

        private async void Awake()
        {
            await Init();
        }

        private async Task Init()
        {
            using (new StopwatchScope("Bootstrap"))
            {
                using (new StopwatchScope("Addressables.Init"))
                {
                    var handle = Addressables.InitializeAsync();
                    await handle.Task;
                }

                using (new StopwatchScope("LoadingAssets"))
                {
                    await AddressableAssets.LoadAsync(_assets);
                }

                var rawMap = _assets.Maps.LoadedObjects.First();
                var map = _assets.Maps.LoadedMaps.First();
                
                Debug.Log(rawMap);
                Debug.Log($"map size {map.Width}x{map.Height}");


                using (new StopwatchScope("BuildingMap"))
                {
                    // _map.Build(_assets, _mapName);
                    await _map.BuildAsync(_assets, _mapName);
                    // await _map.BuildAsyncBatch(_assets, _mapName);
                }
            }
        }
    }
}