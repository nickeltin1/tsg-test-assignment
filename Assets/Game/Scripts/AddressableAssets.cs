using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Game.Scripts
{
    /// <summary>
    /// Provides handles for all used game assets as addressables.
    /// Call <see cref="LoadAsync"/> to load all assets in parallel.
    /// </summary>
    public static class AddressableAssets
    {
        /// <summary>
        /// Just represents asset that can be loaded
        /// </summary>
        public interface ILoadableAsset
        {
            Task Load();
        }
        

        /// <summary>
        /// Abstract collection for addressable assets under one label
        /// </summary>
        [Serializable]
        public class AssetsCollection<TAsset> : ILoadableAsset 
            where TAsset : Object
        {
            [SerializeField] private AssetLabelReference _loadReference;

            private List<TAsset> _loadedObjects;
            public IList<TAsset> LoadedObjects => _loadedObjects;

            public virtual async Task Load()
            {
                var loadHandle = Addressables.LoadAssetsAsync<TAsset>(_loadReference, o => Debug.Log($"Object {o.name} loaded"));
                await loadHandle.Task;
                _loadedObjects = new List<TAsset>(loadHandle.Result);
                loadHandle.Release();
            }

            public TAsset GetRandom() => LoadedObjects[Random.Range(0, LoadedObjects.Count)];
        }

        /// <summary>
        /// Single addressable asset loaded by reference
        /// </summary>
        [Serializable]
        public class Asset<TAsset> : ILoadableAsset where TAsset : Object
        {
            [SerializeField] private AssetReferenceT<TAsset> _loadReference;
            
            public TAsset LoadedObject => _loadReference.Asset as TAsset;
            
            public async Task Load()
            {
                var loadOperation = _loadReference.LoadAssetAsync<TAsset>();
                await loadOperation.Task;
            }
        }

        [Serializable]
        public class GameObjectsCollection : AssetsCollection<GameObject> { }
        
        /// <summary>
        /// Loads maps as text assets, additionally parses the text assets into
        /// concrete <see cref="MapData"/> instances.
        /// Might be usefully to add map save/load from disk/cloud, but for now its static assets
        /// </summary>
        [Serializable]
        public class MapsCollection : AssetsCollection<TextAsset>
        {
            private List<MapData> _loadedMaps;
            
            public IList<MapData> LoadedMaps => _loadedMaps;
            
            /// <summary>
            /// Loads all text assets, then parse all map datas.
            /// </summary>
            public override async Task Load()
            {
                await base.Load();
                var mapLoadTasks = new List<Task<MapData>>();
                foreach (var textAsset in LoadedObjects)
                {
                    using var stringReader = new StringReader(textAsset.text);
                    // In this case async map data load is not really used, since not reading from file directly
                    // However later on might add map saving, and there it will work just fine
                    Debug.Log("Loading map " + textAsset.name);
                    mapLoadTasks.Add(MapData.LoadAsync(stringReader, textAsset.name));
                }
                var result = await Task.WhenAll(mapLoadTasks);
                _loadedMaps = result.ToList();
            }

            public MapData FindMapByName(string mapName)
            {
                return _loadedMaps.FirstOrDefault(map => map.Name == mapName);
            }
        }

        /// <summary>
        /// All addressables assets used in game
        /// Serialized wrapper to expose addressable references
        /// In runtime holds loaded assets references
        /// </summary>
        [Serializable]
        public class Assets : IEnumerable<ILoadableAsset>
        {
            [InlineProperty] public GameObjectsCollection WaterTiles;
            [InlineProperty] public GameObjectsCollection GroundTiles;
            [InlineProperty] public GameObjectsCollection Decorations;
            [InlineProperty] public MapsCollection Maps;
            [InlineProperty] public Asset<GameObject> Boat;
            [InlineProperty] public Asset<GameObject> Water;
            
            /// <summary>
            /// Can be done with reflections, but keeping it simple for now
            /// Don't forget to add <see cref="ILoadableAsset"/> if any new is defined
            /// </summary>
            public IEnumerator<ILoadableAsset> GetEnumerator()
            {
                yield return WaterTiles;
                yield return GroundTiles;
                yield return Decorations;
                yield return Maps;
                yield return Boat;
                yield return Water;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        
        /// <summary>
        /// Loads all assets to memory in parallel
        /// </summary>
        public static async Task LoadAsync(Assets assets)
        {
            var tasks = new List<Task>(assets.Select(collection => collection.Load()));
            await Task.WhenAll(tasks);
        }
    }
}