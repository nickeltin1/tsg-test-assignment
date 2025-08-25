using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    /// <summary>
    /// A* pathfinding on an odd-r (row-offset) hex grid.
    /// Single per-node state array (NodeState[]) for performance & locality.
    /// Heuristic is cube distance computed with int3 (integer-exact).
    /// </summary>
    public static class HexPathfindingOld
    {
        [Flags]
        private enum NodeFlags : byte
        {
            None = 0,
            InOpen = 1 << 0,
            Closed = 1 << 1
        }

        private struct NodeState
        {
            public float G; // cost from start
            public float F; // G + heuristic
            public int CameFrom; // predecessor index
            public NodeFlags Flags; // InOpen / Closed
        }

        public static List<MapData.Tile> FindPath(MapComponent mapComponent, Vector2Int start, Vector2Int goal)
        {
            var map = mapComponent.MapData;
            if (!map.Contains(start) || !map.Contains(goal)) return null;
            if (!map.GetTile(start).IsPassable || !map.GetTile(goal).IsPassable) return null;
            
            var length = map.Length;

            var startIndex = map.XYToIndex(start);
            var goalIndex = map.XYToIndex(goal);
            

            // Single state array
            var state = new NodeState[length];
            for (var i = 0; i < length; i++)
            {
                state[i].G = float.PositiveInfinity;
                state[i].F = float.PositiveInfinity;
                state[i].CameFrom = -1;
                state[i].Flags = NodeFlags.None;
            }

            state[startIndex].G = 0;
            state[startIndex].F = mapComponent.DistanceBetweenCells(start, goal);

            var openHeap = new MinHeap(length, state);
            openHeap.Push(startIndex);
            state[startIndex].Flags |= NodeFlags.InOpen;

            while (openHeap.Count > 0)
            {
                var current = openHeap.Pop();
                state[current].Flags &= ~NodeFlags.InOpen;

                if (current == goalIndex)
                    return ReconstructPath(map, state, current);

                state[current].Flags |= NodeFlags.Closed;

                map.IndexToXY(current, out var position);
                
                foreach (var neighborOffset in HexPathfindingMath.GetCellNeighborOffsets(position))
                {
                    var neighbor = position + neighborOffset;
                    
                    if (!map.Contains(neighbor)) continue;
                        
                    var nIndex = map.XYToIndex(neighbor);

                    if ((state[nIndex].Flags & NodeFlags.Closed) != 0)
                        continue;

                    var tile = map[neighbor];
                    if (!tile.IsPassable)
                        continue;

                    var tentative = state[current].G + 1; // uniform step cost
                    if (tentative < state[nIndex].G)
                    {
                        state[nIndex].CameFrom = current;
                        state[nIndex].G = tentative;
                        state[nIndex].F = tentative + mapComponent.DistanceBetweenCells(neighbor, goal);

                        if ((state[nIndex].Flags & NodeFlags.InOpen) == 0)
                        {
                            openHeap.Push(nIndex);
                            state[nIndex].Flags |= NodeFlags.InOpen;
                        }
                        else
                        {
                            openHeap.UpdateKey(nIndex);
                        }
                    }
                }
            }

            return null; // no path
        }

        private static List<MapData.Tile> ReconstructPath(MapData map, NodeState[] state, int current)
        {
            var rev = new List<int>(64);
            while (current != -1)
            {
                rev.Add(current);
                current = state[current].CameFrom;
            }

            rev.Reverse();
            var path = new List<MapData.Tile>(rev.Count);
            for (var i = 0; i < rev.Count; i++)
                path.Add(map[rev[i]]);
            return path;
        }

        private sealed class MinHeap
        {
            private readonly int[] _heap; // indices into node/state arrays
            private readonly int[] _pos; // nodeIndex -> heap position, -1 if not in heap
            private readonly NodeState[] _state; // reference to node states (for F keys)
            private int _count;

            public int Count => _count;

            public MinHeap(int capacity, NodeState[] state)
            {
                _heap = new int[capacity];
                _pos = new int[capacity];
                Array.Fill(_pos, -1);
                _state = state;
                _count = 0;
            }

            public void Push(int idx)
            {
                if (_count >= _heap.Length)
                {
                    Debug.LogError("MinHeap overflow. Ensure capacity == map.Length.");
                    return;
                }

                _heap[_count] = idx;
                _pos[idx] = _count;
                SiftUp(_count++);
            }

            public int Pop()
            {
                var root = _heap[0];
                _pos[root] = -1;

                var last = _heap[--_count];
                if (_count > 0)
                {
                    _heap[0] = last;
                    _pos[last] = 0;
                    SiftDown(0);
                }

                return root;
            }

            public void UpdateKey(int idx)
            {
                var p = _pos[idx];
                if (p < 0) return;
                SiftUp(p);
                SiftDown(p);
            }

            private bool Less(int aIndex, int bIndex)
            {
                return _state[aIndex].F < _state[bIndex].F;
            }

            private void SiftUp(int i)
            {
                while (i > 0)
                {
                    var parent = (i - 1) >> 1;
                    if (!Less(_heap[i], _heap[parent])) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            private void SiftDown(int i)
            {
                while (true)
                {
                    var l = i * 2 + 1;
                    var r = l + 1;
                    var smallest = i;

                    if (l < _count && Less(_heap[l], _heap[smallest])) smallest = l;
                    if (r < _count && Less(_heap[r], _heap[smallest])) smallest = r;

                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                var ia = _heap[a];
                var ib = _heap[b];
                _heap[a] = ib;
                _heap[b] = ia;
                _pos[ia] = b;
                _pos[ib] = a;
            }
        }
    }
}