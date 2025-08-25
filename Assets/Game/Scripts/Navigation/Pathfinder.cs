using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public class Pathfinder
    {
        private readonly MapData _mapData;
        private readonly MapComponent _mapComponent;
        private readonly Path _outputPath;

        public Pathfinder(MapData mapData, MapComponent mapComponent, Path outputPath)
        {
            _mapData = mapData;
            _mapComponent = mapComponent;
            _outputPath = outputPath;
        }

        public void RequestSearch(Vector2Int from, Vector2Int to)
        {
            var path = HexPathfindingOld.FindPath(_mapData, from, to);
            _outputPath.Clear();
            _outputPath.AddPoints(path.Select(tile => (float3)_mapComponent.CellToWorld(tile.Position)));
        }
    }
}