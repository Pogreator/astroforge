using Godot;

public class ChunkData
{
	public Vector3I LocalPosition { get; private set; }
	public byte[] VoxelIso { get; set; }
	public uint[] VoxelType { get; set; }
	public int[] VoxelHealth { get; set; }
	public bool IsDirty { get; set; }

	public ChunkData(Vector3I position, int size)
	{
		LocalPosition = position;
		VoxelIso = new byte[size * size * size];
		VoxelType = new uint[size * size * size];
		VoxelHealth = new int[size * size * size];
	}
}
