using System.Text.Json.Serialization;

namespace DynamicIsland;

public class UpdateAsset
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";


	[JsonPropertyName("browser_download_url")]
	public string BrowserDownloadUrl { get; set; } = "";


	[JsonPropertyName("size")]
	public long Size { get; set; }
}
