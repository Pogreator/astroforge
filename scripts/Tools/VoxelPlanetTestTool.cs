using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class VoxelPlanetTestTool : Node3D
{
	[Export] public Vector3I TargetChunkPosition { get; set; } = Vector3I.Zero;
	[Export] public int WorldSeed { get; set; } = 0;
	[Export] public byte IsoLevel { get; set; } = 128;
	[Export] public int ChunksPerBatch { get; set; } = 32; 
	[Export] public float LOD0Distance { get; set; } = 50;
	[Export] public float LOD1Distance { get; set; } = 100;
	[Export] public float LOD2Distance { get; set; } = 150;
	[Export] public float LOD3Distance { get; set; } = 200;
	[Export] public float LOD4Distance { get; set; } = 250;
	[Export(PropertyHint.Range, "1,40,1")] public int GenerationRadius { get; set; } = 1;
	
	private bool _runRealTimeGeneration = false;
	[Export]
	public bool RunRealTImeGeneration
	{
		get => _runRealTimeGeneration;
		set
		{
			_runRealTimeGeneration = value;
			SetProcess(_runRealTimeGeneration);
		}
	}

	private List<Vector3I> ChunkPositions { get; set; }
	private ConcurrentDictionary<Vector3I, int> ChunkQueue { get; set; }
	private ConcurrentDictionary<Vector3I, ChunkData> LoadedChunks { get; set; }
	private ConcurrentDictionary<Vector3I, int> LoadedChunkLOD { get; set; }
	private ConcurrentDictionary<Vector3I, MeshInstance3D> ActiveChunkNodes { get; set; }
	private ConcurrentQueue<(Vector3I pos, int lod, MeshData mesh)> MeshRenderQueue { get; set; } = new();
	private bool _isGeneratingChunks = false;
	private bool _isSortingAndFiltering = false;
	private long _activeChunkGroupId = -1;
	

	private void _InitChunks()
	{
		ChunkPositions ??= new List<Vector3I>();
		ChunkPositions.Clear();

		for (int x = -GenerationRadius - 1; x <= GenerationRadius + 1; x++)
			{
				for (int y = -GenerationRadius - 1; y <= GenerationRadius + 1; y++)
				{
					for (int z = -GenerationRadius - 1; z <= GenerationRadius + 1; z++)
				{
					Vector3I offset = new Vector3I(x,y,z);
					Vector3I absolutePos = TargetChunkPosition + offset;

					if (Mathf.Abs(x) <= GenerationRadius && Mathf.Abs(y) <= GenerationRadius && Mathf.Abs(z) <= GenerationRadius)
					{
						ChunkPositions.Add(absolutePos);
					}
				}
			}
		}
	}

	public override void _EnterTree()
	{
		ChunkQueue ??= new ConcurrentDictionary<Vector3I, int>();
		LoadedChunks ??= new ConcurrentDictionary<Vector3I, ChunkData>();
		LoadedChunkLOD ??= new ConcurrentDictionary<Vector3I, int>();
		ActiveChunkNodes ??= new ConcurrentDictionary<Vector3I, MeshInstance3D>();
	}

	public override void _Ready()
	{
		_InitChunks();
		SetProcess(_runRealTimeGeneration);
	}

	private void ChunkQueueUpdate(Vector3 cameraPos)
	{
		if (ChunkQueue == null || LoadedChunks == null || !IsInsideTree()) return;
		float radiusOffset = (float)GenerationRadius;
		Vector3 newCameraPos = cameraPos / 16.0f;
		Vector3 planetPosGlobal = this.GlobalPosition / 16.0f;

		foreach (Vector3I chunk in ChunkPositions)
		{
			float distance = (((Vector3)chunk + planetPosGlobal) - newCameraPos).Length() - radiusOffset; 
			// Different LODs that have to be added to queue
			if (distance <= LOD0Distance)
			{
				ChunkQueue[chunk] = 0;
			} else if (distance <= LOD1Distance)
			{
				ChunkQueue[chunk] = 1;
			} else if (distance <= LOD2Distance)
			{
				ChunkQueue[chunk] = 2;
			} else if (distance <= LOD3Distance)
			{
				ChunkQueue[chunk] = 3;
			} else if (distance <= LOD4Distance)
			{
				ChunkQueue[chunk] = 4;
			} else if (LoadedChunks.ContainsKey(chunk))
			{
				ChunkQueue[chunk] = -1;
			}
		}
	}

	private void RemoveAndCleanupChunkNode(Vector3I chunkPos)
	{
		if (ActiveChunkNodes.TryRemove(chunkPos, out MeshInstance3D chunkNode))
		{
			if (GodotObject.IsInstanceValid(chunkNode))
			{
				chunkNode.QueueFree();
			}
		}   

		ChunkQueue.TryRemove(chunkPos, out _);
		LoadedChunks.TryRemove(chunkPos, out _);
		LoadedChunkLOD.TryRemove(chunkPos, out _);
		ActiveChunkNodes.TryRemove(chunkPos, out _);

		if (Engine.IsEditorHint())
		{
			NotifyPropertyListChanged();
		}
	}

	private void AddChunkNode(Vector3I chunkPos, MeshInstance3D newChunkNode)
	{
		if (ActiveChunkNodes.TryRemove(chunkPos, out MeshInstance3D oldNode))
		{
			if (GodotObject.IsInstanceValid(oldNode))
			{
				oldNode.QueueFree();
			}
		}

		this.AddChild(newChunkNode);
		newChunkNode.Position += (Vector3)chunkPos * 16.0f;
		ActiveChunkNodes.TryAdd(chunkPos, newChunkNode);
		
		ChunkQueue.TryRemove(chunkPos, out _);

		if (Engine.IsEditorHint())
		{
			NotifyPropertyListChanged();
		}
	}

	private void GenerateChunks(Vector3 cameraPos)
	{
		if (ChunkQueue == null || ChunkQueue.IsEmpty) return;

		if (_activeChunkGroupId != -1)
		{
			if (!WorkerThreadPool.IsGroupTaskCompleted(_activeChunkGroupId))
			{
				return;
			}
			_activeChunkGroupId = -1;
		}

		if (_isGeneratingChunks || _isSortingAndFiltering) return;
		_isSortingAndFiltering = true;

		Vector3 planetGlobalPos = this.GlobalPosition;

		WorkerThreadPool.AddTask(Callable.From(() =>
		{
			
			try
			{
				var allKeys = ChunkQueue.Keys.ToList();
				Vector3 cameraIndexSpace = (cameraPos - planetGlobalPos) / 16.0f;

				var prioritizedChunks = allKeys
					.Where(pos => {
						ChunkQueue.TryGetValue(pos, out int targetLod);
						LoadedChunkLOD.TryGetValue(pos, out int currentLod);
						return targetLod == -1 || targetLod != currentLod;
					})
					.OrderBy(pos => ((Vector3)pos - cameraIndexSpace).LengthSquared())
					.ToList();

				_isSortingAndFiltering = false;

				if (prioritizedChunks.Count == 0) return;

				_isGeneratingChunks = true;
				_activeChunkGroupId = WorkerThreadPool.AddGroupTask(Callable.From<int>((index) =>
				{
					if (index >= prioritizedChunks.Count) return;
					Vector3I chunkPos = prioritizedChunks[index];
					ChunkQueue.TryGetValue(chunkPos, out int lodLevel);
					if (lodLevel == -1)
					{
						Callable.From(() => RemoveAndCleanupChunkNode(chunkPos)).CallDeferred();
						return;
					}

					LoadedChunkLOD.TryGetValue(chunkPos, out int pastLodLevel);
					if (lodLevel == pastLodLevel)
					{
						return;
					}

					byte[] rawBytes = PlanetGenerator.GenerateChunkDataPlanet(chunkPos, planetGlobalPos, WorldSeed, (float)(GenerationRadius - 0.5) * 16.0f);
					var chunk = new ChunkData(chunkPos, 16) { VoxelIso = rawBytes };

					LoadedChunks[chunkPos] = chunk;

					byte[] paddedBuffer = VoxelBufferUtility.BuildPaddedBuffer(chunkPos, LoadedChunks);
					MeshData computedMesh = VoxelMesher.GenerateMarchingCubes(paddedBuffer, IsoLevel, lodLevel);

					if (computedMesh == null || computedMesh.Verticies == null || computedMesh.Verticies.Count == 0)
					{
						ChunkQueue.TryRemove(chunkPos, out _);
						return;
					}

					MeshRenderQueue.Enqueue((chunkPos, lodLevel, computedMesh));

				}), prioritizedChunks.Count);
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"Exception in GenerateChunks layout: {ex.Message}");
			}
			finally
			{
				_isGeneratingChunks = false;
			}
		}));
	}

	public override void _Process(double delta)
	{   
		if (Engine.IsEditorHint())
		{

			int meshesBuiltThisFrame = 0;
			while (meshesBuiltThisFrame < ChunksPerBatch && MeshRenderQueue.TryDequeue(out var renderJob))
			{
				BuildAndAddMeshToScene(renderJob.pos, renderJob.lod, renderJob.mesh);
				meshesBuiltThisFrame++;
			}

			var editorInterface = EditorInterface.Singleton;
			SubViewport editorViewport = editorInterface.GetEditorViewport3D(0);

			if (editorViewport != null)
			{
				Camera3D editorCam = editorViewport.GetCamera3D();
				Vector3 cameraGlobalPos = editorCam.GlobalPosition;
				ChunkQueueUpdate(cameraGlobalPos);
				GenerateChunks(cameraGlobalPos);
			}
		}
	}
	private void BuildAndAddMeshToScene(Vector3I chunkPos, int lodLevel, MeshData computedMesh)
	{
		MeshInstance3D newChunkNode = new MeshInstance3D();
		var arrayMesh = new ArrayMesh();
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		arrays[(int)Mesh.ArrayType.Vertex] = computedMesh.Verticies.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = computedMesh.Triangles.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = computedMesh.Normals.ToArray();

		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		newChunkNode.Mesh = arrayMesh;

		LoadedChunkLOD[chunkPos] = lodLevel;

		AddChunkNode(chunkPos, newChunkNode);

		if (Engine.IsEditorHint())
		{
			NotifyPropertyListChanged();
		}
	}
}
