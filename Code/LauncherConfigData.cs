namespace DynamicIsland;

public class LauncherConfigData
{
	public const int ConfigVersion = 1;

	public int Version { get; set; } = 1;


	public string PlayerName { get; set; } = "Player";


	public int MaxRamMb { get; set; } = 2048;


	public int RamType { get; set; } = 0;


	public int VersionIsolation { get; set; } = 0;


	public string WindowTitle { get; set; } = "";


	public string CustomInfo { get; set; } = "DIL";


	public int LauncherVisibility { get; set; } = 0;


	public int ProcessPriority { get; set; } = 1;


	public int WindowType { get; set; } = 1;


	public int WindowWidth { get; set; } = 854;


	public int WindowHeight { get; set; } = 480;


	public string JvmArgs { get; set; } = "";


	public string GameArgs { get; set; } = "";


	public string PreLaunchCommand { get; set; } = "";


	public bool WaitForPreLaunch { get; set; } = true;


	public int GcType { get; set; } = 0;


	public bool DisableJlw { get; set; } = false;


	public bool DisableLua { get; set; } = false;


	public bool UseHighPerfGpu { get; set; } = false;


	public bool OptimizeMemoryBeforeLaunch { get; set; } = false;


	public int SkinType { get; set; } = 0;


	public string SkinId { get; set; } = "";


	public int Theme { get; set; } = 0;


	public int Opacity { get; set; } = 100;


	public int Hue { get; set; } = 0;


	public int Saturation { get; set; } = 0;


	public int Lightness { get; set; } = 0;


	public int HueDelta { get; set; } = 0;


	public bool ShowLogo { get; set; } = true;


	public int BackgroundFit { get; set; } = 0;


	public int BackgroundOpacity { get; set; } = 100;


	public int BackgroundBlur { get; set; } = 0;


	public bool ColorfulBackground { get; set; } = true;


	public int MusicVolume { get; set; } = 50;


	public bool MusicRandom { get; set; } = false;


	public bool MusicAuto { get; set; } = false;


	public bool MusicStart { get; set; } = false;


	public bool MusicStop { get; set; } = false;


	public int LogoType { get; set; } = 1;


	public bool EnableAnimation { get; set; } = true;


	public int AnimationSpeed { get; set; } = 100;


	public int LinkLatencyMode { get; set; } = 0;


	public string LinkCustomPeer { get; set; } = "";


	public string LinkPort { get; set; } = "25565";


	public int LinkMaxPlayers { get; set; } = 8;


	public int LinkHeartbeat { get; set; } = 5;


	public int LinkTimeout { get; set; } = 30;


	public bool LinkUpnp { get; set; } = true;


	public bool LinkCompress { get; set; } = true;


	public bool LinkEncrypt { get; set; } = false;


	public int LinkRelayServer { get; set; } = 0;


	public int LinkMtu { get; set; } = 1;


	public bool LinkAllowSpectator { get; set; } = true;


	public bool LinkWhitelist { get; set; } = false;


	public bool LinkAutoKick { get; set; } = true;


	public bool LinkShowPing { get; set; } = true;


	public int DownloadSource { get; set; } = 1;


	public int VersionListSource { get; set; } = 1;


	public int MaxThreads { get; set; } = 63;


	public int SpeedLimit { get; set; } = 42;


	public bool VerifySsl { get; set; } = true;


	public int ModSource { get; set; } = 2;


	public int ModNameFormat { get; set; } = 1;


	public int ModLocalNameStyle { get; set; } = 0;


	public bool UpdateRelease { get; set; } = true;


	public bool UpdateSnapshot { get; set; } = false;


	public bool AutoChinese { get; set; } = true;


	public bool AutoCheckUpdate { get; set; } = true;


	public string Language { get; set; } = "en_US";


	public bool ShowDownloadSnapshot { get; set; } = true;


	public bool ShowDownloadOldBeta { get; set; } = false;


	public bool ShowDownloadAlpha { get; set; } = false;


	public bool ShowDownloadAprilFool { get; set; } = false;

}
