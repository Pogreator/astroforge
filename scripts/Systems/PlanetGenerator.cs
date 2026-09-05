using Godot;
using System;

public static class PlanetGenerator
{
    private const int CoreSize = 16;

    public static byte[] GenerateChunkData(Vector3I chunkPos, int seed)
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
}