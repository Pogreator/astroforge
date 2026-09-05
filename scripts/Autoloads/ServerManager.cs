using Godot;
using System;
using System.Collections.Generic;

public partial class ServerManager : Node
{
	public static ServerManager Instance { get; private set; }

	public int MaxPlayers { get; set; } = 32;
	public string ServerName { get; set; } = "Test Server";
	public System.Collections.Generic.List<long> ConnectedPeers { get; private set; } = new();

	// Planet data#
	public Dictionary<int, PlanetData> Universe { get; private set; } = new();
	public const int ChunkSize = 16;
	public const int PaddedChunkSize = 18;
}
