using System.Collections.Concurrent;
using Godot;

public static class VoxelBufferUtility
{
    private const int CoreSize = 16;
    private const int PaddedSize = 18;

    private static int GetPaddedIndex(int x, int y, int z)
    {
        return (x * PaddedSize * PaddedSize) + (y * PaddedSize) + z;
    }

    public static VoxelData[] BuildPaddedBuffer(Vector3I chunkPos, ConcurrentDictionary<Vector3I, ChunkData> chunkMap)
    {
        VoxelData[] paddedBuffer = new VoxelData[PaddedSize * PaddedSize * PaddedSize];

        for (int x = 0; x < PaddedSize; x++)
        {
            for (int y = 0; y < PaddedSize; y++)
            {
                for (int z = 0; z < PaddedSize; z++)
                {
                    int localX = x - 1;
                    int localY = y - 1;
                    int localZ = z - 1;

                    int chunkOffsetX = Mathf.FloorToInt((float)localX / CoreSize);
                    int chunkOffsetY = Mathf.FloorToInt((float)localY / CoreSize);
                    int chunkOffsetZ = Mathf.FloorToInt((float)localZ / CoreSize);

                    Vector3I targetChunkCoords = chunkPos + new Vector3I(chunkOffsetX, chunkOffsetY, chunkOffsetZ);

                    int blockX = Mathf.PosMod(localX, CoreSize);
                    int blockY = Mathf.PosMod(localY, CoreSize);
                    int blockZ = Mathf.PosMod(localZ, CoreSize);

                    int paddedIdx = GetPaddedIndex(x, y, z);

                    if (chunkMap.TryGetValue(targetChunkCoords, out var sourceChunk) && sourceChunk != null && sourceChunk.Voxels != null)
                    {
                        int coreIdx = (blockX * CoreSize * CoreSize) + (blockY * CoreSize) + blockZ;

                        if (coreIdx >= 0 && coreIdx < sourceChunk.Voxels.Length)
                        {
                            paddedBuffer[paddedIdx] = sourceChunk.Voxels[coreIdx];
                        }
                        else
                        {
                            paddedBuffer[paddedIdx] = new VoxelData { Iso = 0, Type = 0, Health = 100 };
                        }
                    }
                    else
                    {
                        paddedBuffer[paddedIdx] = new VoxelData { Iso = 0, Type = 0, Health = 100 }; 
                    }
                }
            }
        }
        return paddedBuffer;
    }
}