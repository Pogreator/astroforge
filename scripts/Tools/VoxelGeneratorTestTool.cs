using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

[Tool]
public partial class VoxelGeneratorTestTool : MeshInstance3D
{
	[Export] public Vector3I TargetChunkPosition { get; set; } = Vector3I.Zero;
	[Export] public int WorldSeed { get; set; } = 0;
	[Export] public byte IsoLevel { get; set; } = 128;
	[Export(PropertyHint.Range, "0,4,1")] public int LOD { get; set; } = 0;
	
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
		GD.Print($"[Tool] Generating voxel for chunk {TargetChunkPosition}");

		Stopwatch globalTimer = Stopwatch.StartNew();
		Stopwatch phaseTimer = new Stopwatch();

		var mockWorldData = new ConcurrentDictionary<Vector3I, ChunkData>();

		phaseTimer.Start();
		await Task.Run(() =>
		{
		   for (int x = -1; x <= 1; x++)
			{
				for (int y = -1; y <= 1; y++)
				{
					for (int z = -1; z <= 1; z++)
					{
						Vector3I relativePos = TargetChunkPosition + new Vector3I(x, y, z);
						byte[] rawBytes = PlanetGenerator.GenerateChunkDataTest(relativePos, WorldSeed);
						
						var mockChunk = new ChunkData(relativePos, 16) { VoxelIso = rawBytes };
						mockWorldData[relativePos] = mockChunk;
					}
				}
			}
		});
		phaseTimer.Stop();
		long noiseTimeMs = phaseTimer.ElapsedMilliseconds;

		phaseTimer.Restart();
		byte[] paddedBuffer = await Task.Run(() => 
			VoxelBufferUtility.BuildPaddedBuffer(TargetChunkPosition, mockWorldData)
		);
		phaseTimer.Stop();
		long paddingTimeMs = phaseTimer.ElapsedMilliseconds;

		phaseTimer.Restart();
		MeshData computedMesh = await Task.Run(() => 
			VoxelMesher.GenerateMarchingCubes(paddedBuffer, IsoLevel, LOD)
		);
		phaseTimer.Stop();
		long meshingTimeMs = phaseTimer.ElapsedMilliseconds;

		if (computedMesh.Verticies.Count == 0)
		{
			GD.PrintErr("[Tool] Mesh generation completed, but resulted in 0 vertices (Empty/Solid field)");
			return;
		}

		phaseTimer.Restart();
		var arrayMesh = new ArrayMesh();
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		arrays[(int)Mesh.ArrayType.Vertex] = computedMesh.Verticies.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = computedMesh.Triangles.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = computedMesh.Normals.ToArray();

		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		this.Mesh = arrayMesh;
		phaseTimer.Stop();
		long engineTimeMs = phaseTimer.ElapsedMilliseconds;

		globalTimer.Stop();

		GD.Print($"[Tool] Success! Generated Voxel Mesh with {computedMesh.Verticies.Count} vertices.");

		GD.Print($"[Tool] Noise time: {noiseTimeMs}");
		GD.Print($"[Tool] Padding time: {paddingTimeMs}");
		GD.Print($"[Tool] Meshing time: {meshingTimeMs}");
		GD.Print($"[Tool] Engine time: {engineTimeMs}");
		GD.Print($"[Tool] Total time: {noiseTimeMs + paddingTimeMs + meshingTimeMs + engineTimeMs}");
	}
}
