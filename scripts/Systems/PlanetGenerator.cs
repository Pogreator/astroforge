using Godot;
using System;
using System.Runtime.InteropServices.Marshalling;

public static class PlanetGenerator
{
    private const int CoreSize = 16;
    private const int PaddedSize = 18;

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

    public static VoxelData[] GenerateChunkDataPlanet(Vector3I chunkPos, Vector3 center, int seed, float radius, int scale)
    {
        int totalSize = CoreSize * CoreSize * CoreSize;        

        VoxelData[] voxels = new VoxelData[totalSize];
        byte[] voxelIso = new byte[totalSize];
        ushort[] voxelType = new ushort [totalSize];
        short[] voxelHealth = new short [totalSize];

        System.Array.Fill(voxelType, (byte)0);
        System.Array.Fill(voxelHealth, (byte)0);

        float maxMountainHeight = 15.0f;
        float crustThickness = 2.0f;

        int chunkXOffset = chunkPos.X * CoreSize * scale;
        int chunkYOffset = chunkPos.Y * CoreSize * scale;
        int chunkZOffset = chunkPos.Z * CoreSize * scale;

        Vector3 chunkWorldCenter = new Vector3(chunkXOffset, chunkYOffset, chunkZOffset) + new Vector3(CoreSize / 2f, CoreSize / 2f, CoreSize / 2f);
        float distanceToChunk = (chunkWorldCenter - center).Length();
        float chunkRadius = (new Vector3(CoreSize, CoreSize, CoreSize) * 0.5f).Length();

        if (distanceToChunk - chunkRadius > radius + maxMountainHeight)
        {
            System.Array.Fill(voxelIso, (byte)0);
            for (int i = 0; i < totalSize; i++)
            {
                voxels[i].Iso = voxelIso[i];
                voxels[i].Type = voxelType[i];
                voxels[i].Health = voxelHealth[i];
            }
            return voxels;
        }
        if (distanceToChunk + chunkRadius < radius - maxMountainHeight)
        {
            System.Array.Fill(voxelIso, (byte)255);
            for (int i = 0; i < totalSize; i++)
            {
                voxels[i].Iso = voxelIso[i];
                voxels[i].Type = voxelType[i];
                voxels[i].Health = voxelHealth[i];
            }
            return voxels;
        }

        var noise = new FastNoiseLite();
        noise.Seed = seed;
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.Frequency = 0.015f;

        for (int x = 0; x < CoreSize; x++)
        {
            int worldX = chunkXOffset + ((x - 1) * scale);
            int xStride = x * CoreSize * CoreSize;
            for (int y = 0; y < CoreSize; y++)
            {
                int worldY = chunkYOffset + ((y - 1) * scale);
                int yStride = y * CoreSize;

                for (int z = 0; z < CoreSize; z++)
                {
                    int worldZ = chunkZOffset + ((z - 1) * scale);

                    Vector3 voxelWorldPos = new Vector3(worldX, worldY, worldZ);
                    Vector3 toCenter = voxelWorldPos - center;
                    float distance = toCenter.Length();

                    Vector3 direction = distance > 0.0001f ? toCenter / distance : Vector3.Up;

                    float noiseValue = noise.GetNoise3Dv(direction * radius);
                    float terrainOffset = noiseValue * maxMountainHeight;

                    float density = distance - (radius + terrainOffset) - maxMountainHeight - crustThickness;

                    float normalizedDensity = Mathf.Clamp(density / crustThickness, -1.0f, 1.0f);
                    byte voxelValue = (byte)Mathf.RoundToInt((1.0f - normalizedDensity) * 127.5f);

                    int index = xStride + yStride + z;
                    voxelIso[index] = voxelValue;
                }
            }
        }

        for (int i = 0; i < totalSize; i++)
        {
            voxels[i].Iso = voxelIso[i];
            voxels[i].Type = voxelType[i];
            voxels[i].Health = voxelHealth[i];
        }

        return voxels;
    }

    public static VoxelData[] GeneratePaddedChunkDataPlanet(Vector3I chunkPos, Vector3 center, int seed, float radius, int scale)
    {
        int totalSize = PaddedSize * PaddedSize * PaddedSize;        

        VoxelData[] voxels = new VoxelData[totalSize];
        byte[] voxelIso = new byte[totalSize];

        float maxMountainHeight = 15.0f;
        float crustThickness = 2.0f;

        int chunkXOffset = (chunkPos.X * CoreSize) - 1;
        int chunkYOffset = (chunkPos.Y * CoreSize) - 1;
        int chunkZOffset = (chunkPos.Z * CoreSize) - 1;

        Vector3 localChunkCenter = new Vector3(chunkXOffset, chunkYOffset, chunkZOffset) + new Vector3(PaddedSize / 2f, PaddedSize / 2f, PaddedSize / 2f);
        Vector3 chunkWorldCenter = localChunkCenter;
        float distanceToChunk = (chunkWorldCenter - center).Length();
        float chunkRadius = (new Vector3(PaddedSize, PaddedSize, PaddedSize) * 0.5f * scale).Length();

        bool isoMaxxed = false;
        if (distanceToChunk - chunkRadius > radius + maxMountainHeight)
        {
            System.Array.Fill(voxelIso, (byte)0);
            isoMaxxed = true;
        }
        else if (distanceToChunk + chunkRadius < radius - maxMountainHeight)
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

            for (int x = 0; x < PaddedSize; x++)
            {
                int worldX = chunkXOffset + x;
                int xStride = x * PaddedSize * PaddedSize;
                for (int y = 0; y < PaddedSize; y++)
                {
                    int worldY = chunkYOffset + y;
                    int yStride = y * PaddedSize;

                    for (int z = 0; z < PaddedSize; z++)
                    {
                        int worldZ = chunkZOffset + z;

                        Vector3 voxelWorldPos = new Vector3(worldX, worldY, worldZ) * scale;
                        Vector3 toCenter = voxelWorldPos - center;
                        float distance = toCenter.Length();

                        Vector3 direction = distance > 0.0001f ? toCenter / distance : Vector3.Up;

                        float noiseValue = noise.GetNoise3Dv(direction * radius);
                        float terrainOffset = noiseValue * maxMountainHeight;

                        float density = distance - (radius + terrainOffset) + maxMountainHeight + crustThickness;
                        float normalizedDensity = Mathf.Clamp(density / crustThickness, -1.0f, 1.0f);
                        
                        int index = xStride + yStride + z;
                        voxelIso[index] = (byte)Mathf.RoundToInt((1.0f - normalizedDensity) * 127.5f);
                    }
                }
            }
        }

        for (int i = 0; i < totalSize; i++)
        {
            voxels[i] = new VoxelData
            {
                Iso = voxelIso[i],
                Type = voxelIso[i] == 0 ? (ushort)0 : (ushort)1,
                Health = 100
            };
        }

        return voxels;
    }
}