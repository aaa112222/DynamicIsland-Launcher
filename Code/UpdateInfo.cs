using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DynamicIsland;

public class UpdateInfo
{
	[JsonPropertyName("tag_name")]
	public string TagName { get; set; } = "";


	[JsonPropertyName("name")]
	public string Name { get; set; } = "";


	[JsonPropertyName("body")]
	public string Body { get; set; } = "";


	[JsonPropertyName("html_url")]
	public string HtmlUrl { get; set; } = "";


	[JsonPropertyName("assets")]
	public List<UpdateAsset> Assets { get; set; } = new List<UpdateAsset>();


	[JsonPropertyName("prerelease")]
	public bool Prerelease { get; set; }

	[JsonPropertyName("published_at")]
	public string PublishedAt { get; set; } = "";

}
