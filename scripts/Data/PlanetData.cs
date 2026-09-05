using System.Collections.Concurrent;
using System.Numerics;
using Godot;

public class PlanetData
{
	public int Id { get; private set; }
	public string Name { get; set; }

	public ConcurrentDictionary<Vector3I, ChunkData> LoadedChunks { get; set; }

	public PlanetData(int id, string name)
	{
		Id = id;
		Name = name;
	}
}
