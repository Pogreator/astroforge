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

    public static VoxelData[] GenerateChunkDataPlanet(Vector3I chunkPos, Vector3 center, int size, int seed, float radius, int lodIndex)
    {
        int totalSize = size * size * size;        

        VoxelData[] voxels = new VoxelData[totalSize];
        byte[] voxelIso = new byte[totalSize];
        ushort[] voxelType = new ushort [totalSize];
        short[] voxelHealth = new short [totalSize];

        System.Array.Fill(voxelIso, (byte)0);

        float maxMountainHeight = 15.0f;
        float crustThickness = 2.0f;
        int step = (int)Mathf.Pow(2, lodIndex);

        int chunkXOffset = chunkPos.X * size;
        int chunkYOffset = chunkPos.Y * size;
        int chunkZOffset = chunkPos.Z * size;

        Vector3 chunkWorldCenter = new Vector3(chunkXOffset, chunkYOffset, chunkZOffset) + new Vector3(size / 2f, size / 2f, size / 2f);
        float distanceToChunk = (chunkWorldCenter - center).Length();
        float chunkRadius = (new Vector3(size, size, size) * 0.5f).Length();

        bool isoMaxxed = false;
        if (distanceToChunk - chunkRadius > radius + maxMountainHeight)
        {
            System.Array.Fill(voxelIso, (byte)0);
            isoMaxxed = true;
        }
        if (distanceToChunk + chunkRadius < radius - maxMountainHeight)
        {
            System.Array.Fill(voxelIso, (byte)255);
            isoMaxxed = true;
        }

        if (!isoMaxxed)
        {
            var noise = new FastNoiseLite();
            noise.Seed = seed;
            noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            noise.Frequency = 0.015f;

            for (int x = 0; x < size; x+=step)
            {
                int worldX = chunkXOffset + x;
                for (int y = 0; y < size; y+=step)
                {
                    int worldY = chunkYOffset + y;

                    for (int z = 0; z < size; z+=step)
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

                        for (int sx = 0; sx < step && (x + sx) < size; sx++)
                        {
                            int xStride = (x + sx) * size * size;
                            for (int sy = 0; sy < step && (y + sy) < size; sy++)
                            {
                                int yStride = (y + sy) * size;
                                for (int sz = 0; sz < step && (z + sz) < size; sz++)
                                {
                                    int index = xStride + yStride + (z + sz);
                                    voxelIso[index] = voxelValue;
                                }
                            }
                        }
                    }
                }
            }
        }

        System.Array.Fill(voxelType, (ushort)0);
        System.Array.Fill(voxelHealth, (short)100);

        for (int i = 0; i < totalSize; i++)
        {
            voxels[i] = new VoxelData
            {
                Iso = voxelIso[i],
                Type = voxelType[i],
                Health = voxelHealth[i]
            };

        }

        return voxels;
    }
}