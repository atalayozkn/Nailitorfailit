using System;
using System.Collections.Generic;

namespace ArminPrime
{
    [Serializable]
    public class HexMapData
    {
        public int width;
        public int height;
        public List<HexCellData> cells = new List<HexCellData>();
        public List<string> biomeNames = new List<string>(); // Store biome names
    }

    [Serializable]
    public class HexCellData
    {
        public int x;
        public int y;
        public string biomeName;
        public int height; // Now stored separately
    }
}

