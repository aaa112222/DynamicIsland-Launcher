using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DynamicIsland;

public static class DownloadManager
{
	private static CancellationTokenSource? _cancellationTokenSource;

	private static DispatcherTimer? _speedTimer;

	private static DateTime _lastSpeedUpdate = DateTime.Now;

	private static long _lastBytes = 0L;

	private static long _currentBytes = 0L;

	private const string BmclApi = "https://bmclapi2.bangbang93.com";

	private const string OfficialApi = "https://launchermeta.mojang.com";

	private const int MaxConcurrency = 16;

	private const int DownloadRetryCount = 3;

	public static DownloadTask? CurrentTask { get; private set; }

	public static bool IsDownloading => CurrentTask != null && CurrentTask.Step != DownloadStep.Completed && CurrentTask.Step != DownloadStep.Failed && CurrentTask.Step != DownloadStep.Cancelled;

	public static string MinecraftPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "minecraft");


	public static event Action<DownloadTask>? ProgressChanged;

	public static event Action<DownloadTask>? DownloadCompleted;

	public static event Action<DownloadTask, Exception>? DownloadFailed;

	public static event Action? DownloadCancelled;

	private static int GetMaxThreads()
	{
		int maxThreads = LauncherConfig.Current.MaxThreads;
		return (maxThreads > 0) ? maxThreads : 16;
	}

	private static int GetSpeedLimitBytes()
	{
		int speedLimit = LauncherConfig.Current.SpeedLimit;
		if (speedLimit >= 42)
		{
			return 0;
		}
		return speedLimit * 1024 * 1024;
	}

	private static int GetSourceMode()
	{
		return LauncherConfig.Current.DownloadSource;
	}

	private static string ApplySourcePolicy(string mirrorUrl, string officialUrl)
	{
		int sourceMode = GetSourceMode();
		if (1 == 0)
		{
		}
		string result = sourceMode switch
		{
			0 => mirrorUrl, 
			2 => officialUrl, 
			_ => mirrorUrl, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static bool ShouldFallbackToOfficial()
	{
		return GetSourceMode() != 2;
	}

	private static bool ShouldFallbackToMirror()
	{
		return GetSourceMode() != 0;
	}

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(30L);
		httpClient.DefaultRequestHeaders.Add("User-Agent", "DIL/1.0");
		return httpClient;
	}

	private static async Task<string> TryGetString(HttpClient client, string url)
	{
		int mode = GetSourceMode();
		string primary = ((mode == 2) ? MirrorToOfficial(url) : url);
		string fallback = ((mode == 2) ? url : MirrorToOfficial(url));
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20L));
		try
		{
			return await client.GetStringAsync(primary, cts.Token);
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
		{
			if (fallback != primary)
			{
				using (CancellationTokenSource cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(20L)))
				{
					return await client.GetStringAsync(fallback, cts2.Token);
				}
			}
			throw;
		}
	}

	private static async Task<byte[]> TryGetByteArray(HttpClient client, string url)
	{
		int mode = GetSourceMode();
		string primary = ((mode == 2) ? MirrorToOfficial(url) : url);
		string fallback = ((mode == 2) ? url : MirrorToOfficial(url));
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20L));
		try
		{
			return await client.GetByteArrayAsync(primary, cts.Token);
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
		{
			if (fallback != primary)
			{
				using (CancellationTokenSource cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(20L)))
				{
					return await client.GetByteArrayAsync(fallback, cts2.Token);
				}
			}
			throw;
		}
	}

	private static string MirrorToOfficial(string url)
	{
		if (url.Contains("minecraftforge") || url.Contains("neoforged"))
		{
			return url.Replace("https://bmclapi2.bangbang93.com/maven/", "https://files.minecraftforge.net/maven/");
		}
		if (url.Contains("fabricmc"))
		{
			return url.Replace("https://bmclapi2.bangbang93.com/maven/", "https://maven.fabricmc.net/");
		}
		if (url.Contains("quiltmc"))
		{
			return url.Replace("https://bmclapi2.bangbang93.com/maven/", "https://maven.quiltmc.org/");
		}
		return url.Replace("https://bmclapi2.bangbang93.com/mc/game/", "https://piston-meta.mojang.com/mc/game/").Replace("https://bmclapi2.bangbang93.com/maven/", "https://libraries.minecraft.net/").Replace("https://bmclapi2.bangbang93.com/assets/", "https://resources.download.minecraft.net/assets/")
			.Replace("https://bmclapi2.bangbang93.com", "https://piston-meta.mojang.com");
	}

	private static async Task DownloadWithRetryAsync(HttpClient client, string url, string filePath, int timeoutSec = 30)
	{
		Exception lastEx = null;
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
				using HttpResponseMessage resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
				resp.EnsureSuccessStatusCode();
				string dir = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(dir))
				{
					Directory.CreateDirectory(dir);
				}
				string tmp = filePath + ".tmp";
				FileStream fs = File.Create(tmp);
				try
				{
					await resp.Content.CopyToAsync(fs);
				}
				finally
				{
					if (fs != null)
					{
						await fs.DisposeAsync();
					}
				}
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
				File.Move(tmp, filePath);
				return;
			}
			catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException || ex is IOException) ? 1 : 0) != 0)
			{
				lastEx = ex;
				if (attempt < 3)
				{
					await Task.Delay(500 * attempt);
				}
			}
		}
		throw lastEx;
	}

	private static async Task DownloadFileWithProgressAsync(HttpClient client, string url, string filePath, int timeoutSec = 120, string? label = null)
	{
		Exception lastEx = null;
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
				using HttpResponseMessage resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
				resp.EnsureSuccessStatusCode();
				long contentLength = resp.Content.Headers.ContentLength.GetValueOrDefault(-1L);
				string dir = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(dir))
				{
					Directory.CreateDirectory(dir);
				}
				string tmp = filePath + ".tmp";
				DateTime lastProgressUpdate = DateTime.MinValue;
				FileStream fs = File.Create(tmp);
				try
				{
					Stream stream = await resp.Content.ReadAsStreamAsync(cts.Token);
					try
					{
						byte[] buffer = new byte[81920];
						long totalRead = 0L;
						while (true)
						{
							int num;
							int read = (num = await stream.ReadAsync(buffer, cts.Token));
							if (num <= 0)
							{
								break;
							}
							await fs.WriteAsync(buffer.AsMemory(0, read), cts.Token);
							totalRead += read;
							Interlocked.Exchange(ref _currentBytes, totalRead);
							if (contentLength <= 0 || CurrentTask == null)
							{
								continue;
							}
							DateTime now = DateTime.Now;
							if (!((now - lastProgressUpdate).TotalMilliseconds >= 200.0))
							{
								continue;
							}
							lastProgressUpdate = now;
							lock (CurrentTask)
							{
								CurrentTask.TotalBytes = contentLength;
								CurrentTask.DownloadedBytes = totalRead;
								if (!string.IsNullOrEmpty(label))
								{
									CurrentTask.StepText = $"{label} ({FormatBytes(totalRead)} / {FormatBytes(contentLength)})";
								}
								DownloadManager.ProgressChanged?.Invoke(CurrentTask);
							}
						}
					}
					finally
					{
						if (stream != null)
						{
							await stream.DisposeAsync();
						}
					}
				}
				finally
				{
					if (fs != null)
					{
						await fs.DisposeAsync();
					}
				}
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
				File.Move(tmp, filePath);
				return;
			}
			catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException || ex is IOException) ? 1 : 0) != 0)
			{
				lastEx = ex;
				try
				{
					if (File.Exists(filePath + ".tmp"))
					{
						File.Delete(filePath + ".tmp");
					}
				}
				catch
				{
				}
				if (attempt < 3)
				{
					await Task.Delay(500 * attempt);
				}
			}
		}
		throw lastEx;
	}

	private static string FormatBytes(long bytes)
	{
		if (!((double)bytes >= 1048576.0))
		{
			if (!((double)bytes >= 1024.0))
			{
				return $"{bytes}B";
			}
			return $"{(double)bytes / 1024.0:F1}KB";
		}
		return $"{(double)bytes / 1048576.0:F1}MB";
	}

	internal static async Task<byte[]> DownloadBytesWithRetryAsync(HttpClient client, string url, int timeoutSec = 30)
	{
		Exception lastEx = null;
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
				return await client.GetByteArrayAsync(url, cts.Token);
			}
			catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException || ex is IOException) ? 1 : 0) != 0)
			{
				lastEx = ex;
				if (attempt < 3)
				{
					await Task.Delay(500 * attempt);
				}
			}
		}
		throw lastEx;
	}

	private static async Task<HttpResponseMessage> GetWithRetryAsync(HttpClient client, string url, int timeoutSec = 30)
	{
		Exception lastEx = null;
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			try
			{
				using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
				HttpResponseMessage resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
				resp.EnsureSuccessStatusCode();
				return resp;
			}
			catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException || ex is IOException) ? 1 : 0) != 0)
			{
				lastEx = ex;
				if (attempt < 3)
				{
					await Task.Delay(500 * attempt);
				}
			}
		}
		throw lastEx;
	}

	private static bool ValidateFileSha1(string filePath, string expectedSha1)
	{
		if (string.IsNullOrEmpty(expectedSha1) || !File.Exists(filePath))
		{
			return false;
		}
		try
		{
			using SHA1 sHA = SHA1.Create();
			using FileStream inputStream = File.OpenRead(filePath);
			byte[] array = sHA.ComputeHash(inputStream);
			string a = BitConverter.ToString(array).Replace("-", "").ToLowerInvariant();
			return string.Equals(a, expectedSha1.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static bool IsBaseVersionComplete(string baseGameVersion)
	{
		try
		{
			string path = Path.Combine(MinecraftPath, "versions");
			string text = Path.Combine(path, baseGameVersion);
			if (!Directory.Exists(text))
			{
				return false;
			}
			string text2 = Path.Combine(text, baseGameVersion + ".jar");
			if (!File.Exists(text2) || new FileInfo(text2).Length < 1048576)
			{
				return false;
			}
			string path2 = Path.Combine(text, baseGameVersion + ".json");
			if (!File.Exists(path2))
			{
				return false;
			}
			string text3 = Path.Combine(MinecraftPath, "assets");
			if (!Directory.Exists(text3) || !Directory.GetFiles(text3, "*", SearchOption.AllDirectories).Any())
			{
				return false;
			}
			string path3 = Path.Combine(text3, "objects");
			if (!Directory.Exists(path3))
			{
				return false;
			}
			int num = Directory.GetFiles(path3, "*", SearchOption.AllDirectories).Length;
			return num > 100;
		}
		catch
		{
			return false;
		}
	}

	private static IEnumerable<string> GetAssetUrlCandidates(string mirrorUrl, string officialUrl)
	{
		int mode = GetSourceMode();
		if (mode == 2)
		{
			yield return officialUrl;
			yield return mirrorUrl;
		}
		else
		{
			yield return mirrorUrl;
			yield return officialUrl;
		}
	}

	private static async Task TryDownloadFile(HttpClient client, string url, string filePath)
	{
		try
		{
			await DownloadWithRetryAsync(client, url, filePath);
		}
		catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException || ex is IOException) ? 1 : 0) != 0)
		{
			string official = MirrorToOfficial(url);
			if (official != url)
			{
				await DownloadWithRetryAsync(client, official, filePath);
				return;
			}
			if (!(ex is Exception source))
			{
				throw ex;
			}
			ExceptionDispatchInfo.Capture(source).Throw();
		}
	}

	public static void StartDownload(string versionId, string loaderName = "", string loaderVersion = "")
	{
		if (!IsDownloading)
		{
			_cancellationTokenSource = new CancellationTokenSource();
			Directory.CreateDirectory(MinecraftPath);
			string name = (string.IsNullOrEmpty(loaderName) ? ("Minecraft " + versionId) : (versionId + " - " + loaderName + (string.IsNullOrEmpty(loaderVersion) ? "" : (" " + loaderVersion))));
			CurrentTask = new DownloadTask
			{
				Name = name,
				VersionId = versionId,
				LoaderName = loaderName,
				Progress = 0.0,
				Speed = 0.0,
				Step = DownloadStep.DownloadingJson,
				StepText = LanguageManager.Get("DlVersionManifest"),
				CurrentFileIndex = 0,
				TotalFiles = 0
			};
			_currentBytes = 0L;
			_lastBytes = 0L;
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			StartSpeedMonitor();
			DownloadAsync(versionId, loaderName, loaderVersion);
		}
	}

	public static void CancelDownload()
	{
		if (IsDownloading && CurrentTask != null)
		{
			_cancellationTokenSource?.Cancel();
			_speedTimer?.Stop();
			CurrentTask.Step = DownloadStep.Cancelled;
			CurrentTask.StepText = LanguageManager.Get("DlCancelled");
			CurrentTask.Speed = 0.0;
			DownloadManager.DownloadCancelled?.Invoke();
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
	}

	public static void StartResourceDownload(string name, string type, Func<Task> downloadAction)
	{
		if (!IsDownloading)
		{
			_cancellationTokenSource = new CancellationTokenSource();
			Directory.CreateDirectory(MinecraftPath);
			CurrentTask = new DownloadTask
			{
				Name = name,
				VersionId = type,
				LoaderName = type,
				Progress = 0.0,
				Speed = 0.0,
				Step = DownloadStep.DownloadingJson,
				StepText = string.Format(LanguageManager.Get("DlDownloading"), name),
				CurrentFileIndex = 0,
				TotalFiles = 1
			};
			_currentBytes = 0L;
			_lastBytes = 0L;
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			StartSpeedMonitor();
			DownloadResourceAsync(downloadAction);
		}
	}

	private static async Task DownloadResourceAsync(Func<Task> downloadAction)
	{
		try
		{
			UpdateStep(DownloadStep.DownloadingClient, LanguageManager.Get("DlDownloadingFile"), 10.0, 0, 1);
			await downloadAction();
			UpdateStep(DownloadStep.Completed, LanguageManager.Get("DlComplete"), 100.0, 1, 1);
			_speedTimer?.Stop();
			if (CurrentTask != null)
			{
				CurrentTask.Speed = 0.0;
				DownloadManager.DownloadCompleted?.Invoke(CurrentTask);
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			_speedTimer?.Stop();
			if (CurrentTask != null)
			{
				CurrentTask.Step = DownloadStep.Failed;
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlFailed"), ex.Message);
				DownloadManager.DownloadFailed?.Invoke(CurrentTask, ex);
			}
		}
	}

	private static void StartSpeedMonitor()
	{
		_speedTimer?.Stop();
		_lastSpeedUpdate = DateTime.Now;
		_lastBytes = _currentBytes;
		_speedTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500L, 0L)
		};
		_speedTimer.Tick += delegate
		{
			if (CurrentTask != null)
			{
				DateTime now = DateTime.Now;
				double totalSeconds = (now - _lastSpeedUpdate).TotalSeconds;
				if (totalSeconds > 0.0)
				{
					long num = _currentBytes - _lastBytes;
					if (num >= 0)
					{
						double num2 = (double)num / totalSeconds;
						if (num2 <= 524288000.0 && totalSeconds >= 0.3)
						{
							CurrentTask.Speed = num2;
						}
						else if (CurrentTask.Speed > 0.0)
						{
							CurrentTask.Speed *= 0.9;
						}
					}
					_lastBytes = _currentBytes;
				}
				else if (CurrentTask.Speed > 0.0)
				{
					CurrentTask.Speed *= 0.8;
				}
				_lastSpeedUpdate = now;
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
		};
		_speedTimer.Start();
	}

	private static async Task DownloadAsync(string versionId, string loaderName, string loaderVersion)
	{
		try
		{
			using HttpClient client = CreateClient();
			UpdateStep(DownloadStep.DownloadingJson, LanguageManager.Get("DlVersionManifest"), 0.0, 0, 1);
			string versionJson = await DownloadVersionJson(client, versionId);
			_currentBytes += versionJson.Length;
			string versionDir = Path.Combine(MinecraftPath, "versions", versionId);
			Directory.CreateDirectory(versionDir);
			string jsonPath = Path.Combine(versionDir, versionId + ".json");
			await File.WriteAllTextAsync(jsonPath, versionJson);
			bool isModdedVersion = !string.IsNullOrEmpty(loaderName);
			bool shouldSkipBaseDownload = false;
			if (isModdedVersion)
			{
				shouldSkipBaseDownload = IsBaseVersionComplete(versionId);
				if (shouldSkipBaseDownload)
				{
					if (CurrentTask != null)
					{
						CurrentTask.StepText = string.Format(LanguageManager.Get("DlBaseVersionExists"), versionId);
					}
					DownloadManager.ProgressChanged?.Invoke(CurrentTask);
					await Task.Delay(500);
				}
			}
			if (!shouldSkipBaseDownload)
			{
				UpdateStep(DownloadStep.DownloadingClient, LanguageManager.Get("DlClient"), 5.0, 0, 1);
				await DownloadClientJar(client, versionId, versionJson, versionDir);
				UpdateStep(DownloadStep.DownloadingLibraries, LanguageManager.Get("DlLibraries"), 20.0, 0, 0);
				await DownloadLibraries(client, versionJson);
				UpdateStep(DownloadStep.DownloadingAssets, LanguageManager.Get("DlAssets"), 50.0, 0, 0);
				await DownloadAssets(client, versionJson);
			}
			else
			{
				double currentProgress = 50.0;
				if (CurrentTask != null)
				{
					CurrentTask.Progress = currentProgress;
					DownloadManager.ProgressChanged?.Invoke(CurrentTask);
				}
			}
			if (!string.IsNullOrEmpty(loaderName))
			{
				UpdateStep(DownloadStep.DownloadingModLoader, string.Format(arg0: string.IsNullOrEmpty(loaderVersion) ? loaderName : (loaderName + " " + loaderVersion), format: LanguageManager.Get("DlModLoader")), 85.0, 0, 0);
				await DownloadModLoader(client, versionId, loaderName, loaderVersion);
			}
			UpdateStep(DownloadStep.Completed, LanguageManager.Get("DlComplete"), 100.0, 0, 0);
			_speedTimer?.Stop();
			if (CurrentTask != null)
			{
				CurrentTask.Speed = 0.0;
				DownloadManager.DownloadCompleted?.Invoke(CurrentTask);
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			_speedTimer?.Stop();
			if (CurrentTask != null)
			{
				CurrentTask.Step = DownloadStep.Failed;
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlFailed"), ex.Message);
				DownloadManager.DownloadFailed?.Invoke(CurrentTask, ex);
			}
		}
	}

	private static void UpdateStep(DownloadStep step, string text, double progress, int currentFile, int totalFiles)
	{
		if (CurrentTask != null)
		{
			CurrentTask.Step = step;
			CurrentTask.StepText = text;
			CurrentTask.Progress = progress;
			CurrentTask.CurrentFileIndex = currentFile;
			CurrentTask.TotalFiles = totalFiles;
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
	}

	private static async Task<string> DownloadVersionJson(HttpClient client, string versionId)
	{
		string manifestUrl = "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json";
		JsonDocument doc = JsonDocument.Parse(await TryGetString(client, manifestUrl));
		string versionUrl2 = null;
		foreach (JsonElement v in doc.RootElement.GetProperty("versions").EnumerateArray())
		{
			if (v.GetProperty("id").GetString() == versionId)
			{
				versionUrl2 = v.GetProperty("url").GetString();
				break;
			}
		}
		if (string.IsNullOrEmpty(versionUrl2))
		{
			throw new Exception(string.Format(LanguageManager.Get("ErrVersionNotFound"), versionId));
		}
		versionUrl2 = versionUrl2.Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com");
		return await TryGetString(client, versionUrl2);
	}

	private static async Task DownloadClientJar(HttpClient client, string versionId, string versionJson, string versionDir)
	{
		JsonDocument vDoc = JsonDocument.Parse(versionJson);
		string clientUrl = null;
		if (vDoc.RootElement.TryGetProperty("downloads", out var downloads) && downloads.TryGetProperty("client", out var clientInfo) && clientInfo.TryGetProperty("url", out var urlProp))
		{
			clientUrl = urlProp.GetString();
		}
		if (string.IsNullOrEmpty(clientUrl))
		{
			throw new Exception(LanguageManager.Get("ErrNoClientUrl"));
		}
		clientUrl = clientUrl.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com").Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com");
		string jarPath = Path.Combine(versionDir, versionId + ".jar");
		HttpResponseMessage response;
		try
		{
			response = await GetWithRetryAsync(client, clientUrl);
			response.EnsureSuccessStatusCode();
		}
		catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
		{
			string officialUrl = MirrorToOfficial(clientUrl);
			if (!(officialUrl != clientUrl))
			{
				throw;
			}
			response = await GetWithRetryAsync(client, officialUrl);
			response.EnsureSuccessStatusCode();
		}
		long totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
		using Stream stream = await response.Content.ReadAsStreamAsync();
		using FileStream fileStream = File.Create(jarPath);
		int speedLimitBytes = GetSpeedLimitBytes();
		byte[] buffer = new byte[8192];
		long totalRead = 0L;
		Stopwatch sw = Stopwatch.StartNew();
		long bytesInWindow = 0L;
		while (true)
		{
			int num;
			int bytesRead = (num = await stream.ReadAsync(buffer, 0, buffer.Length));
			if (num <= 0)
			{
				break;
			}
			await fileStream.WriteAsync(buffer, 0, bytesRead);
			totalRead += bytesRead;
			_currentBytes += bytesRead;
			if (speedLimitBytes > 0)
			{
				bytesInWindow += bytesRead;
				double elapsedSec = sw.Elapsed.TotalSeconds;
				if (elapsedSec > 0.0 && (double)bytesInWindow / elapsedSec > (double)speedLimitBytes)
				{
					int delayMs = (int)(((double)bytesInWindow / (double)speedLimitBytes - elapsedSec) * 1000.0);
					if (delayMs > 0 && delayMs < 1000)
					{
						await Task.Delay(delayMs);
					}
					if (elapsedSec >= 1.0)
					{
						sw.Restart();
						bytesInWindow = 0L;
					}
				}
			}
			if (CurrentTask != null)
			{
				if (totalBytes > 0)
				{
					CurrentTask.Progress = 5.0 + (double)totalRead * 15.0 / (double)totalBytes;
				}
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlClientProgress"), (double)totalRead / 1024.0 / 1024.0, (double)totalBytes / 1024.0 / 1024.0);
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
		}
	}

	private static async Task DownloadLibrariesWithRetry(HttpClient client, string versionJson)
	{
		HttpClient client2 = client;
		JsonDocument doc = JsonDocument.Parse(versionJson);
		string libDir = Path.Combine(MinecraftPath, "libraries");
		Directory.CreateDirectory(libDir);
		List<(string url, string path)> libList = new List<(string, string)>();
		if (doc.RootElement.TryGetProperty("libraries", out var libs))
		{
			foreach (JsonElement lib in libs.EnumerateArray())
			{
				if (lib.TryGetProperty("downloads", out var dl) && dl.TryGetProperty("artifact", out var artifact) && artifact.TryGetProperty("url", out var urlProp) && artifact.TryGetProperty("path", out var pathProp))
				{
					string url4 = urlProp.GetString();
					string path3 = pathProp.GetString();
					if (!string.IsNullOrEmpty(url4) && !string.IsNullOrEmpty(path3))
					{
						url4 = MirrorUrlToBmcl(url4);
						libList.Add((url4, path3));
					}
				}
				else
				{
					if (lib.TryGetProperty("name", out var nameProp) && lib.TryGetProperty("url", out var mavenUrlProp))
					{
						string name = nameProp.GetString();
						string mavenUrl2 = mavenUrlProp.GetString();
						if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(mavenUrl2))
						{
							string[] parts = name.Split(':');
							if (parts.Length >= 3)
							{
								string groupPath = parts[0].Replace('.', '/');
								string artifactName = parts[1];
								string version = parts[2];
								string path4 = $"{groupPath}/{artifactName}/{version}/{artifactName}-{version}.jar";
								mavenUrl2 = MirrorUrlToBmcl(mavenUrl2);
								libList.Add((mavenUrl2 + "/" + path4, path4));
							}
						}
					}
					nameProp = default(JsonElement);
					mavenUrlProp = default(JsonElement);
				}
				dl = default(JsonElement);
				artifact = default(JsonElement);
				urlProp = default(JsonElement);
				pathProp = default(JsonElement);
			}
		}
		if (libList.Count == 0)
		{
			return;
		}
		int total = libList.Count;
		int completedCount = 0;
		int skippedCount = 0;
		int failedCount = 0;
		ConcurrentBag<(string url, string path)> failedFiles = new ConcurrentBag<(string, string)>();
		int maxThreads = Math.Max(4, Math.Min(GetMaxThreads(), 32));
		SemaphoreSlim semaphore = new SemaphoreSlim(maxThreads);
		try
		{
			List<Task> tasks = new List<Task>();
			if (CurrentTask != null)
			{
				CurrentTask.TotalFiles = total;
				CurrentTask.CurrentFileIndex = 0;
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
			foreach (var item in libList)
			{
				var (url, path) = item;
				await semaphore.WaitAsync();
				tasks.Add(Task.Run(async delegate
				{
					try
					{
						string localPath2 = Path.Combine(libDir, path.Replace('/', Path.DirectorySeparatorChar));
						bool needDownload = true;
						if (File.Exists(localPath2) && new FileInfo(localPath2).Length > 0)
						{
							needDownload = false;
							Interlocked.Increment(ref skippedCount);
						}
						if (needDownload)
						{
							Directory.CreateDirectory(Path.GetDirectoryName(localPath2));
							bool success2 = false;
							int toSec = 20;
							foreach (string candidate2 in GetUrlCandidates(url, path))
							{
								try
								{
									await DownloadWithRetryAsync(client2, candidate2, localPath2, toSec);
									if (File.Exists(localPath2) && new FileInfo(localPath2).Length > 0)
									{
										success2 = true;
										break;
									}
								}
								catch
								{
								}
							}
							if (!success2)
							{
								Interlocked.Increment(ref failedCount);
								failedFiles.Add((url, path));
							}
						}
						int done = Interlocked.Increment(ref completedCount);
						if (CurrentTask != null && total > 0)
						{
							DateTime now = DateTime.Now;
							if ((now - _lastSpeedUpdate).TotalMilliseconds >= 200.0)
							{
								_lastSpeedUpdate = now;
								lock (CurrentTask)
								{
									CurrentTask.Progress = 20.0 + (double)done * 30.0 / (double)total;
									CurrentTask.CurrentFileIndex = done;
									CurrentTask.TotalFiles = total;
									if (failedCount > 0 || done == total)
									{
										CurrentTask.StepText = string.Format(LanguageManager.Get("DlLibrariesProgressFailed"), done, total, failedCount);
									}
									else
									{
										CurrentTask.StepText = string.Format(LanguageManager.Get("DlLibrariesProgress"), done, total);
									}
									DownloadManager.ProgressChanged?.Invoke(CurrentTask);
								}
							}
						}
					}
					finally
					{
						semaphore.Release();
					}
				}));
			}
			await Task.WhenAll(tasks);
			if (failedFiles.IsEmpty)
			{
				return;
			}
			if (CurrentTask != null)
			{
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlRetrying"), failedFiles.Count);
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
			int retryFailed = 0;
			foreach (var item2 in failedFiles)
			{
				string url2 = item2.url;
				string path2 = item2.path;
				string localPath = Path.Combine(libDir, path2.Replace('/', Path.DirectorySeparatorChar));
				if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
				{
					continue;
				}
				bool success = false;
				foreach (string candidate in GetUrlCandidates(url2, path2))
				{
					try
					{
						await DownloadWithRetryAsync(client2, candidate, localPath);
						if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
						{
							success = true;
							break;
						}
					}
					catch
					{
					}
				}
				if (!success)
				{
					retryFailed++;
				}
			}
			if (retryFailed > 0)
			{
				throw new Exception(string.Format(LanguageManager.Get("ErrLibrariesFailed"), retryFailed));
			}
		}
		finally
		{
			if (semaphore != null)
			{
				((IDisposable)semaphore).Dispose();
			}
		}
	}

	private static async Task DownloadLibraries(HttpClient client, string versionJson)
	{
		HttpClient client2 = client;
		JsonDocument doc = JsonDocument.Parse(versionJson);
		string libDir = Path.Combine(MinecraftPath, "libraries");
		Directory.CreateDirectory(libDir);
		List<(string url, string path)> libList = new List<(string, string)>();
		if (doc.RootElement.TryGetProperty("libraries", out var libs))
		{
			foreach (JsonElement lib in libs.EnumerateArray())
			{
				if (lib.TryGetProperty("downloads", out var dl) && dl.TryGetProperty("artifact", out var artifact) && artifact.TryGetProperty("url", out var urlProp) && artifact.TryGetProperty("path", out var pathProp))
				{
					string url4 = urlProp.GetString();
					string path2 = pathProp.GetString();
					if (!string.IsNullOrEmpty(url4) && !string.IsNullOrEmpty(path2))
					{
						url4 = url4.Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven").Replace("https://maven.fabricmc.net", "https://bmclapi2.bangbang93.com/maven").Replace("https://files.minecraftforge.net", "https://bmclapi2.bangbang93.com/maven");
						libList.Add((url4, path2));
					}
				}
				else
				{
					if (lib.TryGetProperty("name", out var nameProp) && lib.TryGetProperty("url", out var mavenUrlProp))
					{
						string name = nameProp.GetString();
						string mavenUrl2 = mavenUrlProp.GetString();
						if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(mavenUrl2))
						{
							string[] parts = name.Split(':');
							if (parts.Length >= 3)
							{
								string groupPath = parts[0].Replace('.', '/');
								string artifactName = parts[1];
								string version = parts[2];
								string path3 = $"{groupPath}/{artifactName}/{version}/{artifactName}-{version}.jar";
								mavenUrl2 = mavenUrl2.Replace("https://maven.fabricmc.net", "https://bmclapi2.bangbang93.com/maven").Replace("https://maven.quiltmc.org", "https://bmclapi2.bangbang93.com/maven");
								libList.Add((mavenUrl2 + "/" + path3, path3));
							}
						}
					}
					nameProp = default(JsonElement);
					mavenUrlProp = default(JsonElement);
				}
				dl = default(JsonElement);
				artifact = default(JsonElement);
				urlProp = default(JsonElement);
				pathProp = default(JsonElement);
			}
		}
		int total = libList.Count;
		int completedCount = 0;
		int failedCount = 0;
		List<(string url, string path)> failedFiles = new List<(string, string)>();
		object failedLock = new object();
		SemaphoreSlim semaphore = new SemaphoreSlim(GetMaxThreads());
		try
		{
			List<Task> tasks = new List<Task>();
			foreach (var item in libList)
			{
				var (url2, path) = item;
				await semaphore.WaitAsync();
				tasks.Add(Task.Run(async delegate
				{
					try
					{
						string localPath2 = Path.Combine(libDir, path.Replace('/', Path.DirectorySeparatorChar));
						Directory.CreateDirectory(Path.GetDirectoryName(localPath2));
						if (!File.Exists(localPath2))
						{
							try
							{
								byte[] data2 = await TryGetByteArray(client2, url2);
								await File.WriteAllBytesAsync(localPath2, data2);
								Interlocked.Add(ref _currentBytes, data2.Length);
							}
							catch
							{
								Interlocked.Increment(ref failedCount);
								lock (failedLock)
								{
									failedFiles.Add((url2, path));
								}
							}
						}
						int done = Interlocked.Increment(ref completedCount);
						if (CurrentTask != null && total > 0)
						{
							lock (CurrentTask)
							{
								CurrentTask.Progress = 20.0 + (double)done * 30.0 / (double)total;
								CurrentTask.CurrentFileIndex = done;
								CurrentTask.TotalFiles = total;
								CurrentTask.StepText = ((failedCount > 0) ? string.Format(LanguageManager.Get("DlLibrariesProgressFailed"), done, total, failedCount) : string.Format(LanguageManager.Get("DlLibrariesProgress"), done, total));
								DownloadManager.ProgressChanged?.Invoke(CurrentTask);
							}
						}
					}
					finally
					{
						semaphore.Release();
					}
				}));
			}
			await Task.WhenAll(tasks);
			if (failedFiles.Count <= 0)
			{
				return;
			}
			if (CurrentTask != null)
			{
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlRetrying"), failedFiles.Count);
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
			int retrySuccess = 0;
			int retryFailed = 0;
			foreach (var item2 in failedFiles)
			{
				string url = item2.url;
				string localPath = Path.Combine(path2: item2.path.Replace('/', Path.DirectorySeparatorChar), path1: libDir);
				if (File.Exists(localPath))
				{
					retrySuccess++;
					continue;
				}
				string officialUrl = MirrorToOfficial(url);
				try
				{
					byte[] data = await TryGetByteArray(client2, officialUrl);
					await File.WriteAllBytesAsync(localPath, data);
					Interlocked.Add(ref _currentBytes, data.Length);
					retrySuccess++;
				}
				catch
				{
					retryFailed++;
				}
			}
			if (retryFailed > 0)
			{
				throw new Exception(string.Format(LanguageManager.Get("ErrLibrariesFailed"), retryFailed));
			}
		}
		finally
		{
			if (semaphore != null)
			{
				((IDisposable)semaphore).Dispose();
			}
		}
	}

	private static async Task DownloadAssets(HttpClient client, string versionJson)
	{
		HttpClient client2 = client;
		JsonDocument doc = JsonDocument.Parse(versionJson);
		string assetIndexUrl = null;
		string assetIndexId = "1.20";
		string assetIndexSha1 = null;
		if (doc.RootElement.TryGetProperty("assetIndex", out var ai))
		{
			if (ai.TryGetProperty("url", out var urlProp))
			{
				assetIndexUrl = urlProp.GetString();
			}
			if (ai.TryGetProperty("id", out var idProp))
			{
				assetIndexId = idProp.GetString();
			}
			if (ai.TryGetProperty("sha1", out var sha1Prop))
			{
				assetIndexSha1 = sha1Prop.GetString();
			}
		}
		if (string.IsNullOrEmpty(assetIndexUrl))
		{
			return;
		}
		string assetsDir = Path.Combine(MinecraftPath, "assets");
		string indexDir = Path.Combine(assetsDir, "indexes");
		string objectsDir = Path.Combine(assetsDir, "objects");
		Directory.CreateDirectory(indexDir);
		Directory.CreateDirectory(objectsDir);
		string indexPath = Path.Combine(indexDir, assetIndexId + ".json");
		if (!File.Exists(indexPath) || string.IsNullOrEmpty(assetIndexSha1) || !ValidateFileSha1(indexPath, assetIndexSha1))
		{
			string indexJson = await TryGetString(client2, assetIndexUrl);
			await File.WriteAllTextAsync(indexPath, indexJson);
			Interlocked.Add(ref _currentBytes, indexJson.Length);
		}
		JsonDocument indexDoc = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
		List<(string hash, long size)> assetList = new List<(string, long)>();
		if (indexDoc.RootElement.TryGetProperty("objects", out var objects))
		{
			foreach (JsonProperty prop in objects.EnumerateObject())
			{
				if (prop.Value.TryGetProperty("hash", out var hashProp))
				{
					string hash3 = hashProp.GetString();
					long size2 = 0L;
					if (prop.Value.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var sz))
					{
						size2 = sz;
					}
					assetList.Add((hash3, size2));
					sizeProp = default(JsonElement);
				}
				hashProp = default(JsonElement);
			}
		}
		int total = assetList.Count;
		int completedCount = 0;
		int skippedCount = 0;
		int failedCount = 0;
		ConcurrentBag<(string hash, long size)> failedAssets = new ConcurrentBag<(string, long)>();
		int maxThreads = Math.Max(8, Math.Min(GetMaxThreads(), 48));
		SemaphoreSlim semaphore = new SemaphoreSlim(maxThreads);
		try
		{
			List<Task> tasks = new List<Task>();
			if (CurrentTask != null)
			{
				CurrentTask.TotalFiles = total;
				CurrentTask.CurrentFileIndex = 0;
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlAssetsProgress"), 0, total);
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
			foreach (var item in assetList)
			{
				var (hash, size) = item;
				await semaphore.WaitAsync();
				tasks.Add(Task.Run(async delegate
				{
					try
					{
						string prefix2 = hash.Substring(0, 2);
						string localPath2 = Path.Combine(objectsDir, prefix2, hash);
						bool needDownload = true;
						if (File.Exists(localPath2) && size > 0 && new FileInfo(localPath2).Length == size)
						{
							needDownload = false;
							Interlocked.Increment(ref skippedCount);
						}
						if (needDownload)
						{
							Directory.CreateDirectory(Path.GetDirectoryName(localPath2));
							string mirrorUrl2 = $"{"https://bmclapi2.bangbang93.com"}/assets/{prefix2}/{hash}";
							string officialUrl2 = "https://resources.download.minecraft.net/" + prefix2 + "/" + hash;
							bool downloaded2 = false;
							int toSec = ((size > 1048576) ? 30 : 10);
							foreach (string candidate2 in GetAssetUrlCandidates(mirrorUrl2, officialUrl2))
							{
								try
								{
									await DownloadWithRetryAsync(client2, candidate2, localPath2, toSec);
									if (File.Exists(localPath2) && new FileInfo(localPath2).Length > 0)
									{
										downloaded2 = true;
										break;
									}
								}
								catch
								{
								}
							}
							if (!downloaded2)
							{
								Interlocked.Increment(ref failedCount);
								failedAssets.Add((hash, size));
							}
						}
						int done = Interlocked.Increment(ref completedCount);
						if (CurrentTask != null && total > 0)
						{
							DateTime now = DateTime.Now;
							if ((now - _lastSpeedUpdate).TotalMilliseconds >= 150.0)
							{
								_lastSpeedUpdate = now;
								lock (CurrentTask)
								{
									CurrentTask.Progress = 50.0 + (double)done * 35.0 / (double)total;
									CurrentTask.CurrentFileIndex = done;
									CurrentTask.TotalFiles = total;
									if (failedCount > 0 || done == total)
									{
										CurrentTask.StepText = string.Format(LanguageManager.Get("DlAssetsProgressFailed"), done, total, failedCount);
									}
									else
									{
										CurrentTask.StepText = string.Format(LanguageManager.Get("DlAssetsProgress"), done, total);
									}
									DownloadManager.ProgressChanged?.Invoke(CurrentTask);
								}
							}
						}
					}
					finally
					{
						semaphore.Release();
					}
				}));
			}
			await Task.WhenAll(tasks);
			if (failedAssets.IsEmpty)
			{
				return;
			}
			if (CurrentTask != null)
			{
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlRetrying"), failedAssets.Count);
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
			int retryFailed = 0;
			foreach (var item2 in failedAssets)
			{
				var (hash2, _) = item2;
				_ = item2.size;
				string prefix = hash2.Substring(0, 2);
				string localPath = Path.Combine(objectsDir, prefix, hash2);
				if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
				{
					continue;
				}
				string mirrorUrl = $"{"https://bmclapi2.bangbang93.com"}/assets/{prefix}/{hash2}";
				string officialUrl = "https://resources.download.minecraft.net/" + prefix + "/" + hash2;
				bool downloaded = false;
				foreach (string candidate in GetAssetUrlCandidates(mirrorUrl, officialUrl))
				{
					try
					{
						await DownloadWithRetryAsync(client2, candidate, localPath);
						if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
						{
							downloaded = true;
							break;
						}
					}
					catch
					{
					}
				}
				if (!downloaded)
				{
					retryFailed++;
				}
			}
			if (retryFailed > 0)
			{
				throw new Exception(string.Format(LanguageManager.Get("ErrAssetsFailed"), retryFailed));
			}
		}
		finally
		{
			if (semaphore != null)
			{
				((IDisposable)semaphore).Dispose();
			}
		}
	}

	private static async Task DownloadModLoader(HttpClient client, string versionId, string loaderName, string loaderVersion)
	{
		if (string.IsNullOrEmpty(loaderVersion))
		{
			List<string> loaderVersions = await GetLoaderVersionsAsync(loaderName, versionId);
			if (loaderVersions.Count == 0)
			{
				throw new Exception(string.Format(LanguageManager.Get("ErrLoaderNotFound"), loaderName, versionId));
			}
			loaderVersion = loaderVersions[0];
		}
		int num;
		switch (loaderName)
		{
		case "Fabric":
			await InstallFabricLoader(client, versionId, loaderVersion);
			break;
		case "Quilt":
			await InstallQuiltLoader(client, versionId, loaderVersion);
			break;
		default:
			num = ((loaderName == "NeoForge") ? 1 : 0);
			goto IL_025d;
		case "Forge":
			{
				num = 1;
				goto IL_025d;
			}
			IL_025d:
			if (num != 0)
			{
				await InstallForgeLoader(client, versionId, loaderVersion, loaderName);
			}
			break;
		}
		if (CurrentTask != null)
		{
			CurrentTask.StepText = string.Format(LanguageManager.Get("DlLoaderInstalled"), loaderName);
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
	}

	private static async Task InstallFabricLoader(HttpClient client, string gameVersion, string loaderVersion)
	{
		string profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
		string profileJson = await TryGetString(client, profileUrl);
		using JsonDocument doc = JsonDocument.Parse(profileJson);
		string fabricId = doc.RootElement.GetProperty("id").GetString() ?? ("fabric-loader-" + loaderVersion + "-" + gameVersion);
		string fabricDir = Path.Combine(MinecraftPath, "versions", fabricId);
		Directory.CreateDirectory(fabricDir);
		await File.WriteAllTextAsync(Path.Combine(fabricDir, fabricId + ".json"), profileJson);
		if (CurrentTask != null)
		{
			CurrentTask.StepText = string.Format(LanguageManager.Get("DlModLoader"), "Fabric");
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
		await DownloadLibrariesWithRetry(client, profileJson);
		await DownloadAssets(client, profileJson);
	}

	private static async Task InstallQuiltLoader(HttpClient client, string gameVersion, string loaderVersion)
	{
		string profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
		string profileJson = await TryGetString(client, profileUrl);
		using JsonDocument doc = JsonDocument.Parse(profileJson);
		string quiltId = doc.RootElement.GetProperty("id").GetString() ?? ("quilt-loader-" + loaderVersion + "-" + gameVersion);
		string quiltDir = Path.Combine(MinecraftPath, "versions", quiltId);
		Directory.CreateDirectory(quiltDir);
		await File.WriteAllTextAsync(Path.Combine(quiltDir, quiltId + ".json"), profileJson);
		if (CurrentTask != null)
		{
			CurrentTask.StepText = string.Format(LanguageManager.Get("DlModLoader"), "Quilt");
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
		await DownloadLibrariesWithRetry(client, profileJson);
		await DownloadAssets(client, profileJson);
	}

	private static async Task InstallForgeLoader(HttpClient client, string gameVersion, string loaderVersion, string loaderName)
	{
		List<string> candidateUrls;
		if (loaderName == "NeoForge")
		{
			candidateUrls = new List<string>
			{
				$"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{loaderVersion}/neoforge-{loaderVersion}-installer.jar",
				$"https://maven.neoforged.net/releases/net/neoforged/neoforge/{loaderVersion}/neoforge-{loaderVersion}-installer.jar"
			};
		}
		else
		{
			string bmclUrl = $"{"https://bmclapi2.bangbang93.com"}/maven/net/minecraftforge/forge/{gameVersion}-{loaderVersion}/forge-{gameVersion}-{loaderVersion}-installer.jar";
			string officialUrl = $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{gameVersion}-{loaderVersion}/forge-{gameVersion}-{loaderVersion}-installer.jar";
			candidateUrls = new List<string> { bmclUrl, officialUrl };
		}
		string tempDir = Path.Combine(Path.GetTempPath(), "DIL");
		Directory.CreateDirectory(tempDir);
		string installerPath = Path.Combine(tempDir, $"{loaderName}-{gameVersion}-{loaderVersion}-installer.jar");
		if (!File.Exists(installerPath) || new FileInfo(installerPath).Length == 0)
		{
			if (CurrentTask != null)
			{
				CurrentTask.StepText = string.Format(LanguageManager.Get("DlModLoader"), loaderName);
				CurrentTask.TotalBytes = 0L;
				CurrentTask.DownloadedBytes = 0L;
				DownloadManager.ProgressChanged?.Invoke(CurrentTask);
			}
			string dlLabel = string.Format(LanguageManager.Get("DlModLoader"), loaderName);
			Exception lastEx = null;
			bool downloaded = false;
			foreach (string url in candidateUrls)
			{
				try
				{
					await DownloadFileWithProgressAsync(client, url, installerPath, 120, dlLabel);
					if (new FileInfo(installerPath).Length > 0)
					{
						downloaded = true;
						break;
					}
				}
				catch (Exception ex)
				{
					lastEx = ex;
					try
					{
						if (File.Exists(installerPath))
						{
							File.Delete(installerPath);
						}
					}
					catch
					{
					}
				}
			}
			if (!downloaded)
			{
				throw new Exception(string.Format(LanguageManager.Get("ErrLoaderDownloadFail"), loaderName, candidateUrls.Count, lastEx?.Message));
			}
		}
		string javaPath = FindJavaPath();
		if (string.IsNullOrEmpty(javaPath))
		{
			throw new Exception(LanguageManager.Get("ErrNoJava"));
		}
		await PreDownloadForgeLibraries(client, installerPath);
		EnsureLauncherProfiles();
		if (CurrentTask != null)
		{
			CurrentTask.StepText = string.Format(LanguageManager.Get("DlForgeInstalling"), loaderName);
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
		ProcessStartInfo psi = new ProcessStartInfo
		{
			FileName = javaPath,
			Arguments = $"-Dmirror=https://bmclapi2.bangbang93.com/maven/ -Dforge.don't.prompt=true -jar \"{installerPath}\" --installClient \"{MinecraftPath}\"",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		Process proc = Process.Start(psi);
		try
		{
			if (proc == null)
			{
				return;
			}
			StringBuilder stdoutBuilder = new StringBuilder();
			StringBuilder stderrBuilder = new StringBuilder();
			Task stdoutTask = Task.Run(async delegate
			{
				while (true)
				{
					string text2;
					string line2 = (text2 = await proc.StandardOutput.ReadLineAsync());
					if (text2 == null)
					{
						break;
					}
					stdoutBuilder.AppendLine(line2);
				}
			});
			Task stderrTask = Task.Run(async delegate
			{
				while (true)
				{
					string text;
					string line = (text = await proc.StandardError.ReadLineAsync());
					if (text == null)
					{
						break;
					}
					stderrBuilder.AppendLine(line);
				}
			});
			await proc.WaitForExitAsync();
			await Task.WhenAll(stdoutTask, stderrTask);
			string stdout = stdoutBuilder.ToString();
			string stderr = stderrBuilder.ToString();
			if (proc.ExitCode != 0)
			{
				string detail = (string.IsNullOrEmpty(stderr) ? stdout : stderr);
				if (string.IsNullOrEmpty(detail))
				{
					detail = LanguageManager.Get("ErrInstallerNoOutput");
				}
				throw new Exception(string.Format(LanguageManager.Get("ErrInstallerFail"), loaderName, proc.ExitCode, detail));
			}
			await PostDownloadForgeRuntimeLibraries(client, installerPath);
			string versionDir = Path.Combine(MinecraftPath, "versions", CurrentTask?.VersionId ?? "");
			string versionJsonFile = Path.Combine(versionDir, CurrentTask?.VersionId + ".json");
			if (File.Exists(versionJsonFile))
			{
				await DownloadAssets(client, await File.ReadAllTextAsync(versionJsonFile));
			}
		}
		finally
		{
			if (proc != null)
			{
				((IDisposable)proc).Dispose();
			}
		}
	}

	private static async Task PreDownloadForgeLibraries(HttpClient client, string installerPath)
	{
		HttpClient client2 = client;
		string librariesDir = Path.Combine(MinecraftPath, "libraries");
		Directory.CreateDirectory(librariesDir);
		string profileJson;
		try
		{
			using ZipArchive archive = ZipFile.OpenRead(installerPath);
			ZipArchiveEntry entry = archive.GetEntry("install_profile.json");
			if (entry == null)
			{
				return;
			}
			using Stream s = entry.Open();
			using StreamReader r = new StreamReader(s, Encoding.UTF8);
			profileJson = await r.ReadToEndAsync();
		}
		catch
		{
			return;
		}
		List<(string url, string path)> libList = new List<(string, string)>();
		try
		{
			using JsonDocument doc = JsonDocument.Parse(profileJson);
			if (doc.RootElement.TryGetProperty("libraries", out var libs))
			{
				foreach (JsonElement lib in libs.EnumerateArray())
				{
					string name = null;
					if (lib.TryGetProperty("name", out var nameProp))
					{
						name = nameProp.GetString();
					}
					if (lib.TryGetProperty("downloads", out var dl) && dl.TryGetProperty("artifact", out var artifact))
					{
						string url2 = null;
						string path2 = null;
						if (artifact.TryGetProperty("url", out var uProp))
						{
							url2 = uProp.GetString();
						}
						if (artifact.TryGetProperty("path", out var pProp))
						{
							path2 = pProp.GetString();
						}
						if (!string.IsNullOrEmpty(url2) && !string.IsNullOrEmpty(path2) && url2 != "")
						{
							libList.Add((MirrorUrlToBmcl(url2), path2));
						}
						else if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path2))
						{
							libList.Add(("https://bmclapi2.bangbang93.com/maven/" + path2, path2));
						}
						uProp = default(JsonElement);
						pProp = default(JsonElement);
					}
					else if (!string.IsNullOrEmpty(name))
					{
						string[] parts = name.Split(':');
						if (parts.Length >= 3)
						{
							string groupPath = parts[0].Replace('.', '/');
							string artifactId = parts[1];
							string version = parts[2];
							string fileName = artifactId + "-" + version + ".jar";
							string relPath = $"{groupPath}/{artifactId}/{version}/{fileName}";
							string repoUrl = null;
							if (lib.TryGetProperty("url", out var urlProp))
							{
								repoUrl = urlProp.GetString();
							}
							if (!string.IsNullOrEmpty(repoUrl))
							{
								libList.Add((MirrorUrlToBmcl(repoUrl.TrimEnd('/') + "/" + relPath), relPath));
							}
							else
							{
								libList.Add(("https://bmclapi2.bangbang93.com/maven/" + relPath, relPath));
							}
							urlProp = default(JsonElement);
						}
					}
					nameProp = default(JsonElement);
					dl = default(JsonElement);
					artifact = default(JsonElement);
				}
			}
		}
		catch
		{
			return;
		}
		if (libList.Count == 0)
		{
			return;
		}
		int total = libList.Count;
		int downloaded = 0;
		int skipped = 0;
		int failed = 0;
		int completedCount = 0;
		List<(string url, string path)> failedFiles = new List<(string, string)>();
		object failedLock = new object();
		int maxThreads = Math.Max(4, Math.Min(GetMaxThreads(), 32));
		SemaphoreSlim sem = new SemaphoreSlim(maxThreads);
		if (CurrentTask != null)
		{
			CurrentTask.StepText = string.Format(LanguageManager.Get("DlForgePreDownload"), 0, total);
			CurrentTask.TotalFiles = total;
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
		IEnumerable<Task> tasks = libList.Select<(string, string), Task>(async delegate((string url, string path) item)
		{
			string localPath2 = Path.Combine(librariesDir, item.path.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(localPath2) && new FileInfo(localPath2).Length > 0)
			{
				Interlocked.Increment(ref skipped);
				Interlocked.Increment(ref completedCount);
				return;
			}
			await sem.WaitAsync();
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(localPath2));
				bool success2 = false;
				foreach (string candidate2 in GetUrlCandidates(item.url, item.path))
				{
					try
					{
						await DownloadWithRetryAsync(client2, candidate2, localPath2, 15);
						if (new FileInfo(localPath2).Length > 0)
						{
							Interlocked.Increment(ref downloaded);
							success2 = true;
							break;
						}
					}
					catch
					{
					}
				}
				if (!success2)
				{
					Interlocked.Increment(ref failed);
					lock (failedLock)
					{
						failedFiles.Add((item.url, item.path));
					}
				}
			}
			finally
			{
				sem.Release();
				int done = Interlocked.Increment(ref completedCount);
				if (CurrentTask != null && total > 0)
				{
					lock (CurrentTask)
					{
						CurrentTask.CurrentFileIndex = done;
						CurrentTask.TotalFiles = total;
						CurrentTask.StepText = string.Format(LanguageManager.Get("DlForgePreDownload"), done, total);
						DownloadManager.ProgressChanged?.Invoke(CurrentTask);
					}
				}
			}
		});
		await Task.WhenAll(tasks);
		if (failedFiles.Count <= 0 || CurrentTask == null)
		{
			return;
		}
		CurrentTask.StepText = string.Format(LanguageManager.Get("DlRetrying"), failedFiles.Count);
		DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		int retryFailed = 0;
		foreach (var item in failedFiles)
		{
			string url = item.url;
			string path = item.path;
			string localPath = Path.Combine(librariesDir, path.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
			{
				continue;
			}
			bool success = false;
			foreach (string candidate in GetUrlCandidates(url, path))
			{
				try
				{
					await DownloadWithRetryAsync(client2, candidate, localPath);
					if (new FileInfo(localPath).Length > 0)
					{
						success = true;
						break;
					}
				}
				catch
				{
				}
			}
			if (!success)
			{
				retryFailed++;
			}
		}
		if (retryFailed <= 0)
		{
			return;
		}
		throw new Exception(string.Format(LanguageManager.Get("ErrLibrariesFailed"), retryFailed));
	}

	private static async Task PostDownloadForgeRuntimeLibraries(HttpClient client, string installerPath)
	{
		HttpClient client2 = client;
		string librariesDir = Path.Combine(MinecraftPath, "libraries");
		Directory.CreateDirectory(librariesDir);
		JsonElement? runtimeLibsElement = null;
		try
		{
			using ZipArchive archive = ZipFile.OpenRead(installerPath);
			ZipArchiveEntry versionEntry = archive.GetEntry("version.json");
			if (versionEntry != null)
			{
				using Stream stream = versionEntry.Open();
				using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
				using JsonDocument jsonDocument = JsonDocument.Parse(await streamReader.ReadToEndAsync());
				if (jsonDocument.RootElement.TryGetProperty("libraries", out var libs2) && libs2.ValueKind == JsonValueKind.Array)
				{
					runtimeLibsElement = libs2;
				}
			}
			if (!runtimeLibsElement.HasValue)
			{
				ZipArchiveEntry profileEntry = archive.GetEntry("install_profile.json");
				if (profileEntry != null)
				{
					using Stream s = profileEntry.Open();
					using StreamReader r = new StreamReader(s, Encoding.UTF8);
					using JsonDocument doc = JsonDocument.Parse(await r.ReadToEndAsync());
					if (doc.RootElement.TryGetProperty("versionInfo", out var vi) && vi.TryGetProperty("libraries", out var libs) && libs.ValueKind == JsonValueKind.Array)
					{
						runtimeLibsElement = libs;
					}
				}
			}
		}
		catch
		{
			return;
		}
		if (!runtimeLibsElement.HasValue || runtimeLibsElement.Value.GetArrayLength() == 0)
		{
			return;
		}
		List<(string url, string path)> libList = new List<(string, string)>();
		JsonElement value = runtimeLibsElement.Value;
		foreach (JsonElement lib in value.EnumerateArray())
		{
			if (lib.TryGetProperty("downloads", out var dl) && dl.TryGetProperty("artifact", out var artifact))
			{
				string url2 = null;
				string path2 = null;
				if (artifact.TryGetProperty("url", out var uProp))
				{
					url2 = uProp.GetString();
				}
				if (artifact.TryGetProperty("path", out var pProp))
				{
					path2 = pProp.GetString();
				}
				if (!string.IsNullOrEmpty(url2) && !string.IsNullOrEmpty(path2) && url2 != "")
				{
					libList.Add((MirrorUrlToBmcl(url2), path2));
				}
				else if (lib.TryGetProperty("name", out value) && !string.IsNullOrEmpty(path2))
				{
					libList.Add(("https://bmclapi2.bangbang93.com/maven/" + path2, path2));
				}
				dl = default(JsonElement);
				artifact = default(JsonElement);
				uProp = default(JsonElement);
				pProp = default(JsonElement);
			}
		}
		if (libList.Count == 0)
		{
			return;
		}
		int total = libList.Count;
		int completedCount = 0;
		int skippedCount = 0;
		int failedCount = 0;
		ConcurrentBag<(string url, string path)> failedFiles = new ConcurrentBag<(string, string)>();
		int maxThreads = Math.Max(4, Math.Min(GetMaxThreads(), 32));
		SemaphoreSlim sem = new SemaphoreSlim(maxThreads);
		if (CurrentTask != null)
		{
			CurrentTask.StepText = string.Format(LanguageManager.Get("DlForgeRuntimeLibs"), 0, total);
			CurrentTask.TotalFiles = total;
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
		IEnumerable<Task> tasks = libList.Select<(string, string), Task>(async delegate((string url, string path) item)
		{
			string localPath2 = Path.Combine(librariesDir, item.path.Replace('/', Path.DirectorySeparatorChar));
			bool needDownload = true;
			if (File.Exists(localPath2) && new FileInfo(localPath2).Length > 0)
			{
				needDownload = false;
				Interlocked.Increment(ref skippedCount);
			}
			if (needDownload)
			{
				await sem.WaitAsync();
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(localPath2));
					bool success2 = false;
					foreach (string candidate2 in GetUrlCandidates(item.url, item.path))
					{
						try
						{
							await DownloadWithRetryAsync(client2, candidate2, localPath2, 20);
							if (new FileInfo(localPath2).Length > 0)
							{
								success2 = true;
								break;
							}
						}
						catch
						{
						}
					}
					if (!success2)
					{
						Interlocked.Increment(ref failedCount);
						failedFiles.Add(item);
					}
				}
				finally
				{
					sem.Release();
				}
			}
			int done = Interlocked.Increment(ref completedCount);
			if (CurrentTask != null && total > 0)
			{
				DateTime now = DateTime.Now;
				if ((now - _lastSpeedUpdate).TotalMilliseconds >= 200.0)
				{
					_lastSpeedUpdate = now;
					lock (CurrentTask)
					{
						CurrentTask.CurrentFileIndex = done;
						CurrentTask.TotalFiles = total;
						if (failedCount > 0 || done == total)
						{
							CurrentTask.StepText = string.Format(LanguageManager.Get("DlForgeRuntimeLibsFailed"), done, total, failedCount);
						}
						else
						{
							CurrentTask.StepText = string.Format(LanguageManager.Get("DlForgeRuntimeLibs"), done, total);
						}
						DownloadManager.ProgressChanged?.Invoke(CurrentTask);
					}
				}
			}
		});
		await Task.WhenAll(tasks);
		if (failedFiles.IsEmpty)
		{
			return;
		}
		if (CurrentTask != null)
		{
			CurrentTask.StepText = string.Format(LanguageManager.Get("DlRetrying"), failedFiles.Count);
			DownloadManager.ProgressChanged?.Invoke(CurrentTask);
		}
		int retryFailed = 0;
		foreach (var item in failedFiles)
		{
			string url = item.url;
			string path = item.path;
			string localPath = Path.Combine(librariesDir, path.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
			{
				continue;
			}
			bool success = false;
			foreach (string candidate in GetUrlCandidates(url, path))
			{
				try
				{
					await DownloadWithRetryAsync(client2, candidate, localPath);
					if (new FileInfo(localPath).Length > 0)
					{
						success = true;
						break;
					}
				}
				catch
				{
				}
			}
			if (!success)
			{
				retryFailed++;
			}
		}
		if (retryFailed <= 0)
		{
			return;
		}
		throw new Exception(string.Format(LanguageManager.Get("ErrLibrariesFailed"), retryFailed));
	}

	private static string MirrorUrlToBmcl(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return url;
		}
		return url.Replace("https://libraries.minecraft.net/", "https://bmclapi2.bangbang93.com/maven/").Replace("https://maven.minecraftforge.net/maven/", "https://bmclapi2.bangbang93.com/maven/").Replace("https://files.minecraftforge.net/maven/", "https://bmclapi2.bangbang93.com/maven/")
			.Replace("https://maven.creeperhost.net/", "https://bmclapi2.bangbang93.com/maven/");
	}

	private static IEnumerable<string> GetUrlCandidates(string primaryUrl, string path)
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int mode = GetSourceMode();
		string bmcl = "https://bmclapi2.bangbang93.com/maven/" + path;
		string official = "https://libraries.minecraft.net/" + path;
		string forgeMaven = "https://maven.minecraftforge.net/maven/" + path;
		List<string> candidates = new List<string> { bmcl, primaryUrl, official, forgeMaven };
		foreach (string item in OrderByPreference(candidates))
		{
			yield return item;
		}
		IEnumerable<string> OrderByPreference(IEnumerable<string> urls)
		{
			List<string> list = urls.Where((string u) => !string.IsNullOrEmpty(u) && seen.Add(u)).ToList();
			if (mode == 2)
			{
				return list.AsEnumerable().Reverse();
			}
			return list;
		}
	}

	public static async Task EnsureBaseJarAsync(string versionId)
	{
		string versionDir = Path.Combine(MinecraftPath, "versions", versionId);
		string jsonPath = Path.Combine(versionDir, versionId + ".json");
		if (!File.Exists(jsonPath))
		{
			return;
		}
		string jarVersion = null;
		try
		{
			using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
			JsonElement root = doc.RootElement;
			JsonElement inhEl;
			if (root.TryGetProperty("jar", out var jarEl))
			{
				jarVersion = jarEl.GetString();
			}
			else if (root.TryGetProperty("inheritsFrom", out inhEl))
			{
				jarVersion = inhEl.GetString();
			}
		}
		catch
		{
			return;
		}
		if (string.IsNullOrEmpty(jarVersion) || jarVersion == versionId)
		{
			return;
		}
		string jarDir = Path.Combine(MinecraftPath, "versions", jarVersion);
		string jarPath = Path.Combine(jarDir, jarVersion + ".jar");
		string jarJsonPath = Path.Combine(jarDir, jarVersion + ".json");
		if (File.Exists(jarPath))
		{
			return;
		}
		using HttpClient client = CreateClient();
		string baseJson;
		if (!File.Exists(jarJsonPath))
		{
			baseJson = await DownloadVersionJson(client, jarVersion);
			Directory.CreateDirectory(jarDir);
			await File.WriteAllTextAsync(jarJsonPath, baseJson);
		}
		else
		{
			baseJson = await File.ReadAllTextAsync(jarJsonPath);
		}
		await DownloadClientJar(client, jarVersion, baseJson, jarDir);
	}

	private static void EnsureLauncherProfiles()
	{
		string path = Path.Combine(MinecraftPath, "launcher_profiles.json");
		if (File.Exists(path))
		{
			return;
		}
		string contents = "{\r\n  \"profiles\": {\r\n    \"NexusX\": {\r\n      \"name\": \"NexusX\",\r\n      \"type\": \"custom\",\r\n      \"created\": \"1970-01-01T00:00:00.000Z\",\r\n      \"lastVersionId\": \"1.21\",\r\n      \"icon\": \"Grass\"\r\n    }\r\n  },\r\n  \"selectedProfile\": \"NexusX\",\r\n  \"clientToken\": \"00000000000000000000000000000000\",\r\n  \"authenticationDatabase\": {}\r\n}";
		try
		{
			Directory.CreateDirectory(MinecraftPath);
			File.WriteAllText(path, contents);
		}
		catch
		{
		}
	}

	private static string FindJavaPath()
	{
		List<string> list = new List<string>();
		string environmentVariable = Environment.GetEnvironmentVariable("JAVA_HOME");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			list.Add(Path.Combine(environmentVariable, "bin", "java.exe"));
			list.Add(Path.Combine(environmentVariable, "bin", "java"));
		}
		string text = Environment.GetEnvironmentVariable("PATH") ?? "";
		string[] array = text.Split(Path.PathSeparator);
		foreach (string text2 in array)
		{
			if (!string.IsNullOrWhiteSpace(text2))
			{
				list.Add(Path.Combine(text2, "java.exe"));
				list.Add(Path.Combine(text2, "java"));
			}
		}
		list.Add(Path.Combine(MinecraftPath, "runtime", "jre", "bin", "java.exe"));
		foreach (string item in list)
		{
			try
			{
				if (File.Exists(item))
				{
					return item;
				}
			}
			catch
			{
			}
		}
		return "";
	}

	public static async Task<List<string>> GetLoaderVersionsAsync(string loaderName, string gameVersion)
	{
		using HttpClient client = CreateClient();
		try
		{
			if (1 == 0)
			{
			}
			List<string> result = loaderName switch
			{
				"Forge" => await GetForgeVersionsAsync(client, gameVersion), 
				"Fabric" => await GetFabricVersionsAsync(client, gameVersion), 
				"NeoForge" => await GetNeoForgeVersionsAsync(client, gameVersion), 
				"Quilt" => await GetQuiltVersionsAsync(client, gameVersion), 
				_ => new List<string>(), 
			};
			if (1 == 0)
			{
			}
			return result;
		}
		catch
		{
			return new List<string>();
		}
	}

	private static async Task<List<string>> GetForgeVersionsAsync(HttpClient client, string gameVersion)
	{
		string[] urls = new string[3]
		{
			"https://bmclapi2.bangbang93.com/forge/minecraft/" + gameVersion,
			"https://files.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml",
			"https://files.minecraftforge.net/net/minecraftforge/forge/promotions_sponge.json"
		};
		string[] array = urls;
		foreach (string url in array)
		{
			try
			{
				if (url.Contains("bmclapi") || url.Contains("bangbang93"))
				{
					JsonDocument doc = JsonDocument.Parse(await TryGetString(client, url));
					List<string> result = new List<string>();
					foreach (JsonElement item in doc.RootElement.EnumerateArray())
					{
						string ver = null;
						if (item.TryGetProperty("version", out var v2))
						{
							ver = v2.GetString();
						}
						else
						{
							if (item.TryGetProperty("name", out var i))
							{
								ver = i.GetString();
							}
							i = default(JsonElement);
						}
						if (!string.IsNullOrEmpty(ver) && !result.Contains(ver))
						{
							result.Add(ver);
						}
						v2 = default(JsonElement);
					}
					if (result.Count > 0)
					{
						return result.OrderByDescending((string v) => v, new VersionComparer()).ToList();
					}
					continue;
				}
				if (url.EndsWith("maven-metadata.xml"))
				{
					string xml = await TryGetString(client, url);
					List<string> result3 = new List<string>();
					string prefix2 = gameVersion + "-";
					string[] lines = xml.Split('\n');
					string[] array2 = lines;
					foreach (string line in array2)
					{
						string trimmed = line.Trim();
						if (!trimmed.StartsWith("<version>") || !trimmed.EndsWith("</version>"))
						{
							continue;
						}
						string ver2 = trimmed.Substring(9, trimmed.Length - 19);
						if (ver2.StartsWith(prefix2))
						{
							string forgeVer = ver2.Substring(prefix2.Length);
							if (!result3.Contains(forgeVer))
							{
								result3.Add(forgeVer);
							}
						}
					}
					if (result3.Count > 0)
					{
						return result3.OrderByDescending((string v) => v, new VersionComparer()).ToList();
					}
					continue;
				}
				JsonDocument doc2 = JsonDocument.Parse(await TryGetString(client, url));
				List<string> result2 = new List<string>();
				string prefix = gameVersion + "-";
				if (doc2.RootElement.TryGetProperty("promos", out var promos))
				{
					foreach (JsonProperty prop in promos.EnumerateObject())
					{
						if (prop.Name.StartsWith(prefix))
						{
							string ver3 = prop.Value.GetString();
							if (!string.IsNullOrEmpty(ver3) && !result2.Contains(ver3))
							{
								result2.Add(ver3);
							}
						}
					}
				}
				if (result2.Count > 0)
				{
					return result2.OrderByDescending((string v) => v, new VersionComparer()).ToList();
				}
				promos = default(JsonElement);
			}
			catch
			{
			}
		}
		return new List<string>();
	}

	private static async Task<List<string>> GetFabricVersionsAsync(HttpClient client, string gameVersion)
	{
		try
		{
			string url = "https://meta.fabricmc.net/v2/versions/loader/" + gameVersion;
			JsonDocument doc = JsonDocument.Parse(await client.GetStringAsync(url));
			List<string> result = new List<string>();
			foreach (JsonElement item in doc.RootElement.EnumerateArray())
			{
				if (item.TryGetProperty("loader", out var loader) && loader.TryGetProperty("version", out var verProp))
				{
					string ver = verProp.GetString();
					if (!string.IsNullOrEmpty(ver))
					{
						result.Add(ver);
					}
				}
				loader = default(JsonElement);
				verProp = default(JsonElement);
			}
			return result.OrderByDescending((string v) => v, new VersionComparer()).ToList();
		}
		catch
		{
			return new List<string>();
		}
	}

	private static async Task<List<string>> GetNeoForgeVersionsAsync(HttpClient client, string gameVersion)
	{
		string url = "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
		string xml = await client.GetStringAsync(url);
		List<string> result = new List<string>();
		string shortMc = gameVersion.Replace("1.", "");
		string[] lines = xml.Split('\n');
		string[] array = lines;
		foreach (string line in array)
		{
			string trimmed = line.Trim();
			if (trimmed.StartsWith("<version>") && trimmed.EndsWith("</version>"))
			{
				string ver = trimmed.Substring(9, trimmed.Length - 19);
				if (ver.StartsWith(shortMc + "."))
				{
					result.Add(ver);
				}
			}
		}
		result.Reverse();
		return result;
	}

	private static async Task<List<string>> GetQuiltVersionsAsync(HttpClient client, string gameVersion)
	{
		string url = "https://meta.quiltmc.org/v3/versions/loader/" + gameVersion;
		JsonDocument doc = JsonDocument.Parse(await client.GetStringAsync(url));
		List<string> result = new List<string>();
		foreach (JsonElement item in doc.RootElement.EnumerateArray())
		{
			if (item.TryGetProperty("loader", out var loader) && loader.TryGetProperty("version", out var verProp))
			{
				string ver = verProp.GetString();
				if (!string.IsNullOrEmpty(ver))
				{
					result.Add(ver);
				}
			}
			loader = default(JsonElement);
			verProp = default(JsonElement);
		}
		return result;
	}
}