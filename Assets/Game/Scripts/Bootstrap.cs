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
            // _loadingScreen.enableVirtualLoading = true;
            // _loadingScreen.onLoadingStart.Invoke();

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            Debug.Log("Addressables init started");
            var handle = Addressables.InitializeAsync();
            await handle.Task;
            stopwatch.Stop();
            Debug.Log($"Addressables init ended, time took {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}