using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class VoxelGeneratorTestToolThreaded : MeshInstance3D
{
	[Export] public Vector3I TargetChunkPosition { get; set; } = Vector3I.Zero;
	[Export] public int WorldSeed { get; set; } = 0;
	[Export] public byte IsoLevel { get; set; } = 128;
	[Export(PropertyHint.Range, "0,4,1")] public int LOD { get; set; } = 0;
	[Export(PropertyHint.Range, "1,20,1")] public int GenerationRadius { get; set; } = 1;
	
	private bool _triggerGeneration = false;
	[Export]
	public bool TriggerGeneration
	{
		get => _triggerGeneration;
		set
		{
			if (value)
			{
				RunVoxelGenerationTest();
			}
		}
	}

	private async void RunVoxelGenerationTest()
	{
		GD.Print($"[Tool] [WorkerPool] Initialising Batch generation at {GenerationRadius} around {TargetChunkPosition}");

		Stopwatch globalTimer = Stopwatch.StartNew();
		Stopwatch phaseTimer = new Stopwatch();

		var corePositions = new List<Vector3I>();
		var allPositionsRequired = new HashSet<Vector3I>();

		await Task.Run(() =>
		{
		   for (int x = -GenerationRadius - 1; x <= GenerationRadius + 1; x++)
			{
				for (int y = -GenerationRadius - 1; y <= GenerationRadius + 1; y++)
				{
					for (int z = -GenerationRadius - 1; z <= GenerationRadius + 1; z++)
					{
						Vector3I offset = new Vector3I(x,y,z);
						Vector3I absolutePos = TargetChunkPosition + offset;

						allPositionsRequired.Add(absolutePos);

						if (Mathf.Abs(x) <= GenerationRadius && Mathf.Abs(y) <= GenerationRadius && Mathf.Abs(z) <= GenerationRadius)
						{
							corePositions.Add(absolutePos);
						}
					}
				}
			}
		});

		Vector3I[] dataPositionArray = allPositionsRequired.ToArray();
		var mockWorldData = new ConcurrentDictionary<Vector3I, ChunkData>();

		phaseTimer.Start();

		long noiseGroupId = WorkerThreadPool.AddGroupTask(Callable.From<int>((index) => 
		{
			Vector3I relativePos = dataPositionArray[index];
			byte[] rawBytes = PlanetGenerator.GenerateChunkDataTest(relativePos, WorldSeed);

			var mockChunk = new ChunkData(relativePos, 16) { VoxelData = rawBytes };
			mockWorldData[relativePos] = mockChunk;
		}), dataPositionArray.Length);

		while (!WorkerThreadPool.IsGroupTaskCompleted(noiseGroupId))
		{
			await Task.Delay(1);
		}
		phaseTimer.Stop();
		long noiseTimeMs = phaseTimer.ElapsedMilliseconds;

		phaseTimer.Restart();

		var compiledMeshes = new ConcurrentBag<MeshData>();
		Vector3I[] corePositionsArray = corePositions.ToArray();
		
		long meshGroupId = WorkerThreadPool.AddGroupTask(Callable.From<int>((index) =>
		{
			Vector3I currentCorePos = corePositionsArray[index];
			byte[] paddedBuffer = VoxelBufferUtility.BuildPaddedBuffer(currentCorePos, mockWorldData);

			MeshData singleChunkMesh = VoxelMesher.GenerateMarchingCubes(paddedBuffer, IsoLevel, LOD);

			if (singleChunkMesh.Verticies.Count > 0)
			{
				Vector3 localOffset = new Vector3(
					(currentCorePos.X - TargetChunkPosition.X) * 16,
					(currentCorePos.Y - TargetChunkPosition.Y) * 16,
					(currentCorePos.Z - TargetChunkPosition.Z) * 16
				);

				for (int i = 0; i < singleChunkMesh.Verticies.Count; i++)
				{
					singleChunkMesh.Verticies[i] += localOffset;
				}

				compiledMeshes.Add(singleChunkMesh);
			}

		}), corePositionsArray.Length);

		while (!WorkerThreadPool.IsGroupTaskCompleted(meshGroupId))
		{
			await Task.Delay(1);
		}

		phaseTimer.Stop();
		long meshingTimeMs = phaseTimer.ElapsedMilliseconds;

		phaseTimer.Restart();

		var finalVertices = new List<Vector3>();
		var finalIndices = new List<int>();
		var finalNormals = new List<Vector3>();
		int indexOffset = 0;

		foreach (MeshData subMesh in compiledMeshes)
		{
			finalVertices.AddRange(subMesh.Verticies);
			finalNormals.AddRange(subMesh.Normals);

			foreach (int localIndex in subMesh.Triangles)
			{
				finalIndices.Add(localIndex + indexOffset);
			}
			indexOffset += subMesh.Verticies.Count;
		}

		if (finalVertices.Count == 0)
		{
			GD.PrintErr("[Tool] [WorkerPool] Generation completed, but all sampled regions resulted in 0 vertices.");
			TriggerGeneration = false;
			return;
		}

		var arrayMesh = new ArrayMesh();
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		arrays[(int)Mesh.ArrayType.Vertex] = finalVertices.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = finalIndices.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = finalNormals.ToArray();

		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		this.Mesh = arrayMesh;
		phaseTimer.Stop();
		long engineTimeMs = phaseTimer.ElapsedMilliseconds;

		globalTimer.Stop();

		GD.Print($"[Tool] Success! Generated Voxel Mesh with {finalVertices.Count} vertices.");

		GD.Print($"[Tool] Noise time: {noiseTimeMs}");
		GD.Print($"[Tool] Meshing time: {meshingTimeMs}");
		GD.Print($"[Tool] Engine time: {engineTimeMs}");
		GD.Print($"[Tool] Total time: {noiseTimeMs + meshingTimeMs + engineTimeMs}");
		TriggerGeneration = false;
	}
}
