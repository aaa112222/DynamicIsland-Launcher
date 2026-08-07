namespace DynamicIsland;

public class DownloadTask
{
	public string Name { get; set; } = "";


	public string VersionId { get; set; } = "";


	public string LoaderName { get; set; } = "";


	public double Progress { get; set; }

	public double Speed { get; set; }

	public DownloadStep Step { get; set; }

	public string StepText { get; set; } = "";


	public int CurrentFileIndex { get; set; }

	public int TotalFiles { get; set; }

	public long TotalBytes { get; set; }

	public long DownloadedBytes { get; set; }
}
