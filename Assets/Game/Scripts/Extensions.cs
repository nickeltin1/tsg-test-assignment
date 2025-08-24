using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Scripts
{
    public static class Extensions
    {
        public static TAsset GetRandom<TAsset>(this AddressableAssets.AssetsCollection<TAsset> collection) where TAsset : Object
        {
            return collection.LoadedObjects[Random.Range(0, collection.LoadedObjects.Count)];
        }

        public static AsyncGameObjectPoolCollection CreateAsyncPools(this AddressableAssets.AssetsCollection<GameObject> collection, Transform parent)
        {
            return new AsyncGameObjectPoolCollection(collection.LoadedObjects, parent);
        }
    }
}