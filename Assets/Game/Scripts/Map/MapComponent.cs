using System.Threading.Tasks;
using UnityEngine;

namespace Game.Scripts
{
    /// <summary>
    /// Represents physical representation of the map
    /// </summary>
    public class MapComponent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Grid _grid;
        [SerializeField] private Transform _waterParent;
        [SerializeField] private Transform _groundParent;
        [SerializeField] private Transform _decorParent;

        [Header("Map data")]
        [SerializeField] private string _mapName = "maze";
        [SerializeField, Range(0,1)] private float _decorationSpawnChance = 0.5f;
        [SerializeField] private int _seed = 0;

        private AddressableAssets.Assets _assets;
        
        public MapData MapData { get; private set; }

        public async Task Init(AddressableAssets.Assets assets)
        {
            _assets = assets;
            MapData = _assets.Maps.FindMapByName(_mapName);
            MapData.InitRandomTilesState(_assets, _seed, _decorationSpawnChance);
            await Task.CompletedTask;
        }
        
        public Transform DecorationsParent => _decorParent;
        public Transform GroundParent => _groundParent;
        public Transform WaterParent => _waterParent;

        public Vector3 CellSize => _grid.cellSize;
        
        public Vector3 CellToWorld(Vector2Int position)
        {
            // Y is inverted since map is loaded from file top to bottom
            return _grid.GetCellCenterWorld(new Vector3Int(position.x, -position.y, 0));
        }

        public Vector2Int WorldToCell(Vector3 position)
        {
            var pos = _grid.WorldToCell(position);
            // Y is inverted since map is loaded from file top to bottom
            return new Vector2Int(pos.x, -pos.y);
        }
    }
}