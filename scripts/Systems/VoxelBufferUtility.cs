using System.Collections.Concurrent;
using Godot;

public static class VoxelBufferUtility
{
    private static int GetPaddedIndex(int x, int y, int z, int paddedSize)
    {
        return (x * paddedSize * paddedSize) + (y * paddedSize) + z;
    }

    public static VoxelData[] BuildPaddedBuffer(Vector3I chunkPos, ConcurrentDictionary<Vector3I, ChunkData> chunkMap, int size)
    {
        int paddedSize = size + 2;
        VoxelData[] paddedBuffer = new VoxelData[paddedSize * paddedSize * paddedSize];

        for (int x = 0; x < paddedSize; x++)
        {
            for (int y = 0; y < paddedSize; y++)
            {
                for (int z = 0; z < paddedSize; z++)
                {
                    int localX = x - 1;
                    int localY = y - 1;
                    int localZ = z - 1;

                    int chunkOffsetX = Mathf.FloorToInt((float)localX / size);
                    int chunkOffsetY = Mathf.FloorToInt((float)localY / size);
                    int chunkOffsetZ = Mathf.FloorToInt((float)localZ / size);

                    Vector3I targetChunkCoords = chunkPos + new Vector3I(chunkOffsetX, chunkOffsetY, chunkOffsetZ);

                    int blockX = Mathf.PosMod(localX, size);
                    int blockY = Mathf.PosMod(localY, size);
                    int blockZ = Mathf.PosMod(localZ, size);

                    int paddedIdx = GetPaddedIndex(x, y, z, paddedSize);

                    if (chunkMap.TryGetValue(targetChunkCoords, out var sourceChunk) && sourceChunk != null)
                    {
                        int coreIdx = sourceChunk.GetIndex(blockX, blockY, blockZ);
                        paddedBuffer[paddedIdx] = sourceChunk.Voxels[coreIdx];
                    }
                    else
                    {
                        // Default fallback if neighbor chunk data isn't ready/loaded yet
                        paddedBuffer[paddedIdx] = new VoxelData(0, 0, 0); 
                    }
                }
            }
        }
        return paddedBuffer;
    }
}
