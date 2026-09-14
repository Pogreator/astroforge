using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Godot;

public class PlanetOctreeNode
{
    public Vector3 Center { get; private set; }
    public float Size { get; private set; }
    public int Depth { get; private set; }

    public Vector3I ChunkPos { get; private set; }
    public PlanetOctreeNode[] Children { get; private set; }
    public bool IsLeaf => Children == null;

    public MeshInstance3D RenderMesh { get; set; }
    public bool MeshesPending { get; set; } = false;

    public int LOD { get; private set; } = 4;

    // Added a calculated lower-left bounding box property variable to place meshes flawlessly
    public Vector3 Position => Center - new Vector3(Size * 0.5f, Size * 0.5f, Size * 0.5f);

    public PlanetOctreeNode(Vector3 center, float size, int depth, Vector3I chunkPos = default)
    {
        Center = center;
        Size = size;
        Depth = depth;
        ChunkPos = chunkPos;
    }

    public void Subdivide()
    {
        if (!IsLeaf) return;
        Children = new PlanetOctreeNode[8];
        float childSize = Size * 0.5f;
        int childDepth = Depth + 1;

        Vector3I baseChunkPos = ChunkPos * 2;

        int index = 0;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 childCenter = Center + new Vector3(x, y, z) * (childSize * 0.5f);

                    int chunkOffsetX = x > 0 ? 1 : -1;
                    int chunkOffsetY = y > 0 ? 1 : -1;
                    int chunkOffsetZ = z > 0 ? 1 : -1;
                    Vector3I childChunkPos = baseChunkPos + new Vector3I(chunkOffsetX, chunkOffsetY, chunkOffsetZ);
                    
                    Children[index] = new PlanetOctreeNode(childCenter, childSize, childDepth, childChunkPos);
                    index++;
                }
            }
        }      
    }

    public void Collapse()
    {
        if (IsLeaf) return;
        foreach (var child in Children)
        {
            child.Collapse();
            if (child.RenderMesh != null)
            {
                // if (GodotObject.IsInstanceValid(child.RenderMesh))
                // {
                //     if (child.RenderMesh.GetParent() is Node parent)
                //     {
                //         parent.RemoveChild(child.RenderMesh);
                //     }
                //     child.RenderMesh.QueueFree();
                // }
                child.RenderMesh.QueueFree();
                child.RenderMesh = null;
			}
        }
        Children = null;
        MeshesPending = false;
    }

    public void SubdivideFully(int maxDepth)
    {
        if (Depth >= maxDepth) return;

        Subdivide();

        if (Children == null) return;

        foreach (var child in Children)
        {
            child.SubdivideFully(maxDepth);
        }
    }

    public void UpdateLOD(Vector3 cameraPosition, int maxDepth, float[] lodDistances)
    {
        float distance = Center.DistanceTo(cameraPosition);
        if (distance <= lodDistances[0])      LOD = 0; 
        else if (distance <= lodDistances[1]) LOD = 1; 
        else if (distance <= lodDistances[2]) LOD = 2; 
        else if (distance <= lodDistances[3]) LOD = 3; 
        else                                  LOD = 4; 

        int requiredDepth = Mathf.Clamp(maxDepth - LOD, 1, maxDepth);

        if (Depth < requiredDepth)
        {
            if (IsLeaf)
            {
                Subdivide();
            }
            foreach (var child in Children)
            {
                child.UpdateLOD(cameraPosition, maxDepth, lodDistances);
            }
        }
        else
        {
            if (!IsLeaf)
            {
                Collapse();
            }
        }
    }

    public void GetActiveLeafNodes(List<PlanetOctreeNode> activeVisibleNodes)
    {
        if (IsLeaf)
        {
            activeVisibleNodes.Add(this);
        }
        else
        {
            foreach (var child in Children)
            {
                child.GetActiveLeafNodes(activeVisibleNodes);
            }
        }
    }
}
