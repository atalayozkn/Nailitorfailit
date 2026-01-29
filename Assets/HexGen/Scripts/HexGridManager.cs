using UnityEngine;
using System.Collections.Generic;

namespace ArminPrime
{
    public class HexGridManager : MonoBehaviour
    {
        [HideInInspector] public int width;
        [HideInInspector] public int height;
        [HideInInspector] public float hexSize;
        [HideInInspector] public BiomeData[] biomes;

        private BiomeData[,] biomeMap;
        private Dictionary<Vector2Int, int> tileHeights = new Dictionary<Vector2Int, int>();

        public void SetGridSettings(int w, int h, float size)
        {
            width = w;
            height = h;
            hexSize = size;
            biomeMap = new BiomeData[width, height];

            HexCanvasWindow.ShowCanvas(this);
        }

        public void SetBiome(int x, int y, BiomeData biome)
        {
            if (biome == null) return;
            biomeMap[x, y] = biome;
        }
        public void SetBiomes(BiomeData[] newBiomes)
        {
            biomes = newBiomes;
        }

        public void GenerateWorld()
        {
            if (biomeMap == null)
            {
                biomeMap = new BiomeData[width, height];
            }

            float xOffset = 2 * hexSize * (3f / 4f);
            float zOffset = Mathf.Sqrt(3) * hexSize;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float xPos = x * xOffset;
                    float zPos = z * zOffset;
                    if (x % 2 == 1) zPos += zOffset / 2f;

                    // **Fix: Use tile height, remove biomeHeight reference**
                    int heightLevel = tileHeights.ContainsKey(new Vector2Int(x, z)) ? tileHeights[new Vector2Int(x, z)] : 1;
                    Vector3 hexPosition = new Vector3(xPos, 0, zPos);

                    GameObject hex = new GameObject($"Hex {x},{z}");
                    hex.transform.position = hexPosition;
                    hex.transform.SetParent(transform);

                    HexMeshGenerator meshGen = hex.AddComponent<HexMeshGenerator>();
                    meshGen.hexSize = hexSize;

                    // **Fix: Call Initialize with correct arguments**
                    meshGen.Initialize(this, new Vector2Int(x, z));

                    if (biomeMap[x, z] == null)
                    {
                        if (biomes.Length > 0)
                        {
                            biomeMap[x, z] = biomes[0];
                        }
                        else
                        {
                            continue;
                        }
                    }

                    MeshRenderer renderer = hex.GetComponent<MeshRenderer>();
                    if (renderer == null)
                    {
                        renderer = hex.AddComponent<MeshRenderer>();
                    }

                    if (biomeMap[x, z]?.biomeMaterial != null)
                    {
                        renderer.sharedMaterial = biomeMap[x, z].biomeMaterial;
                    }
                    else
                    {
                        Debug.LogWarning($"Biome at ({x}, {z}) has no material assigned!");
                    }
                }
            }
        }

        public bool HasTile(Vector2Int position)
        {
            return tileHeights.ContainsKey(position);
        }

        public int GetTileHeight(Vector2Int position)
        {
            return tileHeights.ContainsKey(position) ? tileHeights[position] : 0; // Default to 0 if missing
        }

        public void SetTileHeight(int x, int y, int height)
        {
            tileHeights[new Vector2Int(x, y)] = height;
        }

        public float GetNeighborHeight(Vector2Int currentPos, int direction)
        {
            Vector2Int neighborPos = GetNeighborPosition(currentPos, direction);
            return tileHeights.ContainsKey(neighborPos) ? tileHeights[neighborPos] * hexSize : 0f;
        }

        private Vector2Int GetNeighborPosition(Vector2Int currentPos, int direction)
        {
            Vector2Int[] neighborOffsets = new Vector2Int[]
            {
            new Vector2Int(1, 0), // Right
            new Vector2Int(0, 1), // Top Right
            new Vector2Int(-1, 1), // Top Left
            new Vector2Int(-1, 0), // Left
            new Vector2Int(0, -1), // Bottom Left
            new Vector2Int(1, -1)  // Bottom Right
            };

            return currentPos + neighborOffsets[direction];
        }
    }

}
