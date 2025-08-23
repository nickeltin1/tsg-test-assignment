using Michsky.LSS;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Scripts
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private LSS_LoadingScreen _loadingScreen;
        
        private async void Awake()
        {
            _loadingScreen.enableVirtualLoading = true;
            _loadingScreen.onLoadingStart.Invoke();

            var handle = Addressables.InitializeAsync();
            await handle.Task;
        }
    }
}