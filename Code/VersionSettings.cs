namespace DynamicIsland;

public class VersionSettings
{
	public string VersionId { get; set; } = "";


	public string DisplayName { get; set; } = "";


	public bool UseCustomMemory { get; set; } = false;


	public int CustomMemoryMb { get; set; } = 2048;


	public string JvmArgs { get; set; } = "";


	public string GameArgs { get; set; } = "";

}
