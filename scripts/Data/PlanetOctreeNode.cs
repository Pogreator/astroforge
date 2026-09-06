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

    
}