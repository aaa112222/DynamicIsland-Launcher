namespace DynamicIsland;

public enum DownloadStep
{
	Idle,
	DownloadingJson,
	DownloadingClient,
	DownloadingLibraries,
	DownloadingAssets,
	DownloadingModLoader,
	Completed,
	Failed,
	Cancelled
}
