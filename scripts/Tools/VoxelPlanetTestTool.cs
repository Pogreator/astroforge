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
	[Export] public float CameraMovementThreshold { get; set; } = 8.0f; 
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

	public override void _Ready()
	{
		SetProcess(_runRealTimeGeneration);
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
			}
		}
	}
}
