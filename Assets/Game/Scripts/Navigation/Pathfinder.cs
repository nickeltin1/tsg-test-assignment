using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    /// <summary>
    /// A* for a hex grid
    /// </summary>
    public class Pathfinder
    {
        private readonly MapComponent _mapComponent;
        private readonly Path _outputPath;

        public Pathfinder(MapComponent mapComponent, Path outputPath)
        {
            _mapComponent = mapComponent;
            _outputPath = outputPath;
        }

        public void RequestSearch(Vector2Int from, Vector2Int to)
        {
            var path = HexPathfindingOld.FindPath(_mapComponent, from, to);
            _outputPath.Clear();
            _outputPath.AddPoints(path.Select(tile => (float3)_mapComponent.CellToWorld(tile.Position)));
        }
    }
}