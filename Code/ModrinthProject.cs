using System.Collections.Generic;

namespace DynamicIsland;

public class ModrinthProject
{
	public string ProjectId { get; set; } = "";


	public string Slug { get; set; } = "";


	public string Title { get; set; } = "";


	public string Description { get; set; } = "";


	public string IconUrl { get; set; } = "";


	public long Downloads { get; set; }

	public List<string> GameVersions { get; set; } = new List<string>();


	public List<string> Loaders { get; set; } = new List<string>();

}
