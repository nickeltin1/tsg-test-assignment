using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Scripts
{
    /// <summary>
    /// Simple implementation for the async pooling.
    /// Uses <see cref="Object.InstantiateAsync"/> for batch instantiation and spreads out objects release
    /// </summary>
    public class AsyncGameObjectPool
    {
        private static readonly Vector3[] _tempVectorArray = new Vector3[1];
        private static readonly Quaternion[] _tempQuaternionArray = new Quaternion[1];
        private static readonly GameObject[] _tempGameObjectArray = new GameObject[1];
        
        
        private readonly GameObject _source;
        private readonly Transform _root;
        private readonly ConcurrentQueue<GameObject> _queue;

        public AsyncGameObjectPool(GameObject source, Transform root)
        {
            _source = source;
            _root = root;
            _queue = new ConcurrentQueue<GameObject>();
        }
        
        public async Task<GameObject> Get(Vector3 position, Quaternion rotation, Transform parent)
        {
            _tempVectorArray[0] = position;
            _tempQuaternionArray[0] = rotation;
            var task = GetBatch(1, _tempVectorArray, _tempQuaternionArray, parent);
            await task;
            return task.Result[0];
        }

        
        public async Task<GameObject[]> GetBatch(int count, Vector3[] positions, Quaternion[] rotations, Transform parent = null)
        {
            parent ??= _root;
            // First trying to use existing game objects
            // (Array pool can be used too, but whatever)
            var result = new GameObject[count];
            var remainingCount = count;
            var i = 0;
            while (_queue.TryDequeue(out var cachedGameObject) && remainingCount > 0)
            {
                result[i] = cachedGameObject;
                cachedGameObject.gameObject.SetActive(true);
                cachedGameObject.transform.SetParent(parent);
                cachedGameObject.transform.position = positions[i];
                cachedGameObject.transform.rotation = rotations[i];
                remainingCount--;
                i++;
            }

            // If requested batch does not satisfied request spawning new objects
            if (remainingCount > 0)
            {
                var @params = new InstantiateParameters();
                @params.parent = parent;
                var asyncOperation = Object.InstantiateAsync(_source, remainingCount, 
                    positions.AsSpan(i), rotations.AsSpan(i), // Taking partial spans
                    @params);
                
                await asyncOperation;
                // Filling up the remaining indexes
                for (var j = 0; j < asyncOperation.Result.Length; j++)
                {
                    result[i + j] = asyncOperation.Result[j];
                }
            }

            return result;
        }

        public async Task Release(GameObject go)
        {
            _tempGameObjectArray[0] = go;
            await ReleaseBatch(_tempGameObjectArray);
        }
        
        /// <summary>
        /// Not required to be async, but to keep consisted style and maybe spread out looping of big batches
        /// lets at least make it partially async
        /// </summary>
        public async Task ReleaseBatch(IList<GameObject> gameObjects)
        {
            for (var i = 0; i < gameObjects.Count; i++)
            {
                var go = gameObjects[i];
                go.SetActive(false);
                go.transform.SetParent(_root);
                _queue.Enqueue(go);
                // Process in batches of 10
                if (i % 10 == 0) await Task.Yield();
            }
        }
    }

    /// <summary>
    /// Unifies different source objects with one parent
    /// </summary>
    public class AsyncGameObjectPoolCollection
    {
        private readonly List<AsyncGameObjectPool> _pools;

        public AsyncGameObjectPoolCollection(IEnumerable<GameObject> sourceObjects, Transform root)
        {
            _pools = new List<AsyncGameObjectPool>();
            foreach (var sourceObject in sourceObjects)
            {
                _pools.Add(new AsyncGameObjectPool(sourceObject, root));
            }
        }

        public int Count => _pools.Count;
        
        public AsyncGameObjectPool this[int index] => _pools[index];
    }
}