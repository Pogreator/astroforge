using Godot;
using System;
using System.Runtime.InteropServices.Marshalling;

public static class PlanetGenerator
{
    private const int CoreSize = 16;

    public static byte[] GenerateChunkDataTest(Vector3I chunkPos, int seed)
    {
        byte[] voxelIso = new byte[CoreSize*CoreSize*CoreSize];

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
                    voxelIso[flatIndex] = finalVoxelValue;
                }
            }
        }

        return voxelIso;
    }

    public static byte[] GenerateChunkDataPlanet(Vector3I chunkPos, Vector3 center, int seed, float radius)
    {
        byte[] voxelIso = new byte[CoreSize*CoreSize*CoreSize];
        float maxMountainHeight = 15.0f;
        float crustThickness = 2.0f;

        int chunkXOffset = chunkPos.X * CoreSize;
        int chunkYOffset = chunkPos.Y * CoreSize;
        int chunkZOffset = chunkPos.Z * CoreSize;

        Vector3 chunkWorldCenter = new Vector3(chunkXOffset, chunkYOffset, chunkZOffset) + new Vector3(CoreSize / 2f, CoreSize / 2f, CoreSize / 2f);
        float distanceToChunk = (chunkWorldCenter - center).Length();
        float chunkRadius = (new Vector3(CoreSize, CoreSize, CoreSize) * 0.5f).Length();

        if (distanceToChunk - chunkRadius > radius + maxMountainHeight)
        {
            System.Array.Fill(voxelIso, (byte)0);
            return voxelIso;
        }
        if (distanceToChunk + chunkRadius < radius - maxMountainHeight)
        {
            System.Array.Fill(voxelIso, (byte)255);
            return voxelIso;
        }

        var noise = new FastNoiseLite();
        noise.Seed = seed;
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.Frequency = 0.015f;

        for (int x = 0; x < CoreSize; x++)
        {
            int worldX = chunkXOffset + x;
            int xStride = x * CoreSize * CoreSize;
            for (int y = 0; y < CoreSize; y++)
            {
                int worldY = chunkYOffset + y;
                int yStride = y * CoreSize;

                for (int z = 0; z < CoreSize; z++)
                {
                    int worldZ = chunkZOffset + z;

                    Vector3 voxelWorldPos = new Vector3(worldX, worldY, worldZ);
                    Vector3 toCenter = voxelWorldPos - center;
                    float distance = toCenter.Length();

                    Vector3 direction = distance > 0.0001f ? toCenter / distance : Vector3.Up;

                    float noiseValue = noise.GetNoise3Dv(direction * radius);
                    float terrainOffset = noiseValue * maxMountainHeight;

                    float density = distance - (radius + terrainOffset);

                    float normalizedDensity = Mathf.Clamp(density / crustThickness, -1.0f, 1.0f);
                    byte voxelValue = (byte)Mathf.RoundToInt((1.0f - normalizedDensity) * 127.5f);

                    int index = xStride + yStride + z;
                    voxelIso[index] = voxelValue;
                }
            }
        }

        return voxelIso;
    }
}