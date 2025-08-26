using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    /// <summary>
    /// A* for a hex grid
    /// </summary>
    public class Pathfinder
    {
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

            public SearchTask()
            {
                CancellationTokenSource = new CancellationTokenSource();
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

        private struct NodeData
        {
            public float G;
            public int Parent;
            public NodeState State;
            public int Gen;
        }

        private NodeData[] _nodes;
        private int _gen;
        

        private float GetG(int index)
        {
            return _nodes[index].Gen == _gen ? _nodes[index].G : float.PositiveInfinity;
        }

        private void EnsureBuffers()
        {
            // Change this accessor to whatever your MapData exposes (Length/Count/TileCount, etc.)
            var n = _mapComponent.MapData.Length;
            if (_nodes == null || _nodes.Length != n)
                _nodes = new NodeData[n];
        }

        private readonly MapComponent _mapComponent;
        private readonly Path _outputPath;
        private SearchTask _searchTask;

        public Pathfinder(MapComponent mapComponent, Path outputPath)
        {
            _mapComponent = mapComponent;
            _outputPath = outputPath;
        }

        public async Task<SearchTask> RequestSearchAsync(Vector2Int from, Vector2Int to, bool yield)
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
                    /* cancelled or faulted */
                }
            }

            _searchTask = Search(from, to, yield);
            return _searchTask;
        }

        private SearchTask Search(Vector2Int from, Vector2Int to, bool yield)
        {
            var searchTask = new SearchTask();
            searchTask.Task = Search(from, to, searchTask, yield);
            searchTask.State = SearchState.Searching;
            return searchTask;
        }

        private async Task Search(Vector2Int from, Vector2Int to, SearchTask searchTask, bool yield)
        {
            EnsureBuffers();
            _gen++;

            var start = _mapComponent.MapData.XYToIndex(from);
            var goal = _mapComponent.MapData.XYToIndex(to);
            
            var node = _nodes[start];
            node.Gen = _gen;
            node.G = 0f;
            node.Parent = -1;
            node.State = NodeState.Open;
            _nodes[start] = node;

            // OPEN set, ordered by (f, then h) — lexicographic via ValueTuple comparer
            var open = new PriorityQueue<int, (float f, float h)>();
            var h0 = _mapComponent.DistanceBetweenCells(from, to);
            open.Enqueue(start, (h0, h0));

            _outputPath.StartBuilding();
            _outputPath.Clear();

            var iter = 0;

            try
            {
                while (open.Count > 0)
                {
                    if (yield && (iter++ & 63) == 0) await Task.Yield();

                    if (searchTask.CancellationTokenSource.IsCancellationRequested)
                    {
                        searchTask.EndSearch(SearchState.Cancelled);
                        _outputPath.StopBuilding();
                        return;
                    }

                    var currentIndex = open.Dequeue();
                    var currentNode = _nodes[currentIndex];

                    // Skip stale entries (already closed or from older gen)
                    if (currentNode.Gen != _gen || currentNode.State == NodeState.Closed)
                        continue;

                    currentNode.State = NodeState.Closed;
                    _nodes[currentIndex] = currentNode;

                    var currentPosition = _mapComponent.MapData[currentIndex].Position;
                    searchTask.OnNodeInspected?.Invoke(currentPosition);

                    // Goal reached — reconstruct final path once
                    if (currentIndex == goal)
                    {
                        var stack = new Stack<int>();
                        for (var index = currentIndex; index != -1; index = _nodes[index].Parent) stack.Push(index);

                        while (stack.Count > 0)
                        {
                            var index = stack.Pop();
                            var position = _mapComponent.MapData[index].Position;
                            _outputPath.PushPoint(_mapComponent.CellToWorld(position)); // returns float3
                        }

                        _outputPath.StopBuilding();
                        searchTask.EndSearch(SearchState.Completed);
                        return;
                    }

                    // Expand neighbors
                    foreach (var offset in HexPathfindingMath.GetCellNeighborOffsets(currentPosition))
                    {
                        if (yield && (iter++ & 63) == 0) await Task.Yield();

                        if (searchTask.CancellationTokenSource.IsCancellationRequested)
                        {
                            searchTask.EndSearch(SearchState.Cancelled);
                            _outputPath.StopBuilding();
                            return;
                        }

                        var neighborPosition = currentPosition + offset;
                        if (!_mapComponent.MapData.Contains(neighborPosition)) continue;

                        var neighborIndex = _mapComponent.MapData.XYToIndex(neighborPosition);

                        var tile = _mapComponent.MapData[neighborIndex];
                        if (!tile.IsPassable) continue;

                        var neighborNode = _nodes[neighborIndex];
                        
                        if (neighborNode.Gen != _gen)
                        {
                            neighborNode.Gen = _gen;
                            neighborNode.G = float.PositiveInfinity;
                            neighborNode.Parent = -1;
                            neighborNode.State = NodeState.Unseen;
                        }

                        if (neighborNode.State == NodeState.Closed)
                            continue;

                        var tentativeG = GetG(currentIndex) + _mapComponent.DistanceBetweenCells(currentPosition, neighborPosition);
                        if (tentativeG >= neighborNode.G) continue; // not a better path

                        neighborNode.G = tentativeG;
                        neighborNode.Parent = currentIndex;
                        neighborNode.State = NodeState.Open;
                        _nodes[neighborIndex] = neighborNode; 

                        var h = _mapComponent.DistanceBetweenCells(neighborPosition, to);
                        var f = tentativeG + h;

                        open.Enqueue(neighborIndex, (f, h));
                    }
                }

                // No path found
                _outputPath.StopBuilding();
                _outputPath.Clear();
                searchTask.EndSearch(SearchState.Failed);
            }
            finally
            {
                // Safe even if already stopped
                _outputPath.StopBuilding();
            }
        }
    }
}