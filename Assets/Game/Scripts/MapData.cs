using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Game.Scripts
{
    /// <summary>
    /// Holds loaded map state, later on can be extended to read-write map state
    /// if any user map edit features is introduced
    /// </summary>
    public class MapData
    {
        /// <summary>
        /// For now there are only 2 types
        /// </summary>
        public enum TileType : byte
        {
            Water = 0, 
            Ground = 1
        }

        /// <summary>
        /// Tile might contain additional information except of the type later on
        /// </summary>
        public struct Tile
        {
            public readonly TileType Type;
        
            public Tile(TileType type)
            {
                Type = type;
            }
        }
        
        public readonly int Width;
        public readonly int Height;
        public int Length => Width * Height;
        
        /// <summary>
        /// Using 1D array with 2D indexing as this is the most performant approach
        /// </summary>
        private readonly List<Tile> _tiles;
        
        public IReadOnlyList<Tile> Tiles => _tiles;

        public readonly string Name;
        
        private MapData(int width, List<Tile> tiles, string name)
        {
            Width = width;
            _tiles = tiles;
            Name = name;
            Height = tiles.Count / width;
        }

        /// <summary>
        /// 2D to 1D conversion
        /// </summary>
        public int XYToIndex(int x, int y) => y * Width + x;
        
        public void IndexToXY(int index, out int x, out int y)
        {
            y = index / Width;
            x = index % Width;
        }

        public Tile GetTile(int x, int y)
        {
            if (x >= Width || y >= Height) throw new IndexOutOfRangeException($"Coordinates ({x},{y}) are out of map bounds {Width}x{Height}");
            return _tiles[XYToIndex(x,y)];
        }
        
        public Tile this[int x, int y] => GetTile(x, y);
        public Tile this[int index] => _tiles[index];

        /// <summary>
        /// Reads text file line by line and filling the map data.
        /// '0' = water
        /// '1' = ground
        /// All rows expected to be of the same width
        /// </summary>
        public static async Task<MapData> LoadAsync(StringReader reader, string name)
        {
            var tiles = new List<Tile>();
            var width = 0;
            
            // Reading row by row to spread out synchronous parts
            var rowCount = 0;
            while (reader.Peek() != -1)
            {
                var rowString = await reader.ReadLineAsync();
                
                // First row defines the grid width
                if (width <= 0) width = rowString.Length;
                
                if (width != rowString.Length) throw new Exception($"Inconsistent rows lenght. First row width {width}, row {rowCount} width {rowString.Length}");
                
                foreach (var character in rowString)
                {
                    var tile = ParseTile(character);
                    tiles.Add(tile);
                }
                rowCount++;
            }

            return new MapData(width, tiles, name);
        }

        /// <summary>
        /// Considering that single tile state encapsulated in single character
        /// </summary>
        public static Tile ParseTile(char character)
        {
            var type = TileType.Water;
            switch (character)
            {
                // water is default state, so doing nothing
                case '0':
                    break;
                case '1':
                    type = TileType.Ground;
                    break;
                default:
                    throw new Exception($"Unsupported tile type character {character}");
            }

            return new Tile(type);
        }
    }
}