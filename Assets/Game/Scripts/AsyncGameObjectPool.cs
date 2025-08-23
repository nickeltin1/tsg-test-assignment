using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Scripts
{
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

        public async Task<GameObject[]> GetBatch(int count, Vector3[] positions, Quaternion[] rotations, Transform parent)
        {
            // First trying to use existing game objects
            var result = new GameObject[count];
            var remainingCount = count;
            var i = 0;
            while (_queue.TryDequeue(out var cachedGameObject))
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
        public async Task ReleaseBatch(GameObject[] gameObjects)
        {
            for (var i = 0; i < gameObjects.Length; i++)
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
}