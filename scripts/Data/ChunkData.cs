using System.IO.IsolatedStorage;
using Godot;

public struct VoxelData
{
	byte Iso { get; set; }
	ushort Type { get; set; }
	short Health { get; set; }

	public VoxelData(byte iso, ushort type, short health)
	{
		Iso = iso;
		Type = type;
		Health = health;
	}
}

public class ChunkData
{
	public Vector3I LocalPosition { get; private set; }
	public VoxelData[] Voxels { get; set; }
	public bool IsDirty { get; set; }

	public ChunkData(Vector3I position, int size)
	{
		LocalPosition = position;
		Voxels = new VoxelData[size * size * size];
	}
}
