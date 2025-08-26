using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
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
            
            public Action<Node> OnNodeInspected;
            public Action<SearchState> OnSearchEnded;
            
            public readonly CancellationTokenSource CancellationTokenSource;

            public SearchTask()
            {
                CancellationTokenSource = new CancellationTokenSource();
            }

            public void Cancel() => CancellationTokenSource.Cancel();
            
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
        
        public struct Node
        {
            public readonly bool IsValid;
            
            public readonly int Index;
            public readonly Vector2Int Position;

            public float DistanceFromStart;
            public float DistanceToGoal;
            
            public float TotalDistance => DistanceFromStart + DistanceToGoal;

            public Node(int index, Vector2Int position)
            {
                Index = index;
                Position = position;
                DistanceFromStart = 0;
                DistanceToGoal = 0;
                IsValid = true;
            }
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
                try { await _searchTask.Task; } catch { /* cancelled or faulted */ }
            }
            _searchTask = Search(from, to, yield);
            return _searchTask;
        }

        public SearchTask Search(Vector2Int from, Vector2Int to, bool yield)
        {
            
            var searchTask = new SearchTask();
            searchTask.Task = Search(from, to, searchTask, yield);
            searchTask.State = SearchState.Searching;
            return searchTask;
        }
        
        private async Task Search(Vector2Int from, Vector2Int to, SearchTask searchTask, bool yield)
        {
            // var path = HexPathfindingOld.FindPath(_mapComponent, from, to);
            // _outputPath.Clear();
            // _outputPath.AddPoints(path.Select(tile => (float3)_mapComponent.CellToWorld(tile.Position)));
            
            var destIndex = _mapComponent.MapData.XYToIndex(to);
            var startNode = CreateNode(from);
            startNode.DistanceToGoal = _mapComponent.DistanceBetweenCells(from, to);
            
            var path = new Stack<Node>();
            var onPath = new HashSet<int>(); 
            var closed = new HashSet<int>();
            
            path.Push(startNode);
            onPath.Add(startNode.Index);
            
            _outputPath.StartBuilding();
            _outputPath.PushPoint(GetWorldPosition(startNode));
            
            while (path.Count > 0)
            {
                var node = path.Peek();
                
                searchTask.OnNodeInspected?.Invoke(node);

                if (searchTask.CancellationTokenSource.IsCancellationRequested)
                {
                    searchTask.EndSearch(SearchState.Cancelled);
                    _outputPath.StopBuilding();
                    return;
                }
                
                // Path completed
                if (node.Index == destIndex)
                {
                    searchTask.EndSearch(SearchState.Completed);
                    _outputPath.StopBuilding();
                    return;
                }

                var neighborOffsets = HexPathfindingMath.GetCellNeighborOffsets(node.Position);
                Node bestNeighbor = default;
                bestNeighbor.DistanceToGoal = float.MaxValue;
                foreach (var offset in neighborOffsets)
                {
                    var neighborPosition = node.Position + offset;
                    
                    // Neighbor is not in map bounds
                    if (!_mapComponent.MapData.Contains(neighborPosition))
                        continue;
                    
                    var neighborIndex = _mapComponent.MapData.XYToIndex(neighborPosition);
                    
                    // Skin nodes that is already on path
                    if (onPath.Contains(neighborIndex))
                        continue;
                    
                    // This cell was already inspected and appeared to be a dead end
                    if (closed.Contains(neighborIndex))
                        continue;
                    
                    var tile = _mapComponent.MapData[neighborIndex];
                    if (!tile.IsPassable)
                        continue;
                    
                    var neighbor = CreateNode(neighborPosition);
                    
                    var distanceToNeighbor = _mapComponent.DistanceBetweenCells(node.Position, neighborPosition);
                    var distanceToGoal = _mapComponent.DistanceBetweenCells(to, neighborPosition);
                    
                    
                    neighbor.DistanceFromStart = node.DistanceFromStart + distanceToNeighbor;
                    neighbor.DistanceToGoal = distanceToGoal;
                    
                    
                    if (!bestNeighbor.IsValid
                        || neighbor.TotalDistance < bestNeighbor.TotalDistance
                        || (Mathf.Approximately(neighbor.TotalDistance, bestNeighbor.TotalDistance)
                            && neighbor.DistanceToGoal < bestNeighbor.DistanceToGoal))
                    {
                        bestNeighbor = neighbor;
                    }


                    if (yield) await Task.Yield();
                }

                // No neighbors found, dead end, backtrack
                if (!bestNeighbor.IsValid)
                {
                    path.Pop();
                    onPath.Remove(node.Index);
                    closed.Add(node.Index);
                    _outputPath.PopPoint();
                }
                else
                {
                    path.Push(bestNeighbor);
                    onPath.Add(bestNeighbor.Index);
                    _outputPath.PushPoint(GetWorldPosition(bestNeighbor));
                }
            }
            
            _outputPath.StopBuilding();
            _outputPath.Clear();
            searchTask.EndSearch(SearchState.Failed);
        }

        private float3 GetWorldPosition(Node node)
        {
            return _mapComponent.CellToWorld(node.Position);
        }
        
        private Node CreateNode(Vector2Int pos)
        {
            var tile = _mapComponent.MapData.GetTile(pos);
            return new Node(tile.Index, tile.Position);
        }
    }
}