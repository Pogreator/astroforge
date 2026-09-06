using System.IO.IsolatedStorage;
using Godot;

public struct VoxelData
{
	public byte Iso { get; set; }
	public ushort Type { get; set; }
	public short Health { get; set; }

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
	public int Size { get; set; }
	public bool IsDirty { get; set; }

	public ChunkData(Vector3I position, int size)
	{
		LocalPosition = position;
		Size = size;
		Voxels = new VoxelData[size * size * size];
		IsDirty = true;
	}	

	public int GetIndex(int x, int y, int z)
	{
		return x + (y * Size) + (z * Size * Size);
	}

	public VoxelData GetVoxel(int x, int y, int z)
	{
		if (x < 0 || x >= Size || y < 0 || y >= Size || z < 0 || z >= Size)
		{
			return default; // Return empty voxel if out of bounds
		}
		return Voxels[GetIndex(x, y, z)];
	}

	public void SetVoxel(int x, int y, int z, VoxelData data)
	{
		if (x >= 0 && x < Size && y >= 0 && y < Size && z >= 0 && z < Size)
		{
			Voxels[GetIndex(x, y, z)] = data;
			IsDirty = true;
		}
	}
}
