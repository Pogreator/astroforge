using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

public class PlanetOctreeNode
{
    public Vector3 Center { get; private set; }
    public float Size { get; private set; }
    public int Depth { get; private set; }

    public Vector3I ChunkPos { get; private set; }
    public bool IsLeaf => Children == null;

    public PlanetOctreeNode[] Children { get; private set; }

    public PlanetOctreeNode(Vector3 center, float size, int depth, Vector3I chunkPos = default)
    {
        Center = center;
        Size = size;
        Depth = depth;
        ChunkPos = chunkPos;
    }

    public void Subdivide(HashSet<Vector3I> validPositions)
    {
        if (Size <= 16.0f) return;

        float childSize = Size / 2.0f;
        float offset = childSize / 2.0f;
        bool hasValidChildren = false;

        PlanetOctreeNode[] tempChildren = new PlanetOctreeNode[8];
        int childIndex = 0;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 childCenter = Center + new Vector3(x * offset, y * offset, z * offset);

                    Vector3I estimatedChunkPos = new Vector3I(
                        Mathf.FloorToInt(childCenter.X / 16.0f),
                        Mathf.FloorToInt(childCenter.Y / 16.0f),
                        Mathf.FloorToInt(childCenter.Z / 16.0f)
                    );

                    if (IsRegionActive(childCenter, childSize, validPositions))
                    {
                        tempChildren[childIndex] = new PlanetOctreeNode(childCenter, childSize, Depth + 1, estimatedChunkPos);
                        tempChildren[childIndex].Subdivide(validPositions);
                        hasValidChildren = true;
                    }
                    childIndex++;
                }
            }
        }
        if (hasValidChildren)
        {
            Children = tempChildren;
        }
    }

    private bool IsRegionActive(Vector3 center, float size, HashSet<Vector3I> validPositions)
    {
        float halfSize = size / 2.0f;

        int minChunkX = Mathf.FloorToInt((center.X - halfSize) / 16.0f);
        int maxChunkX = Mathf.FloorToInt((center.X + halfSize) / 16.0f);
        
        int minChunkY = Mathf.FloorToInt((center.Y - halfSize) / 16.0f);
        int maxChunkY = Mathf.FloorToInt((center.Y + halfSize) / 16.0f);
        
        int minChunkZ = Mathf.FloorToInt((center.Z - halfSize) / 16.0f);
        int maxChunkZ = Mathf.FloorToInt((center.Z + halfSize) / 16.0f);

        for (int cx = minChunkX; cx <= maxChunkX; cx++)
        {
            for (int cy = minChunkY; cy <= maxChunkY; cy++)
            {
                for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
                {
                    if (validPositions.Contains(new Vector3I(cx, cy, cz)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}