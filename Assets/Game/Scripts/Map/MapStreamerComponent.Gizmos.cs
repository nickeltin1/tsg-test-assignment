#if UNITY_EDITOR

using UnityEngine;

namespace Game.Scripts
{
    public sealed partial class MapStreamerComponent
    {
        [Header("Debug")]
        [Tooltip("Don't know why but its expensive af, turn off for proper")]
        [SerializeField] private bool _drawGizmos = false;
        
        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return; 
            if (_mapComponent == null || MapData == null) return;

            // full map outline (gray)
            DrawAreaWire(new RectInt(0, 0, MapData.Width, MapData.Height), new Color(0.6f, 0.6f, 0.6f, 1f));

            // current / last outlines
            DrawAreaWire(_currentStreamArea, new Color(1f, 0.9f, 0.2f, 1f)); // yellow
            if (_lastStreamArea != _currentStreamArea)
                DrawAreaWire(_lastStreamArea, new Color(0.2f, 0.9f, 1f, 1f)); // cyan

            // diffs
            if (_addedAreas != null)
            {
                foreach (var r in _addedAreas)
                    DrawAreaFilledThenWire(r, new Color(0.2f, 1f, 0.2f, 0.18f),
                        new Color(0.2f, 1f, 0.2f, 0.9f)); // green
            }

            if (_removedAreas != null)
            {
                foreach (var r in _removedAreas)
                    DrawAreaFilledThenWire(r, new Color(1f, 0.2f, 0.2f, 0.18f), new Color(1f, 0.2f, 0.2f, 0.9f)); // red
            }
        }

        private void DrawAreaFilledThenWire(RectInt area, Color fill, Color wire)
        {
            if (area.width <= 0 || area.height <= 0) return;

            GetAreaWorldBox(area, out var center, out var size, out var xyPlane);
            Gizmos.color = fill;
            Gizmos.DrawCube(center, SizeWithThickness(size, xyPlane));
            Gizmos.color = wire;
            Gizmos.DrawWireCube(center, SizeWithThickness(size, xyPlane));
        }

        private void DrawAreaWire(RectInt area, Color wire)
        {
            if (area.width <= 0 || area.height <= 0) return;

            GetAreaWorldBox(area, out var center, out var size, out var xyPlane);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(center, SizeWithThickness(size, xyPlane));
        }
        
        private void GetAreaWorldBox(RectInt r, out Vector3 center, out Vector3 size, out bool xyPlane)
        {
            if (r.width <= 0 || r.height <= 0)
            {
                center = Vector3.zero;
                size = Vector3.zero;
                xyPlane = true;
                return;
            }

            // Inclusive min/max tile cells (RectInt xMax/yMax are EXCLUSIVE)
            var minCell = new Vector2Int(r.xMin, r.yMin);
            var maxCell = new Vector2Int(r.xMax - 1, r.yMax - 1);

            // World centers of extreme tiles
            var c0 = _mapComponent.CellToWorld(minCell);
            var c1 = _mapComponent.CellToWorld(maxCell);
            center = (c0 + c1) * 0.5f;

            var cellSize = _mapComponent.CellSize;
            // Decide plane: 2D XY if z==0, else XZ
            xyPlane = Mathf.Approximately(cellSize.z, 0f);

            // World tile size (local cellSize scaled by transform)
            var scale = _mapComponent.transform.lossyScale;
            var tileW = Mathf.Abs(cellSize.x * scale.x);
            var tileH = xyPlane ? Mathf.Abs(cellSize.y * scale.y) : Mathf.Abs(cellSize.z * scale.z);

            // Span = distance between extreme centers + one tile size on that axis
            if (xyPlane)
            {
                var spanX = Mathf.Abs(c1.x - c0.x) + tileW;
                var spanY = Mathf.Abs(c1.y - c0.y) + tileH;
                size = new Vector3(spanX, spanY, 0f);
            }
            else
            {
                var spanX = Mathf.Abs(c1.x - c0.x) + tileW;
                var spanZ = Mathf.Abs(c1.z - c0.z) + tileH;
                size = new Vector3(spanX, 0f, spanZ);
            }
        }

        private static Vector3 SizeWithThickness(Vector3 size, bool xyPlane)
        {
            const float t = 0.03f; // small slab thickness for visibility
            return xyPlane
                ? new Vector3(size.x, size.y, t) // flat on XY
                : new Vector3(size.x, t, size.z); // flat on XZ
        }
    }
}

#endif