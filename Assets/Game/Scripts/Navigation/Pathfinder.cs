using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    /// <summary>
    /// A* for a hex grid, using .NET <see cref="System.Collections.Generic.PriorityQueue{T,T}"/>
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
        
        /// <summary>
        /// Search task can be async (for now async part is only path reconstruction)
        /// </summary>
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
            /// <summary>
            /// G
            /// </summary>
            public float CostFromStart;
            
            /// <summary>
            /// What node led to this node
            /// </summary>
            public int Parent;
            
            public NodeState State;
            
            public int Gen;
        }

        private Node[] _nodes;
        private int _gen;


        private float GetCostFromStart(int index)
        {
            return _nodes[index].Gen == _gen ? _nodes[index].CostFromStart : float.PositiveInfinity;
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
            _unityCtx = SynchronizationContext.Current;
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
            using (Profiling.Search.Auto())
            {
                var task = Search(from, to, searchTask);
                searchTask.Task = task;
                searchTask.State = SearchState.Searching;
            }
            return searchTask;
        }

        private async Task Search(Vector2Int from, Vector2Int to, SearchTask searchTask)
        {
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
            node.CostFromStart = 0f;
            node.Parent = -1;
            node.State = NodeState.Open;
            _nodes[start] = node;
            
            // Tuple for comparison, first f, then h
            // This ir Priority queue stolen from newer .NEW version
            var open = new PriorityQueue<int, (float f, float h)>();
            float h0 = HexPathfindingMath.Heuristic(from, to);
            open.Enqueue(start, (h0, h0));
            
            while (open.Count > 0)
            {
                if (searchTask.CancellationTokenSource.IsCancellationRequested)
                {
                    searchTask.EndSearch(SearchState.Cancelled);
                    return;
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
                    return;
                }

                // Inspect neighbors
                foreach (var offset in HexPathfindingMath.GetCellNeighborOffsets(currentPosition))
                {
                    if (searchTask.CancellationTokenSource.IsCancellationRequested)
                    {
                        searchTask.EndSearch(SearchState.Cancelled);
                        return;
                    }

                    var neighborPosition = currentPosition + offset;
                    if (!_mapComponent.MapData.Contains(neighborPosition)) continue;

                    var neighborIndex = _mapComponent.MapData.XYToIndex(neighborPosition);
                    if (!_mapComponent.MapData[neighborIndex].IsPassable) continue;

                    var neighborNode = _nodes[neighborIndex];
                        
                    if (neighborNode.Gen != _gen)
                    {
                        neighborNode.Gen = _gen;
                        neighborNode.CostFromStart = float.PositiveInfinity;
                        neighborNode.Parent = -1;
                        neighborNode.State = NodeState.Unseen;
                    }

                    if (neighborNode.State == NodeState.Closed) continue;

                    // Uniform edge cost on hex grid: 1 per step
                    var tentativeCostFromStart = GetCostFromStart(currentIndex) + 1f;

                    // Not a better path
                    if (tentativeCostFromStart >= neighborNode.CostFromStart) continue; 

                    neighborNode.CostFromStart = tentativeCostFromStart;
                    neighborNode.Parent = currentIndex;
                    neighborNode.State = NodeState.Open;
                    _nodes[neighborIndex] = neighborNode;

                    // H
                    float costToGoal = HexPathfindingMath.Heuristic(neighborPosition, to); 
                    // F
                    var totalCost = tentativeCostFromStart + costToGoal;

                    Profiling.OpenNodesEnqueue.Begin();
                    open.Enqueue(neighborIndex, (totalCost, costToGoal));
                    Profiling.OpenNodesEnqueue.End();
                }
            }

            // No path found
            searchTask.EndSearch(SearchState.Failed);
        }

        /// <summary>
        /// If updating path all at once it will take about 70ms with around 900 nodes path.
        /// This is considerable spike, so spreading out actual path reconstruction (and pushing to spline) across frames.
        /// </summary>
        private async Task ReconstructPath(int currentIndex, CancellationToken cancellationToken)
        {
            // Can't use profiler markers with Task.Yield() since task can resume on different thread.
            // Profiling.ReconstructPath.Begin();
         
            var stack = new Stack<int>();
            for (var index = currentIndex; index != -1; index = _nodes[index].Parent) stack.Push(index);

            _outputPath.StartBuilding();
            while (stack.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _outputPath.StopBuilding();
                    _outputPath.Clear();
                    // Profiling.ReconstructPath.End();
                    return;
                }

                var index = stack.Pop();
                var position = _mapComponent.MapData[index].Position;
                Profiling.PushPointsToPath.Begin();
                _outputPath.PushPoint(_mapComponent.CellToWorld(position));
                Profiling.PushPointsToPath.End();
                await Task.Yield();
            }

            _outputPath.StopBuilding();
            
            // Profiling.ReconstructPath.End();
        }
    }
}