using Godot;
using System;
using System.Runtime.InteropServices.Marshalling;

public static class PlanetGenerator
{
    private const int CoreSize = 16;

    public static byte[] GenerateChunkDataTest(Vector3I chunkPos, int seed)
    {
        byte[] voxelData = new byte[CoreSize*CoreSize*CoreSize];

        var noise = new FastNoiseLite();
        noise.Seed = seed;
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        noise.Frequency = 0.015f;

        for (int x = 0; x < CoreSize; x++)
        {
            int worldX = (chunkPos.X * CoreSize) + x;
            for (int y = 0; y < CoreSize; y++)
            {
                int worldY = (chunkPos.Y * CoreSize) + y;
                for (int z = 0; z < CoreSize; z++)
                {
                    int worldZ = (chunkPos.Z * CoreSize) + z;

                    float noise3D = noise.GetNoise3D(worldX, worldY, worldZ);
                    float normalizedNoise = (noise3D + 1f) * 0.5f;
                    float scaledDensity = normalizedNoise * 255f;
                    
                    byte finalVoxelValue = (byte)Mathf.Clamp(scaledDensity, 0, 255);
                    int flatIndex = (x*CoreSize*CoreSize) + (y*CoreSize) + z;
                    voxelData[flatIndex] = finalVoxelValue;
                }
            }
        }

        return voxelData;
    }

    public static byte[] GenerateChunkDataPlanet(Vector3I chunkPos, Vector3 center, int seed, float radius)
    {
        byte[] voxelData = new byte[CoreSize*CoreSize*CoreSize];

        var noise = new FastNoiseLite();
        noise.Seed = seed;
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.Frequency = 0.015f;

        for (int x = 0; x < CoreSize; x++)
        {
            int worldX = (chunkPos.X * CoreSize) + x;
            for (int y = 0; y < CoreSize; y++)
            {
                int worldY = (chunkPos.Y * CoreSize) + y;
                for (int z = 0; z < CoreSize; z++)
                {
                    int worldZ = (chunkPos.Z * CoreSize) + z;

                    Vector3 voxelWorldPos = new Vector3(worldX, worldY, worldZ);

                    Vector3 toCenter = voxelWorldPos - center;
                    float distance = toCenter.Length();

                    Vector3 direction = distance > 0.0001f ? toCenter.Normalized() : Vector3.Up;

                    float noiseValue = noise.GetNoise3Dv(direction * radius);
                    float maxMountainHeight = 15.0f;
                    float terrainOffset = noiseValue * maxMountainHeight;

                    float density = distance - (radius + terrainOffset);

                    float crustThickness = 2.0f;
                    float normalizedDensity = Mathf.Clamp(density / crustThickness, -1.0f, 1.0f);

                    byte voxelValue = (byte)Mathf.RoundToInt((1.0f - normalizedDensity) * 127.5f);

                    int index = (x * CoreSize * CoreSize) + (y * CoreSize) + z;
                    voxelData[index] = voxelValue;
                }
            }
        }

        return voxelData;
    }
}