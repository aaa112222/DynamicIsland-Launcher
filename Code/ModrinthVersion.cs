using System.Collections.Generic;

namespace DynamicIsland;

public class ModrinthVersion
{
	public string Id { get; set; } = "";


	public string Name { get; set; } = "";


	public string VersionNumber { get; set; } = "";


	public string DatePublished { get; set; } = "";


	public string VersionType { get; set; } = "release";


	public List<string> GameVersions { get; set; } = new List<string>();


	public List<string> Loaders { get; set; } = new List<string>();


	public string DownloadUrl { get; set; } = "";


	public string FileName { get; set; } = "";

}
