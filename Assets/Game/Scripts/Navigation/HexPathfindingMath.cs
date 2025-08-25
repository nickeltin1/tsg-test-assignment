using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public static class HexPathfindingMath
    {
        private static readonly Vector2Int[] OffsetsEven =
        {
            new(+1, 0), new(0, +1), new(-1, +1),
            new(-1, 0), new(-1, -1), new(0, -1)
        };

        private static readonly Vector2Int[] OffsetsOdd =
        {
            new(+1, 0), new(+1, +1), new(0, +1),
            new(-1, 0), new(0, -1), new(+1, -1)
        };

        public static Vector2Int[] GetCellNeighborOffsets(Vector2Int pos)
        {
            return pos.y % 2 == 0 ? OffsetsEven : OffsetsOdd;
        }

        // public static int Heuristic(Vector2Int a, Vector2Int b)
        // {
        //     var A = OddRowToCube(a.x, a.y);
        //     var B = OddRowToCube(b.x, b.y);
        //     var d = math.abs(A - B);
        //     return math.max(d.x, math.max(d.y, d.z));
        // }
        //
        // private static int3 OddRowToCube(int col, int row)
        // {
        //     var x = col - ((row - (row & 1)) >> 1);
        //     var z = row;
        //     var y = -x - z;
        //     return new int3(x, y, z);
        // }
    }
}