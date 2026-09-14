using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class VoxelPlanetTestToolCube : Node3D
{
	[Export] public Vector3I TargetChunkPosition { get; set; } = Vector3I.Zero;
	[Export] public int WorldSeed { get; set; } = 1337;
	[Export] public byte IsoLevel { get; set; } = 128;
	[Export] public float CameraMovementThreshold { get; set; } = 8.0f; 
	[Export] public float LOD0Distance { get; set; } = 50;
	[Export] public float LOD1Distance { get; set; } = 100;
	[Export] public float LOD2Distance { get; set; } = 150;
	[Export] public float LOD3Distance { get; set; } = 200;
	[Export] public float LOD4Distance { get; set; } = 250;
	[Export(PropertyHint.Range, "1,40,1")] public int PlanetRadius { get; set; } = 1;
	
	[ExportToolButton("Initialise")]
	public Callable InitButton => Callable.From(Init);

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

	private const int CoreSize = 16;
	private const int PaddedSize = 18;
	private PlanetOctreeNode _rootNode;
	private Vector3 _lastCameraPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
	private List<PlanetOctreeNode> _visibleLeaves = new List<PlanetOctreeNode>();
	private PlanetOctreeNode[] _generationSnapshot = Array.Empty<PlanetOctreeNode>();

	public void Init()
	{
		if (_rootNode != null)
		{
			_rootNode.Collapse();
		}

		float chunksAcrossDiameter = PlanetRadius;

		int powerOfTwoChunks = 1;
		while (powerOfTwoChunks < chunksAcrossDiameter)
		{
			powerOfTwoChunks *= 2;
		}

		float calculatedRootSize = powerOfTwoChunks * (float)CoreSize;

		_rootNode = new PlanetOctreeNode(Vector3.Zero, calculatedRootSize, 0, Vector3I.Zero); 
	}

	public override void _Ready()
	{
		SetProcess(_runRealTimeGeneration);
		Init();
	}

	public void UpdateLOD(Vector3 CameraPosition)
	{
		if (_rootNode == null) return;

		float[] lodDistances = new float[] { LOD0Distance, LOD1Distance, LOD2Distance, LOD3Distance, LOD4Distance };
		for (int i = 0; i < lodDistances.Length; i++) lodDistances[i] += PlanetRadius*(float)CoreSize;

		float sizeRatio = (PlanetRadius * CoreSize) / CoreSize;
		int maxDepth = Mathf.RoundToInt(Math.Log2(sizeRatio)) + 1;
		maxDepth = (maxDepth > 5) ? 5 : maxDepth;

		_rootNode.UpdateLOD(CameraPosition, maxDepth, lodDistances);
		_visibleLeaves.Clear();
		_rootNode.GetActiveLeafNodes(_visibleLeaves);

		// Temp
		foreach (var child in GetChildren()) child.QueueFree();

		foreach (var node in _visibleLeaves)
		{
			if (node.RenderMesh == null && !node.MeshesPending)
			{
				node.MeshesPending = true;
			}
		}
		
		_generationSnapshot = _visibleLeaves.ToArray();

		// long taskId = WorkerThreadPool.AddGroupTask(Callable.From<int>(_generateNode),_visibleLeaves.Count());
		// WorkerThreadPool.WaitForGroupTaskCompletion(taskId);
		long newTaskID = WorkerThreadPool.AddGroupTask(Callable.From<int>(_testGenerate),_visibleLeaves.Count());
	}

	private void _testGenerate(int nodeIndex)
	{
		if (_generationSnapshot[nodeIndex].RenderMesh != null) return;
		CallDeferred(MethodName._testMesh, nodeIndex);
	}

	private void _testMesh(int nodeIndex)
	{
		PlanetOctreeNode node = _visibleLeaves[nodeIndex];
		int scale = (int)node.Size / CoreSize;
		CsgBox3D box = new CsgBox3D();
		AddChild(box);
		box.Size = Vector3.One * (float)(scale * CoreSize) * 0.9f;	
		StandardMaterial3D transparentMaterial = new StandardMaterial3D();
		transparentMaterial.AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
		transparentMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		box.Material = transparentMaterial;
		box.Position = node.Center;
	}

private void _generateNode(int nodeIndex)
	{
		PlanetOctreeNode node = _generationSnapshot[nodeIndex];
		Vector3I chunkPos = node.ChunkPos;
		float effectiveRadius = PlanetRadius * (float)CoreSize;
		int scale = ((int)node.Size / CoreSize);
		GD.Print($"{node.Depth} {chunkPos} {effectiveRadius} {scale}");

		VoxelData[] voxels = PlanetGenerator.GeneratePaddedChunkDataPlanet(chunkPos, Vector3.Zero, WorldSeed, effectiveRadius, scale);
		MeshData computedMesh = VoxelMesher.GenerateMarchingCubes(voxels, IsoLevel, 0, scale);

		if (computedMesh != null && computedMesh.Verticies != null && computedMesh.Verticies.Count > 0)
		{
			Vector3 worldChunkOffset = new Vector3(
				(chunkPos.X - TargetChunkPosition.X) * 16 * scale,
				(chunkPos.Y - TargetChunkPosition.Y) * 16 * scale,
				(chunkPos.Z - TargetChunkPosition.Z) * 16 * scale
			);

			for (int i = 0; i < computedMesh.Verticies.Count; i++)
			{
				computedMesh.Verticies[i] += worldChunkOffset;
			}

			Vector3[] vertices = computedMesh.Verticies.ToArray();
			int[] triangles = computedMesh.Triangles.ToArray();
			Vector3[] normals = computedMesh.Normals.ToArray();

			CallDeferred(MethodName._computeMesh, nodeIndex, vertices, triangles, normals);
		}
		else
		{
			node.MeshesPending = false;
		}
	}

	private void _computeMesh(int nodeIndex, Vector3[] verticies, int[] triangles, Vector3[] normals)
	{
		PlanetOctreeNode node = _visibleLeaves[nodeIndex];
		node.MeshesPending = false;

		if (verticies.Length == 0 || !GodotObject.IsInstanceValid(this) || node.IsLeaf == false)
		{
			return;
		}

		ArrayMesh arrayMesh = new ArrayMesh();
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		arrays[(int)Mesh.ArrayType.Vertex] = verticies;
		arrays[(int)Mesh.ArrayType.Index] = triangles;
		arrays[(int)Mesh.ArrayType.Normal] = normals;

		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		MeshInstance3D chunkMesh = new MeshInstance3D();
		chunkMesh.Mesh = arrayMesh;
		
		AddChild(chunkMesh);

		chunkMesh.Position = Vector3.Zero;
		chunkMesh.Scale = Vector3.One;

		node.RenderMesh = chunkMesh;
	}


	public override void _Process(double delta)
	{   
		if (Engine.IsEditorHint())
		{
			var editorInterface = EditorInterface.Singleton;
			SubViewport editorViewport = editorInterface.GetEditorViewport3D(0);

			if (editorViewport != null)
			{
				Camera3D editorCam = editorViewport.GetCamera3D();
				Vector3 cameraGlobalPos = editorCam.GlobalPosition;

				if (_lastCameraPosition.DistanceTo(cameraGlobalPos) > CameraMovementThreshold)
				{
					_lastCameraPosition = cameraGlobalPos;
					// GD.Print($"Camera moved, updating...");
					UpdateLOD(cameraGlobalPos);
				}
			}
		}
	}
}
