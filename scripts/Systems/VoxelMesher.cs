using Godot;
using System;
using System.Collections.Generic;

public class MeshData
{
    public List<Vector3> Verticies { get; set; } = new();
    public List<int> Triangles { get; set; } = new();
    public List<Vector3> Normals { get; set; } = new();
}

public static class VoxelMesher
{
    private const int CoreSize = 16;
    private const int PaddedSize = 18;

    private static int GetPaddedIndex(int x, int y, int z) => (x*PaddedSize*PaddedSize) + (y*PaddedSize) + z;

    private static Vector3 CalculateSmoothNormal(byte[] paddedData, int x, int y, int z)
    {
        int xLeft  = GetPaddedIndex(Mathf.Clamp(x - 1, 0, PaddedSize - 1), y, z);
        int xRight = GetPaddedIndex(Mathf.Clamp(x + 1, 0, PaddedSize - 1), y, z);

        int yLeft  = GetPaddedIndex(x, Mathf.Clamp(y - 1, 0, PaddedSize - 1), z);
        int yRight = GetPaddedIndex(x, Mathf.Clamp(y + 1, 0, PaddedSize - 1), z);

        int zLeft  = GetPaddedIndex(x, y, Mathf.Clamp(z - 1, 0, PaddedSize - 1));
        int zRight = GetPaddedIndex(x, y, Mathf.Clamp(z + 1, 0, PaddedSize - 1));

        float gradX = paddedData[xRight] - paddedData[xLeft];
        float gradY = paddedData[yRight] - paddedData[yLeft];
        float gradZ = paddedData[zRight] - paddedData[zLeft];

        Vector3 normal = new Vector3(-gradX, -gradY, -gradZ);
        
        return normal.IsZeroApprox() ? Vector3.Up : normal.Normalized();
    }


    // Helper mappings to identify which corners form each edge (0-11)
    private static int GetStartCornerIndex(int edge) => edge switch {
        0=>0, 1=>1, 2=>3, 3=>0, 4=>4, 5=>5, 6=>7, 7=>4, 8=>0, 9=>1, 10=>2, 11=>3, _=>0
    };
    private static int GetEndCornerIndex(int edge) => edge switch {
        0=>1, 1=>2, 2=>2, 3=>3, 4=>5, 5=>6, 6=>6, 7=>7, 8=>4, 9=>5, 10=>6, 11=>7, _=>0
    };

    // Requires padded data so that it can smoothly transition ( 18x18x18 to 16x16x16 )
    public static MeshData GenerateMarchingCubes(byte[] paddedVoxelData, int isolevel, int lodIndex)
    {
        var meshData = new MeshData();
        int vertexIndexCounter = 0;

        int step = (int)Mathf.Pow(2, lodIndex);
        
        for (int x = 1; x <= CoreSize; x+=step)
        {
            for (int y = 1; y <= CoreSize; y+=step)
            {
                for (int z = 1; z <= CoreSize; z+=step)
                {
                    // Evaluate density of 8 corners from data array
                    int cubeIndex = 0;
                    byte[] cubeValues = new byte[8];

                    for (int i = 0; i < 8; i ++)
                    {
                        Vector3I offset = MarchingTable.Corners[i];

                        int sampleX = x + (offset.X*step);
                        int sampleY = y + (offset.Y*step);
                        int sampleZ = z + (offset.Z*step);

                        int sampleIndex = GetPaddedIndex(sampleX, sampleY, sampleZ);
                        cubeValues[i] = paddedVoxelData[sampleIndex];

                        if (cubeValues[i] >= isolevel)
                        {
                            cubeIndex |= (1 << i);
                        }
                    }

                    if (cubeIndex == 0 || cubeIndex == 255) continue;

                    Vector3[] cornerNormals = new Vector3[8];
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3I offset = MarchingTable.Corners[i] * step;
                        cornerNormals[i] = CalculateSmoothNormal(paddedVoxelData, x + offset.X, y + offset.Y, z + offset.Z);
                    }
                    
                    // Build list of local edge verticies for cell
                    Vector3 cellOrigin = new Vector3(x - 1, y - 1, z - 1);
                    Vector3[] localEdgeVertices = new Vector3[12];
                    Vector3[] localEdgeNormals = new Vector3[12];

                    for (int i = 0; i < 12; i++)
                    {
                        Vector3 edgeStart = MarchingTable.Edges[i,0];
                        Vector3 edgeEnd = MarchingTable.Edges[i,1];

                        int cornerIdxStart = GetStartCornerIndex(i);
                        int cornerIdxEnd = GetEndCornerIndex(i);

                        byte valStart = cubeValues[cornerIdxStart];
                        byte valEnd = cubeValues[cornerIdxEnd];

                        int cornerA = i;
                        float t = 0.5f; // Interpolation level
                        if (valEnd != valStart)
                        {
                            t = (float)(isolevel - valStart) / (float)(valEnd - valStart);
                        }

                        Vector3 scaledStart = edgeStart * step;
                        Vector3 scaledEnd = edgeEnd * step;
    
                        localEdgeVertices[i] = cellOrigin + scaledStart + ((scaledEnd - scaledStart) * t);

                        Vector3 normalStart = cornerNormals[cornerIdxStart];
                        Vector3 normalEnd = cornerNormals[cornerIdxEnd];

                        localEdgeNormals[i] = normalStart.Lerp(normalEnd,t).Normalized();
                    }

                    // Itterate triangulation table to create faces

                    for (int i = 0; i < 16; i+=3)
                    {
                        int edge0 = MarchingTable.Triangles[cubeIndex, i];
                        if (edge0 == -1) break; // Finished processing all triangles

                        int edge1 = MarchingTable.Triangles[cubeIndex, i+1];
                        int edge2 = MarchingTable.Triangles[cubeIndex, i+2];

                        // Append edges to 3d mesh
                        meshData.Verticies.Add(localEdgeVertices[edge0]);
                        meshData.Verticies.Add(localEdgeVertices[edge1]);
                        meshData.Verticies.Add(localEdgeVertices[edge2]);

                        // Append normal data to 3d mesh
                        meshData.Normals.Add(localEdgeNormals[edge0]);
                        meshData.Normals.Add(localEdgeNormals[edge1]);
                        meshData.Normals.Add(localEdgeNormals[edge2]);

                        // Map sequential indices
                        meshData.Triangles.Add(vertexIndexCounter);
                        meshData.Triangles.Add(vertexIndexCounter+1);
                        meshData.Triangles.Add(vertexIndexCounter+2);

                        vertexIndexCounter += 3;    
                    }
                }
            }
        }
        
        return meshData;
    }
}