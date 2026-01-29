using UnityEngine;
using System.Collections.Generic;


namespace ArminPrime
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HexMeshGenerator : MonoBehaviour
    {
        private Mesh mesh;
        public float hexSize = 1f;
        private HexGridManager hexGridManager;
        private Vector2Int currentHexPosition;
        private int height;

        public float bevelSize = 0.05f; // Controls the bevel effect
        public float domeHeightFactor = 0.1f; // Controls the dome curvature

        public void Initialize(HexGridManager gridManager, Vector2Int hexPosition)
        {
            hexGridManager = gridManager;
            currentHexPosition = hexPosition;
            height = hexGridManager.GetTileHeight(hexPosition); // Fetch height from grid
            GenerateHexMesh(height);
        }


        private void ApplySmoothShading(Mesh mesh)
        {
            Vector3[] normals = new Vector3[mesh.vertexCount];

            // **Compute average normals to smooth the shading**
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.zero;
            }

            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                int i1 = mesh.triangles[i];
                int i2 = mesh.triangles[i + 1];
                int i3 = mesh.triangles[i + 2];

                Vector3 normal = Vector3.Cross(
                    mesh.vertices[i2] - mesh.vertices[i1],
                    mesh.vertices[i3] - mesh.vertices[i1]
                ).normalized;

                normals[i1] += normal;
                normals[i2] += normal;
                normals[i3] += normal;
            }

            // Normalize normals to get an averaged smooth effect
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normals[i].normalized;
            }

            mesh.normals = normals;
        }

        public void GenerateHexMesh(int height)
        {
            mesh = new Mesh();
            GetComponent<MeshFilter>().mesh = mesh;

            List<Color> colors = new List<Color>();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            Vector3[] outerCorners = new Vector3[6];
            Vector3[] beveledCorners = new Vector3[6];

            float centerHeight = height * hexSize + domeHeightFactor * hexSize;

            for (int i = 0; i < 6; i++)
            {
                float angle = 60 * i * Mathf.Deg2Rad;
                Vector3 outerCorner = new Vector3(Mathf.Cos(angle) * hexSize, height * hexSize, Mathf.Sin(angle) * hexSize);
                Vector3 beveledCorner = Vector3.Lerp(outerCorner, new Vector3(0, centerHeight, 0), bevelSize);

                outerCorners[i] = outerCorner;
                beveledCorners[i] = beveledCorner;
            }

            vertices.Add(new Vector3(0, centerHeight, 0));
            uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(Color.white);

            for (int i = 0; i < 6; i++)
            {
                vertices.Add(beveledCorners[i]);
                vertices.Add(outerCorners[i]);

                // Beveled edges - slightly darker
                uvs.Add(new Vector2(0.5f + Mathf.Cos(60 * i * Mathf.Deg2Rad) * 0.3f,
                                    0.5f + Mathf.Sin(60 * i * Mathf.Deg2Rad) * 0.3f));

                // Outer edges - adjust UV to use a darker part of the texture
                uvs.Add(new Vector2(0.5f + Mathf.Cos(60 * i * Mathf.Deg2Rad) * 0.8f,
                                    0.5f + Mathf.Sin(60 * i * Mathf.Deg2Rad) * 0.8f - 0.2f));

                colors.Add(Color.black * 0.6f);
                colors.Add(Color.white);
            }

            for (int i = 0; i < 6; i++)
            {
                int inner = i * 2 + 1;
                int outer = i * 2 + 2;
                int nextInner = ((i + 1) % 6) * 2 + 1;
                int nextOuter = ((i + 1) % 6) * 2 + 2;

                triangles.Add(0);
                triangles.Add(nextInner);
                triangles.Add(inner);

                triangles.Add(inner);
                triangles.Add(nextInner);
                triangles.Add(nextOuter);

                triangles.Add(inner);
                triangles.Add(nextOuter);
                triangles.Add(outer);
            }

            float lowestGround = GetLowestNeighborHeight();

            for (int i = 0; i < 6; i++)
            {
                Vector3 lowerA = outerCorners[i];
                Vector3 lowerB = outerCorners[(i + 1) % 6];

                float neighborHeight = GetNeighborHeight(i);
                float lowestPoint = Mathf.Min(neighborHeight, lowestGround);

                if (neighborHeight == 0)
                {
                    lowestPoint = 0;
                }

                float finalWallHeight = (height * hexSize) - lowestPoint;
                if (finalWallHeight < hexSize)
                {
                    finalWallHeight = height * hexSize;
                }

                Vector3 upperA = lowerA + Vector3.down * finalWallHeight;
                Vector3 upperB = lowerB + Vector3.down * finalWallHeight;

                int baseIndex = vertices.Count;

                vertices.Add(lowerA);
                vertices.Add(lowerB);
                vertices.Add(upperA);
                vertices.Add(upperB);

                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));

                colors.Add(Color.gray * 0.4f);
                colors.Add(Color.gray * 0.4f);
                colors.Add(Color.gray * 0.4f);
                colors.Add(Color.gray * 0.4f);

                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);

                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();

            mesh.RecalculateNormals(); // Ensure basic normal calculation
            ApplySmoothShading(mesh);  // **Apply smooth shading**
            mesh.RecalculateTangents(); // Useful if you are using normal maps
        }
        // **Find Lowest Neighbor Height**
        private float GetLowestNeighborHeight()
        {
            float lowest = height * hexSize;

            for (int i = 0; i < 6; i++)
            {
                float neighborHeight = GetNeighborHeight(i);
                if (neighborHeight < lowest)
                {
                    lowest = neighborHeight;
                }
            }
            return Mathf.Min(lowest, 0);
        }

        // **Find Neighbor Height**
        private float GetNeighborHeight(int direction)
        {
            Vector2Int neighborPos = GetNeighborPosition(currentHexPosition, direction);
            if (hexGridManager != null && hexGridManager.HasTile(neighborPos))
            {
                return hexGridManager.GetTileHeight(neighborPos) * hexSize;
            }
            return 0f;
        }

        // **Find Neighbor Position in Hex Grid**
        private Vector2Int GetNeighborPosition(Vector2Int currentPos, int direction)
        {
            Vector2Int[] neighborOffsets = new Vector2Int[]
            {
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(1, -1)
            };

            return currentPos + neighborOffsets[direction];
        }
    }

}
