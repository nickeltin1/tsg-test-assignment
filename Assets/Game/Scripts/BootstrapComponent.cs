using System.Threading.Tasks;
using Game.Scripts.Navigation;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Scripts
{
    public class BootstrapComponent : MonoBehaviour
    {
        [SerializeField] private AddressableAssets.Assets _assets;
        [SerializeField] private MapComponent _mapComponent;
        [SerializeField] private MapStreamerComponent _mapStreamer;
        [SerializeField] private MapNavigationComponent _mapNavigation;
      
        
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
                
                var player = Instantiate(_assets.Boat.LoadedObject).GetComponent<Player>();
                await _mapStreamer.Init(_assets, player.transform, _mapComponent);
                await _mapNavigation.Init(player, _mapStreamer.Map, _mapComponent);
            }
        }
    }
}