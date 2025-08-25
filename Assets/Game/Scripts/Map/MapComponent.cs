using UnityEngine;

namespace Game.Scripts
{
    /// <summary>
    /// Represents physical representation of the map
    /// </summary>
    public class MapComponent : MonoBehaviour
    {
        [SerializeField] private Grid _grid;
        [SerializeField] private Transform _waterParent;
        [SerializeField] private Transform _groundParent;
        [SerializeField] private Transform _decorParent;
        
        public Transform DecorationsParent => _decorParent;
        public Transform GroundParent => _groundParent;
        public Transform WaterParent => _waterParent;

        public Vector3 CellSize => _grid.cellSize;
        
        public Vector3 CellToWorld(Vector2Int position)
        {
            return _grid.GetCellCenterWorld(new Vector3Int(position.x, position.y, 0));
        }

        public Vector2Int WorldToCell(Vector3 position)
        {
            var pos = _grid.WorldToCell(position);
            return new Vector2Int(pos.x, pos.y);
        }
    }
}