using Godot;

public class ChunkData
{
	public Vector3I LocalPosition { get; private set; }
	public byte[] VoxelData { get; set; }
	public bool IsDirty { get; set; }

	public ChunkData(Vector3I position, int size)
	{
		LocalPosition = position;
		VoxelData = new byte[size * size * size];
	}
}
