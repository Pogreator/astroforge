// Quick tool script generated with Gemini to make sure im not crazy
// The mesh generation DOES infact work (marching cubes and planet noise generation)
// So I have NO clue why it refuses to work with my octrees

using System;
using System.Diagnostics;
using System.Collections.Generic;
using Godot;

[Tool]
public partial class VoxelPaddedTestTool : MeshInstance3D
{
    [Export] public Vector3I TargetChunkPosition { get; set; } = Vector3I.Zero;
    [Export] public int WorldSeed { get; set; } = 1337;
    [Export] public byte IsoLevel { get; set; } = 128;
    [Export(PropertyHint.Range, "1,5,1")] public int scaleMultiplier { get; set; } = 1;
    [Export(PropertyHint.Range, "1,10,1")] public int TestRadius { get; set; } = 2;

    private bool _triggerTest = false;
    [Export]
    public bool TriggerTest
    {
        get => _triggerTest;
        set
        {
            _triggerTest = value;
            if (_triggerTest)
            {
                RunIsolatedPaddedTest();
            }
        }
    }

    private async void RunIsolatedPaddedTest()
    {
        GD.Print($"[Test Tool] Starting isolated Pre-Padded test sequence inside Radius {TestRadius} around grid position {TargetChunkPosition}...");
        
        Stopwatch timer = Stopwatch.StartNew();

        // 1. Gather all core target positions safely
        var targets = new List<Vector3I>();
        for (int x = -TestRadius; x <= TestRadius; x++)
        {
            for (int y = -TestRadius; y <= TestRadius; y++)
            {
                for (int z = -TestRadius; z <= TestRadius; z++)
                {
                    targets.Add(TargetChunkPosition + new Vector3I(x, y, z));
                }
            }
        }

        var collectedSubMeshes = new System.Collections.Concurrent.ConcurrentBag<(Vector3I pos, MeshData mesh)>();
        var activeTaskIds = new List<long>();

        // Calculate physical radius footprint matching your engine's baseline math settings
        float effectiveRadius = TestRadius * 16.0f;

        // 2. Spawn a single independent thread task for each individual coordinate target
        foreach (Vector3I chunkPos in targets)
        {
            // Lock the thread footprint context variable safely inside local closure boundaries
            Vector3I localChunkPos = chunkPos; 

            long taskId = WorkerThreadPool.AddTask(Callable.From(() =>
            {
                // Generate the 18x18x18 padded array cleanly in place using mathematical procedural rules
                VoxelData[] paddedVoxels = PlanetGenerator.GeneratePaddedChunkDataPlanet(
                    localChunkPos,
                    Vector3.Zero, // Planet Center
                    WorldSeed,
                    effectiveRadius,
                    scaleMultiplier
                );

                // Pass the fresh buffer directly down into your 18x18x18 VoxelMesher script layout
                MeshData singleMesh = VoxelMesher.GenerateMarchingCubes(paddedVoxels, IsoLevel, 0, scaleMultiplier);

                if (singleMesh != null && singleMesh.Verticies != null && singleMesh.Verticies.Count > 0)
                {
                    // Shift vertex boundaries into their absolute chunk positions offset layout
                    Vector3 worldChunkOffset = new Vector3(
                        (localChunkPos.X - TargetChunkPosition.X) * 16 * scaleMultiplier,
                        (localChunkPos.Y - TargetChunkPosition.Y) * 16 * scaleMultiplier,
                        (localChunkPos.Z - TargetChunkPosition.Z) * 16 * scaleMultiplier
                    );

                    for (int i = 0; i < singleMesh.Verticies.Count; i++)
                    {
                        singleMesh.Verticies[i] += worldChunkOffset;
                    }

                    collectedSubMeshes.Add((localChunkPos, singleMesh));
                }
            }));

            activeTaskIds.Add(taskId);
        }

        // 3. Keep the asynchronous tool active until every worker thread hits 100% completion
        while (activeTaskIds.Count > 0)
        {
            for (int i = activeTaskIds.Count - 1; i >= 0; i--)
            {
                if (WorkerThreadPool.IsTaskCompleted(activeTaskIds[i]))
                {
                    activeTaskIds.RemoveAt(i);
                }
            }
            await System.Threading.Tasks.Task.Delay(1);
        }

        // 4. Merge all generated submeshes into unified arrays
        var finalVertices = new List<Vector3>();
        var finalIndices = new List<int>();
        var finalNormals = new List<Vector3>();
        int indexOffset = 0;

        foreach (var item in collectedSubMeshes)
        {
            finalVertices.AddRange(item.mesh.Verticies);
            finalNormals.AddRange(item.mesh.Normals);

			foreach (int localIdx in item.mesh.Triangles)
			{
				finalIndices.Add(localIdx + indexOffset);
			}
			indexOffset += item.mesh.Verticies.Count;
        }

        // 5. Apply the arrays directly to this MeshInstance3D container
        if (finalVertices.Count == 0)
        {
            GD.PrintErr("[Test Tool] Failed! All chunks evaluated to 0 vertices. Double-check your planet noise parameters.");
            this.Mesh = null;
            _triggerTest = false;
            return;
        }

        ArrayMesh finalArrayMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        arrays[(int)Mesh.ArrayType.Vertex] = finalVertices.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = finalIndices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = finalNormals.ToArray();

        finalArrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        this.Mesh = finalArrayMesh;

        timer.Stop();
        GD.Print($"[Test Tool] Success! Rendered terrain surface with {finalVertices.Count} vertices in {timer.ElapsedMilliseconds}ms.");
        _triggerTest = false;
    }
}
