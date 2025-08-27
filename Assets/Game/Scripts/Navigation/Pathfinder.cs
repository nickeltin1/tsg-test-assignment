using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Scripts.Navigation
{
    /// <summary>
    /// A* for a hex grid
    /// </summary>
    public class Pathfinder
    {
        public static class Profiling
        {
            public static ProfilerMarker Search = new("Pathfinder.Search");
            public static ProfilerMarker EnsureBuffers = new("EnsureBuffers");
            public static ProfilerMarker ReconstructPath = new("ReconstructPath");
            public static ProfilerMarker OpenNodesEnqueue = new("OpenNodesEnqueue");
            public static ProfilerMarker PushPointsToPath = new("PushPointsToPath");
            
        }
        
        public class SearchTask
        {
            public Task Task;
            public SearchState State;

            public bool IsCompleted => (State == SearchState.Failed
                                        || State == SearchState.Cancelled
                                        || State == SearchState.Completed)
                                       && Task.IsCompleted;

            public Action<Vector2Int> OnNodeInspected;
            public Action<SearchState> OnSearchEnded;

            public readonly CancellationTokenSource CancellationTokenSource;

            public SearchTask(CancellationToken cancellationToken)
            {
                CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            public void Cancel()
            {
                CancellationTokenSource.Cancel();
            }

            public void EndSearch(SearchState state)
            {
                State = state;
                OnSearchEnded?.Invoke(State);
            }
        }

        public enum SearchState
        {
            None,
            Searching,
            Cancelled,
            Completed,
            Failed
        }

        private enum NodeState : byte
        {
            Unseen = 0,
            Open = 1,
            Closed = 2
        }

        private struct Node
        {
            public float G;
            public int Parent;
            public NodeState State;
            public int Gen;
        }

        private Node[] _nodes;
        private int _gen;


        private float GetG(int index)
        {
            return _nodes[index].Gen == _gen ? _nodes[index].G : float.PositiveInfinity;
        }

        private void EnsureBuffers()
        {
            Profiling.EnsureBuffers.Begin();
            var n = _mapComponent.MapData.Length;
            if (_nodes == null || _nodes.Length != n)
                _nodes = new Node[n];
            Profiling.EnsureBuffers.End();
        }

        private readonly CancellationToken _lifecycleToken;
        private readonly MapComponent _mapComponent;
        private readonly Path _outputPath;
        private SearchTask _searchTask;
        private readonly SynchronizationContext _unityCtx;

        public MapData MapData => _mapComponent.MapData;

        public Pathfinder(MapComponent mapComponent, Path outputPath, CancellationToken lifecycleToken)//, bool yield)
        {
            _mapComponent = mapComponent;
            _outputPath = outputPath;
            _lifecycleToken = lifecycleToken;
        }

        public async Task<SearchTask> RequestSearchAsync(Vector2Int from, Vector2Int to)
        {
            if (_searchTask != null && !_searchTask.IsCompleted)
            {
                _searchTask.Cancel();
                try
                {
                    await _searchTask.Task;
                }
                catch
                {
                }
            }

            _searchTask = Search(from, to);
            return _searchTask;
        }

        private SearchTask Search(Vector2Int from, Vector2Int to)
        {
            var searchTask = new SearchTask(_lifecycleToken);
            var task = Search(from, to, searchTask);
            searchTask.Task = task;
            searchTask.State = SearchState.Searching;
            return searchTask;
        }

        private async Task Search(Vector2Int from, Vector2Int to, SearchTask searchTask)
        {
            Profiling.Search.Begin();
            // Map data is allocated once, since big map has like 40_000 nodes
            EnsureBuffers();
            
            // Not needed but for overflow, whatever
            unchecked
            {
                _gen++;
            }

            var start = MapData.XYToIndex(from);
            var goal = MapData.XYToIndex(to);

            
            var node = _nodes[start];
            node.Gen = _gen;
            node.G = 0f;
            node.Parent = -1;
            node.State = NodeState.Open;
            _nodes[start] = node;
            
            // Tuple for comparison, first f, then h
            var open = new PriorityQueue<int, (float f, float h)>();
            float h0 = HexPathfindingMath.Heuristic(from, to);
            open.Enqueue(start, (h0, h0));
            
            // _outputPath.Clear();
            

            while (open.Count > 0)
            {
                if (searchTask.CancellationTokenSource.IsCancellationRequested)
                {
                    searchTask.EndSearch(SearchState.Cancelled);
                    Profiling.Search.End();
                    return;//Task.CompletedTask;
                }

                var currentIndex = open.Dequeue();
                var currentNode = _nodes[currentIndex];

                // Skip either outdated nodes or closed
                if (currentNode.Gen != _gen || currentNode.State == NodeState.Closed)
                    continue;

                currentNode.State = NodeState.Closed;
                _nodes[currentIndex] = currentNode;

                var currentPosition = MapData[currentIndex].Position;
                searchTask.OnNodeInspected?.Invoke(currentPosition);

                // Path found, reconstructing
                if (currentIndex == goal)
                {
                    await ReconstructPath(currentIndex, searchTask.CancellationTokenSource.Token);
                    searchTask.EndSearch(SearchState.Completed);
                    Profiling.Search.End();
                    return;// Task.CompletedTask;
                }

                // Inspect neighbors
                foreach (var offset in HexPathfindingMath.GetCellNeighborOffsets(currentPosition))
                {
                    // if (_yieldTasks && (iterationsCount++ & 63) == 0) await Task.Yield();

                    if (searchTask.CancellationTokenSource.IsCancellationRequested)
                    {
                        searchTask.EndSearch(SearchState.Cancelled);
                        Profiling.Search.End();
                        return;// Task.CompletedTask;
                    }

                    var neighborPosition = currentPosition + offset;
                    if (!_mapComponent.MapData.Contains(neighborPosition)) continue;

                    var neighborIndex = _mapComponent.MapData.XYToIndex(neighborPosition);
                    if (!_mapComponent.MapData[neighborIndex].IsPassable) continue;

                    var neighborNode = _nodes[neighborIndex];
                        
                    if (neighborNode.Gen != _gen)
                    {
                        neighborNode.Gen = _gen;
                        neighborNode.G = float.PositiveInfinity;
                        neighborNode.Parent = -1;
                        neighborNode.State = NodeState.Unseen;
                    }

                    if (neighborNode.State == NodeState.Closed) continue;

                    // Uniform edge cost on hex grid: 1 per step
                    var tentativeG = GetG(currentIndex) + 1f;

                    // Not a better path
                    if (tentativeG >= neighborNode.G) continue; 

                    neighborNode.G = tentativeG;
                    neighborNode.Parent = currentIndex;
                    neighborNode.State = NodeState.Open;
                    _nodes[neighborIndex] = neighborNode;

                    float h = HexPathfindingMath.Heuristic(neighborPosition, to); 
                    var f = tentativeG + h;

                    using (Profiling.OpenNodesEnqueue.Auto())
                    {
                        open.Enqueue(neighborIndex, (f, h));
                    }
                }
            }

            // No path found
            searchTask.EndSearch(SearchState.Failed);

            Profiling.Search.End();
            return;// Task.CompletedTask;
        }

        private async Task ReconstructPath(int currentIndex, CancellationToken cancellationToken)
        {
            Profiling.ReconstructPath.Begin();
         
            var stack = new Stack<int>();
            for (var index = currentIndex; index != -1; index = _nodes[index].Parent) stack.Push(index);

            _outputPath.StartBuilding();
            while (stack.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _outputPath.StopBuilding();
                    _outputPath.Clear();
                    Profiling.ReconstructPath.End();
                    return;
                }

                var index = stack.Pop();
                var position = _mapComponent.MapData[index].Position;
                _outputPath.PushPoint(_mapComponent.CellToWorld(position));
                await Task.Yield();
            }

            _outputPath.StopBuilding();
            
            Profiling.ReconstructPath.End();
        }
    }
}