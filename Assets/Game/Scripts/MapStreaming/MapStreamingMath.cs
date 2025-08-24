using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts
{
    /// <summary>
    /// For map streaming I chose rect overlying to calculate which parts of the map needs to loaded and which unloaded.
    /// Calculation for the areas is pretty simple. 
    /// </summary>
    public static class MapStreamingMath
    {
		public static RectInt CalculateActiveArea(Vector2Int center, int radius, int mapWidth, int mapHeight)
        {
            var xMin = Mathf.Max(0, center.x - radius);
            var yMin = Mathf.Max(0, center.y - radius);
            var xMax = Mathf.Min(mapWidth - 1, center.x + radius);
            var yMax = Mathf.Min(mapHeight - 1, center.y + radius);
            return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        public static void CalculateAreaDifference(RectInt oldArea, RectInt newArea, List<RectInt> addedAreas, List<RectInt> removedAreas)
        {
            addedAreas.Clear();
            removedAreas.Clear();

            // Areas is the same
            if (oldArea.Equals(newArea))
                return;

            // If there is no overlap, then two rects is completely different
            if (!oldArea.Overlaps(newArea))
            {
                TryAddNonEmpty(addedAreas, newArea);
                TryAddNonEmpty(removedAreas, oldArea);
                return;
            }
            
            var intersection = CalculateAreaIntersection(oldArea, newArea);
            BuildDifferenceAreas(newArea, intersection, addedAreas);
            BuildDifferenceAreas(oldArea, intersection, removedAreas);
        }

        public static RectInt CalculateAreaIntersection(RectInt a, RectInt b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);

            if (xMin < xMax && yMin < yMax)
                return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);

            return new RectInt(0, 0, 0, 0);
        }
        
        private static void BuildDifferenceAreas(RectInt whole, RectInt cutout, List<RectInt> outRects)
        {
            if (whole.width <= 0 || whole.height <= 0)
                return;

            // If there's no actual intersection, the "cutout" contributes nothing; return whole.
            if (!whole.Overlaps(cutout))
            {
                outRects.Add(whole);
                return;
            }

            var inter = CalculateAreaIntersection(whole, cutout);
            if (inter.width <= 0 || inter.height <= 0)
            {
                outRects.Add(whole);
                return;
            }

            // Left strip
            TryAddNonEmpty(outRects, new RectInt(
                whole.xMin, whole.yMin,
                inter.xMin - whole.xMin,
                whole.height));

            // Right strip
            TryAddNonEmpty(outRects, new RectInt(
                inter.xMax, whole.yMin,
                whole.xMax - inter.xMax,
                whole.height));

            // Bottom strip
            TryAddNonEmpty(outRects, new RectInt(
                inter.xMin, whole.yMin,
                inter.width,
                inter.yMin - whole.yMin));

            // Top strip
            TryAddNonEmpty(outRects, new RectInt(
                inter.xMin, inter.yMax,
                inter.width,
                whole.yMax - inter.yMax));
        }

        private static void TryAddNonEmpty(List<RectInt> list, RectInt r)
        {
            if (r.width > 0 && r.height > 0) list.Add(r);
        }
    }
}