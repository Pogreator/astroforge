using System.Collections.Concurrent;
using Godot;

public static class VoxelBufferUtility
{
    private const int CoreSize = 16;
    private const int PaddedSize = 18;

    private static int GetPaddedIndex(int x, int y, int z) => (x * 324) + (y * 18) + z;
    private static int GetCoreIndex(int x, int y, int z) => (x * 256) + (y * 16) + z;

    public static byte[] BuildPaddedBuffer(Vector3I chunkPos, ConcurrentDictionary<Vector3I, ChunkData> chunkMap)
    {
        byte[] paddedBuffer = new byte[PaddedSize * PaddedSize * PaddedSize];

        for (int x = 0; x < PaddedSize; x++)
        {
            for (int y = 0; y < PaddedSize; y++)
            {
                for (int z = 0; z < PaddedSize; z++)
                {
                    // Convert padded layout array space (0 to 17) to planet coordinates (-1 to 16)
                    int localX = x - 1;
                    int localY = y - 1;
                    int localZ = z - 1;

                    // Determine which chunk actually owns this specific coordinate step
                    int chunkOffsetX = Mathf.FloorToInt((float)localX / CoreSize);
                    int chunkOffsetY = Mathf.FloorToInt((float)localY / CoreSize);
                    int chunkOffsetZ = Mathf.FloorToInt((float)localZ / CoreSize);

                    Vector3I targetChunkCoords = chunkPos + new Vector3I(chunkOffsetX, chunkOffsetY, chunkOffsetZ);

                    // Find the exact block array index within that target chunk (0 to 15)
                    int blockX = Mathf.PosMod(localX, CoreSize);
                    int blockY = Mathf.PosMod(localY, CoreSize);
                    int blockZ = Mathf.PosMod(localZ, CoreSize);

                    int paddedIdx = (x * 324) + (y * 18) + z;

                    if (chunkMap.TryGetValue(targetChunkCoords, out var sourceChunk) && sourceChunk != null)
                    {
                        int coreIdx = (blockX * 256) + (blockY * 16) + blockZ;
                        paddedBuffer[paddedIdx] = sourceChunk.VoxelData[coreIdx];
                    }
                    else
                    {
                        paddedBuffer[paddedIdx] = 0; // Default to air if neighbor isn't ready
                    }
                }
            }
        }
        return paddedBuffer;
    }
}   