using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public static class HexPathfindingOld
    {
        // A* over MapData using odd-r (row-offset) neighbors
        public static List<MapData.Tile> FindPath(MapData map, Vector2Int start, Vector2Int goal)
        {
            if (!map.Contains(start) || !map.Contains(goal)) return null;
            if (!map.GetTile(start).IsPassable || !map.GetTile(goal).IsPassable) return null;

            var width = map.Width;
            var height = map.Height;
            var length = map.Length;

            int StartIndex(int x, int y)
            {
                return y * width + x;
            }

            var startIndex = StartIndex(start.x, start.y);
            var goalIndex = StartIndex(goal.x, goal.y);

            // Node arrays
            var gScore = new int[length];
            var fScore = new int[length];
            var cameFrom = new int[length];
            var inOpen = new bool[length];
            var closed = new bool[length];

            const int INF = int.MaxValue / 4;
            for (var i = 0; i < length; i++)
            {
                gScore[i] = INF;
                fScore[i] = INF;
                cameFrom[i] = -1;
            }

            gScore[startIndex] = 0;
            fScore[startIndex] = Heuristic(start, goal);

            // Min-heap (index by map index, key by fScore)
            var openHeap = new MinHeap(length, fScore);
            openHeap.Push(startIndex);
            inOpen[startIndex] = true;

            while (openHeap.Count > 0)
            {
                var current = openHeap.Pop();
                inOpen[current] = false;

                if (current == goalIndex)
                    return ReconstructPath(map, cameFrom, current);

                closed[current] = true;
                map.IndexToXY(current, out var cx, out var cy);

                // Iterate neighbors (odd-r)
                foreach (var nb in NeighborsOddR(cx, cy, width, height))
                {
                    var nx = nb.x;
                    var ny = nb.y;
                    var nIndex = StartIndex(nx, ny);

                    if (closed[nIndex]) continue;

                    var tile = map[nx, ny];
                    if (!tile.IsPassable) continue;

                    var tentative = gScore[current] + 1; // uniform cost

                    if (tentative < gScore[nIndex])
                    {
                        cameFrom[nIndex] = current;
                        gScore[nIndex] = tentative;
                        fScore[nIndex] = tentative + Heuristic(new Vector2Int(nx, ny), goal);

                        if (!inOpen[nIndex])
                        {
                            openHeap.Push(nIndex);
                            inOpen[nIndex] = true;
                        }
                        else
                        {
                            openHeap.UpdateKey(nIndex);
                        }
                    }
                }
            }

            return null;
        }

        // Odd-R row-offset neighbors (flat-topped hexes)
        private static IEnumerable<Vector2Int> NeighborsOddR(int x, int y, int width, int height)
        {
            // even row neighbors
            Vector2Int[] even =
            {
                new(x + 1, y), new(x, y + 1), new(x - 1, y + 1),
                new(x - 1, y), new(x - 1, y - 1), new(x, y - 1)
            };

            // odd row neighbors
            Vector2Int[] odd =
            {
                new(x + 1, y), new(x + 1, y + 1), new(x, y + 1),
                new(x - 1, y), new(x, y - 1), new(x + 1, y - 1)
            };

            var dirs = (y & 1) == 0 ? even : odd;
            foreach (var p in dirs)
                if (p.x >= 0 && p.x < width && p.y >= 0 && p.y < height)
                    yield return p;
        }

        // Heuristic: cube distance for odd-r offset
        private static int Heuristic(in Vector2Int a, in Vector2Int b)
        {
            var Ax = OffsetOddRToCube(a.x, a.y);
            var Bx = OffsetOddRToCube(b.x, b.y);
            return CubeDistance(Ax, Bx);
        }

        private readonly struct Cube
        {
            public readonly int x, y, z;

            public Cube(int x, int y, int z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        // Convert odd-r offset (col=x, row=y) to cube coordinates
        private static Cube OffsetOddRToCube(int col, int row)
        {
            var x = col - ((row - (row & 1)) >> 1);
            var z = row;
            var y = -x - z;
            return new Cube(x, y, z);
        }

        private static int CubeDistance(Cube a, Cube b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z));
        }

        private static List<MapData.Tile> ReconstructPath(MapData map, int[] cameFrom, int current)
        {
            var rev = new List<int>(64);
            while (current != -1)
            {
                rev.Add(current);
                current = cameFrom[current];
            }

            rev.Reverse();
            var path = new List<MapData.Tile>(rev.Count);
            for (var i = 0; i < rev.Count; i++)
                path.Add(map[rev[i]]);
            return path;
        }

        private sealed class MinHeap
        {
            private readonly int[] _heap; // capacity = map.Length, never resized
            private int _count;
            private readonly int[] _pos; // mapIndex -> heap position, -1 if not in heap
            private readonly int[] _keyRef; // fScore reference

            public int Count => _count;

            // capacity must be map.Length (max number of nodes that could be open)
            public MinHeap(int capacity, int[] keyRef)
            {
                _heap = new int[capacity]; // no resize
                _pos = new int[capacity];
                for (var i = 0; i < capacity; i++) _pos[i] = -1;

                _keyRef = keyRef;
                _count = 0;
            }

            public void Push(int idx)
            {
                // Optional: guard (should never fire if capacity == map.Length)
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

            private void SiftUp(int i)
            {
                while (i > 0)
                {
                    var parent = (i - 1) >> 1;
                    if (_keyRef[_heap[i]] >= _keyRef[_heap[parent]]) break;
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
                    if (l < _count && _keyRef[_heap[l]] < _keyRef[_heap[smallest]]) smallest = l;
                    if (r < _count && _keyRef[_heap[r]] < _keyRef[_heap[smallest]]) smallest = r;
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