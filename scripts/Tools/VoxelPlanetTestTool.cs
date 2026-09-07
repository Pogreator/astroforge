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

	private PlanetOctreeNode _rootNode;
	private Vector3 _lastCameraPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
	private List<PlanetOctreeNode> _visibleLeaves = new List<PlanetOctreeNode>();
	private readonly ConcurrentDictionary<Vector3I, ChunkData> _globalChunkMap = new ConcurrentDictionary<Vector3I, ChunkData>();
	public void Init()
	{
		if (_rootNode != null)
		{
			_rootNode.Collapse();
		}

		_globalChunkMap.Clear();

		float baseChunkSize = 16.0f;
		float chunksAcrossDiameter = PlanetRadius;

		int powerOfTwoChunks = 1;
		while (powerOfTwoChunks < chunksAcrossDiameter)
		{
			powerOfTwoChunks *= 2;
		}

		float calculatedRootSize = powerOfTwoChunks * baseChunkSize;

		_rootNode = new PlanetOctreeNode(Vector3.Zero, calculatedRootSize, 0, Vector3I.Zero); 

		int targetDepth = Mathf.RoundToInt(Mathf.Log(powerOfTwoChunks) / Mathf.Log(2));

		if (targetDepth > 0)
		{
			_rootNode.SubdivideFully(targetDepth);
		}

		_lastCameraPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		GD.Print($"Voxel Planet Octree Initialised! Root Size: {calculatedRootSize}, Pre-built Depth: {targetDepth}");
	}
	public override void _Ready()
	{
		SetProcess(_runRealTimeGeneration);
		if (_rootNode == null)
		{
			Init();
		}
	}

	private void ProcessLODUpdate(Vector3 cameraPos)
	{
		if (_rootNode == null) return;

		float[] lodDistances = new float[] { LOD0Distance, LOD1Distance, LOD2Distance, LOD3Distance, LOD4Distance };
		
		_rootNode.UpdateLOD(cameraPos, maxDepth: 4, lodDistances);

		_visibleLeaves.Clear();
		_rootNode.GetActiveLeafNodes(_visibleLeaves);

		GD.Print($"Starting mesh generation...");

		foreach (var node in _visibleLeaves)
		{
			if (node.RenderMesh == null && !node.MeshesPending)
			{
				node.MeshesPending = true;
				
				WorkerThreadPool.AddTask(Callable.From(() => AsyncGenerateChunkMesh(node)));
			}
		}
	}

	private void AsyncGenerateChunkMesh(PlanetOctreeNode node)
	{
		int chunkSize = (int)node.Size;

		

		ArrayMesh arrayMesh = new ArrayMesh();
		Callable.From(() => OnMeshGenerationCompleted(node, arrayMesh)).CallDeferred();
	}

	private void OnMeshGenerationCompleted(PlanetOctreeNode node, ArrayMesh generatedMesh)
	{
		if (!node.MeshesPending)
		{
			generatedMesh.Dispose();
			return;
		}

		MeshInstance3D meshInstance = new MeshInstance3D();
		meshInstance.Mesh = generatedMesh;
		meshInstance.GlobalPosition = node.Center;
		
		AddChild(meshInstance);
		
		node.RenderMesh = meshInstance;
		node.MeshesPending = false;
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
					GD.Print($"Camera moved, updating...");
					ProcessLODUpdate(cameraGlobalPos);
				}
			}
		}
	}
}
