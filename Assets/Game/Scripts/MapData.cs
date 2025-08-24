using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

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
            public readonly MapData Map;
            
            public readonly TileType Type;
            
            public readonly int X;
            public readonly int Y;
            public readonly int Index;
            
            public Vector2Int Position => new(X, Y);
            
            public int WaterPrefabIndex;
            public int GroundPrefabIndex;
            public int DecorationPrefabIndex;
        
            public Tile(MapData map, TileType type, int x, int y, int index)
            {
                Map = map;
                Type = type;
                X = x;
                Y = y;
                Index = index;
                DecorationPrefabIndex = -1;
                WaterPrefabIndex = -1;
                GroundPrefabIndex = -1;
            }
        }
        
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Length => Width * Height;
        
        /// <summary>
        /// Using 1D array with 2D indexing as this is the most performant approach
        /// </summary>
        private List<Tile> _tiles;
        
        public IReadOnlyList<Tile> Tiles => _tiles;

        public readonly string Name;
        
        private MapData(string name) => Name = name;

        private void Init(int width, List<Tile> tiles)
        {
            Width = width;
            _tiles = tiles;
            Height = tiles.Count / width;
        }

        /// <summary>
        /// 2D to 1D conversion
        /// </summary>
        public int XYToIndex(int x, int y) => y * Width + x;
        public int XYToIndex(Vector2Int position) => XYToIndex(position.x, position.y);
        
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
        
        public void InitRandomTilesState(AddressableAssets.Assets assets, int seed, float decorChance = 0.3f)
        {
            var waterCount = assets.WaterTiles.LoadedObjects.Count;
            var groundCount = assets.GroundTiles.LoadedObjects.Count;
            var decorCount = assets.Decorations.LoadedObjects.Count;

            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                var hash = Hash(seed, tile.X, tile.Y);

                if (tile.Type == TileType.Water)
                {
                    tile.WaterPrefabIndex = (int)(hash % (uint)waterCount);
                }
                else
                {
                    tile.GroundPrefabIndex = (int)(hash % (uint)groundCount);

                    // deterministically choose if decor appears
                    if (decorCount > 0 && ((hash >> 8) % 10000) / 10000f < decorChance)
                    {
                        tile.DecorationPrefabIndex = (int)((hash >> 16) % (uint)decorCount);
                    }
                }

                _tiles[i] = tile;
            }
        }

        private static uint Hash(int seed, int x, int y)
        {
            unchecked
            {
                var h = (uint)seed;
                h ^= (uint)(x * 0x9E3779B1);
                h = (h << 13) | (h >> 19);
                h ^= (uint)(y * 0x85EBCA6B);
                h ^= h >> 16;
                h *= 0x7FEB352D;
                h ^= h >> 15;
                return h;
            }
        }

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
            var y = 0;
            var map = new MapData(name);
            while (reader.Peek() != -1)
            {
                var rowString = await reader.ReadLineAsync();
                
                // First row defines the grid width
                if (width <= 0) width = rowString.Length;
                
                if (width != rowString.Length) throw new Exception($"Inconsistent rows lenght. First row width {width}, row {y} width {rowString.Length}");

                for (var x = 0; x < rowString.Length; x++)
                {
                    var character = rowString[x];
                    var tileType = ParseTile(character);
                    var tile = new Tile(map, tileType, x, y, y * width + x);
                    tiles.Add(tile);
                }

                y++;
            }

            map.Init(width, tiles);
            return map;
        }

        /// <summary>
        /// Considering that single tile state encapsulated in single character, and holds only tile type for now
        /// </summary>
        public static TileType ParseTile(char character)
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

            return type;
        }
    }
}