using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Scripts
{
    public class Bootstrap : MonoBehaviour
    {
        
        private async void Awake()
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            Debug.Log("Addressables init started");
            var handle = Addressables.InitializeAsync();
            await handle.Task;
            stopwatch.Stop();
            Debug.Log($"Addressables init ended, time took {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Reset();
            
            // Addressables.Release();
        }
    }
}