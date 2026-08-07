using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;

namespace DynamicIsland;

public static class LaunchManager
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private delegate bool EnumWindowsCallback(nint hWnd, nint lParam);

	public static bool IsLaunching { get; private set; }

	public static string MinecraftPath => DownloadManager.MinecraftPath;

	public static event Action<LaunchProgress>? ProgressChanged;

	public static event Action<string>? LogReceived;

	public static event Action? LaunchCompleted;

	public static event Action<Exception>? LaunchFailed;

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EnumWindows(EnumWindowsCallback callback, nint lParam);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	public static string GetVersionGameDir(string versionId)
	{
		return Path.Combine(MinecraftPath, "instances", versionId);
	}

	public static void EnsureVersionIsolationDirs(string versionId)
	{
		string versionGameDir = GetVersionGameDir(versionId);
		string[] array = new string[9] { "mods", "saves", "config", "resourcepacks", "shaderpacks", "logs", "screenshots", "schematics", "crash-reports" };
		string[] array2 = array;
		foreach (string path in array2)
		{
			Directory.CreateDirectory(Path.Combine(versionGameDir, path));
		}
	}

	private static MinecraftPath CreateIsolatedLaunchPath(string versionId)
	{
		MinecraftPath minecraftPath = new MinecraftPath(MinecraftPath);
		string versionGameDir = GetVersionGameDir(versionId);
		return new MinecraftPath(versionGameDir)
		{
			Versions = minecraftPath.Versions,
			Library = minecraftPath.Library,
			Assets = minecraftPath.Assets,
			Resource = minecraftPath.Resource,
			Runtime = minecraftPath.Runtime
		};
	}

	public static List<InstalledVersion> GetInstalledVersions()
	{
		List<InstalledVersion> list = new List<InstalledVersion>();
		string text = Path.Combine(MinecraftPath, "versions");
		if (!Directory.Exists(text))
		{
			return list;
		}
		HashSet<string> hashSet = new HashSet<string>();
		string[] directories = Directory.GetDirectories(text);
		string[] array = directories;
		foreach (string text2 in array)
		{
			string fileName = Path.GetFileName(text2);
			string path = Path.Combine(text2, fileName + ".json");
			if (!File.Exists(path))
			{
				continue;
			}
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
				if (jsonDocument.RootElement.TryGetProperty("inheritsFrom", out var value))
				{
					string @string = value.GetString();
					if (!string.IsNullOrEmpty(@string))
					{
						hashSet.Add(@string);
					}
				}
			}
			catch
			{
			}
		}
		string[] array2 = directories;
		foreach (string text3 in array2)
		{
			string fileName2 = Path.GetFileName(text3);
			string path2 = Path.Combine(text3, fileName2 + ".json");
			if (!File.Exists(path2))
			{
				continue;
			}
			InstalledVersion installedVersion = new InstalledVersion
			{
				Id = fileName2,
				HasJar = File.Exists(Path.Combine(text3, fileName2 + ".jar")),
				LastModified = File.GetLastWriteTimeUtc(path2).Ticks
			};
			try
			{
				using JsonDocument jsonDocument2 = JsonDocument.Parse(File.ReadAllText(path2));
				JsonElement rootElement = jsonDocument2.RootElement;
				if (rootElement.TryGetProperty("type", out var value2))
				{
					installedVersion.Type = value2.GetString() ?? "release";
				}
				if (rootElement.TryGetProperty("mainClass", out var value3))
				{
					string text4 = value3.GetString() ?? "";
					if (text4.Contains("forge", StringComparison.OrdinalIgnoreCase))
					{
						installedVersion.IsForge = true;
					}
					if (text4.Contains("fabric", StringComparison.OrdinalIgnoreCase))
					{
						installedVersion.IsFabric = true;
					}
					if (text4.Contains("quilt", StringComparison.OrdinalIgnoreCase))
					{
						installedVersion.IsQuilt = true;
					}
				}
				if (rootElement.TryGetProperty("inheritsFrom", out var value4))
				{
					string string2 = value4.GetString();
					if (!string.IsNullOrEmpty(string2))
					{
						installedVersion.LoaderName = (installedVersion.IsForge ? "Forge" : (installedVersion.IsFabric ? "Fabric" : (installedVersion.IsQuilt ? "Quilt" : "Modded")));
						if (!installedVersion.HasJar)
						{
							string path3 = Path.Combine(text, string2, string2 + ".jar");
							installedVersion.HasJar = File.Exists(path3);
						}
					}
				}
				if (!installedVersion.HasJar && rootElement.TryGetProperty("jar", out var value5))
				{
					string string3 = value5.GetString();
					if (!string.IsNullOrEmpty(string3))
					{
						string path4 = Path.Combine(text, string3, string3 + ".jar");
						installedVersion.HasJar = File.Exists(path4);
					}
				}
			}
			catch
			{
				installedVersion.Type = "unknown";
			}
			if (installedVersion.IsForge || installedVersion.IsFabric || installedVersion.IsQuilt || !string.IsNullOrEmpty(installedVersion.LoaderName) || !hashSet.Contains(fileName2))
			{
				list.Add(installedVersion);
			}
		}
		return list.OrderByDescending((InstalledVersion v) => v.LastModified).ToList();
	}

	public static async Task LaunchAsync(string versionId, string username, int maxRamMb = 2048, bool forceCheckFiles = false)
	{
		if (IsLaunching)
		{
			LaunchManager.LogReceived?.Invoke(LanguageManager.Get("LogAlreadyLaunching"));
			return;
		}
		IsLaunching = true;
		try
		{
			RaiseProgress(LanguageManager.Get("ProgressInit"), LanguageManager.Get("ProgressInitMsg"), 0.0);
			RaiseLog(string.Format(LanguageManager.Get("LogLaunchVersion"), versionId));
			RaiseLog(string.Format(LanguageManager.Get("LogPlayer"), username));
			RaiseLog(string.Format(LanguageManager.Get("LogMaxRam"), maxRamMb));
			RaiseLog(string.Format(LanguageManager.Get("LogForceCheck"), forceCheckFiles));
			string gameDir = GetVersionGameDir(versionId);
			EnsureVersionIsolationDirs(versionId);
			RaiseLog(string.Format(LanguageManager.Get("LogIsolationDir"), gameDir));
			RaiseProgress(LanguageManager.Get("ProgressCheckDeps"), LanguageManager.Get("ProgressCheckDepsMsg"), 5.0);
			try
			{
				await DownloadManager.EnsureBaseJarAsync(versionId);
			}
			catch (Exception ex2)
			{
				Exception jarEx = ex2;
				RaiseLog(string.Format(LanguageManager.Get("LogBaseJarCheckFail"), jarEx.Message));
			}
			MinecraftPath path = new MinecraftPath(MinecraftPath);
			MinecraftLauncher launcher = new MinecraftLauncher(path);
			int skinType = LauncherConfig.Current.SkinType;
			string skinId = LauncherConfig.Current.SkinId ?? "";
			MSession session = await CreateSessionForSkinAsync(username, skinType, skinId);
			MLaunchOption launchOption = new MLaunchOption
			{
				MaximumRamMb = maxRamMb,
				Session = session,
				Path = CreateIsolatedLaunchPath(versionId),
				GameLauncherName = "DIL"
			};
			if (LauncherConfig.Current.AutoChinese)
			{
				try
				{
					launchOption.ExtraGameArguments = new MArgument[2]
					{
						new MArgument("--language"),
						new MArgument("zh_CN")
					};
				}
				catch
				{
				}
			}
			if (forceCheckFiles)
			{
				RaiseProgress(LanguageManager.Get("ProgressCheckFiles"), LanguageManager.Get("ProgressCheckFilesMsg"), 10.0);
				launcher.FileProgressChanged += delegate(object? sender, InstallerProgressChangedEventArgs e)
				{
					RaiseLog($"[{e.EventType}] {e.Name} ({e.ProgressedTasks}/{e.TotalTasks})");
					double progress2 = 10.0 + ((e.TotalTasks > 0) ? ((double)e.ProgressedTasks / (double)e.TotalTasks * 60.0) : 0.0);
					RaiseProgress(LanguageManager.Get("ProgressCheckFiles"), string.Format(LanguageManager.Get("LogVerifyFile"), e.Name), progress2);
				};
				RaiseProgress(LanguageManager.Get("ProgressBuildProcess"), LanguageManager.Get("ProgressBuildProcessMsg"), 75.0);
				Process process = await launcher.CreateProcessAsync(versionId, launchOption);
				var (stdoutBuilder, stderrBuilder) = PrepareProcessForCapture(process);
				RaiseProgress(LanguageManager.Get("ProgressLaunching"), LanguageManager.Get("ProgressLaunchingMsg"), 90.0);
				StartProcessWithCapture(process);
				RaiseProgress(LanguageManager.Get("ProgressVerifying"), LanguageManager.Get("ProgressVerifyingMsg"), 95.0);
				if (!(await WaitForGameStartupAsync(process)))
				{
					int exitCode = -1;
					try
					{
						if (process.HasExited)
						{
							exitCode = process.ExitCode;
						}
					}
					catch
					{
					}
					string errMsg = ((exitCode != 0) ? string.Format(LanguageManager.Get("LogProcessExitCode"), exitCode) : LanguageManager.Get("LogProcessImmediateExit"));
					string capturedOutput = stderrBuilder.ToString() + "\n" + stdoutBuilder.ToString();
					if (!string.IsNullOrWhiteSpace(capturedOutput))
					{
						string[] lines = capturedOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
						string[] lastLines = ((lines.Length > 50) ? lines.Skip(lines.Length - 50).ToArray() : lines);
						errMsg += string.Format(LanguageManager.Get("LogGameOutput"), lastLines.Length, string.Join("\n", lastLines));
					}
					throw new Exception(errMsg);
				}
				RaiseProgress(LanguageManager.Get("ProgressDone"), LanguageManager.Get("ProgressDoneMsg"), 100.0);
				RaiseLog(string.Format(LanguageManager.Get("LogLaunchSuccess"), process.Id));
			}
			else
			{
				RaiseProgress(LanguageManager.Get("ProgressBuildProcess"), LanguageManager.Get("ProgressSkipCheckMsg"), 50.0);
				RaiseLog(LanguageManager.Get("LogSkipFileCheckNote"));
				Process process2;
				try
				{
					process2 = await launcher.BuildProcessAsync(versionId, launchOption);
				}
				catch (Exception buildEx)
				{
					RaiseLog(string.Format(LanguageManager.Get("LogQuickBuildFail"), buildEx.Message));
					RaiseProgress(LanguageManager.Get("ProgressPatchFiles"), LanguageManager.Get("ProgressPatchFilesMsg"), 20.0);
					int maxRetries = 3;
					Process resultProcess = null;
					Exception lastError = null;
					for (int attempt = 1; attempt <= maxRetries; attempt++)
					{
						try
						{
							RaiseProgress(LanguageManager.Get("ProgressPatchFiles"), string.Format(LanguageManager.Get("LogPatchRetry"), attempt, maxRetries), 20.0);
							launcher.FileProgressChanged += delegate(object? sender, InstallerProgressChangedEventArgs e)
							{
								RaiseLog($"[{e.EventType}] {e.Name} ({e.ProgressedTasks}/{e.TotalTasks})");
								double progress = 20.0 + ((e.TotalTasks > 0) ? ((double)e.ProgressedTasks / (double)e.TotalTasks * 50.0) : 0.0);
								RaiseProgress(LanguageManager.Get("ProgressPatchFiles"), string.Format(LanguageManager.Get("LogDownloadFile"), e.Name), progress);
							};
							resultProcess = await launcher.CreateProcessAsync(versionId, launchOption);
						}
						catch (Exception retryEx)
						{
							lastError = retryEx;
							if ((retryEx.Message.Contains("EOF") || retryEx.Message.Contains("transport") || retryEx.Message.Contains("connection") || retryEx.Message.Contains("timeout") || retryEx.Message.Contains("SSL")) && attempt < maxRetries)
							{
								RaiseLog(string.Format(LanguageManager.Get("LogNetworkRetry"), attempt, retryEx.Message));
								await Task.Delay(3000);
								continue;
							}
							throw;
						}
						break;
					}
					if (resultProcess == null)
					{
						throw lastError ?? buildEx;
					}
					process2 = resultProcess;
				}
				RaiseProgress(LanguageManager.Get("ProgressLaunching"), LanguageManager.Get("ProgressLaunchingMsg"), 90.0);
				var (stdoutBuilder2, stderrBuilder2) = PrepareProcessForCapture(process2);
				StartProcessWithCapture(process2);
				RaiseProgress(LanguageManager.Get("ProgressVerifying"), LanguageManager.Get("ProgressVerifyingMsg"), 95.0);
				if (!(await WaitForGameStartupAsync(process2)))
				{
					int exitCode2 = -1;
					try
					{
						if (process2.HasExited)
						{
							exitCode2 = process2.ExitCode;
						}
					}
					catch
					{
					}
					string errMsg2 = ((exitCode2 != 0) ? string.Format(LanguageManager.Get("LogProcessExitCode"), exitCode2) : LanguageManager.Get("LogProcessImmediateExit"));
					string capturedOutput2 = stderrBuilder2.ToString() + "\n" + stdoutBuilder2.ToString();
					if (!string.IsNullOrWhiteSpace(capturedOutput2))
					{
						string[] lines2 = capturedOutput2.Split('\n', StringSplitOptions.RemoveEmptyEntries);
						string[] lastLines2 = ((lines2.Length > 50) ? lines2.Skip(lines2.Length - 50).ToArray() : lines2);
						errMsg2 += string.Format(LanguageManager.Get("LogGameOutput"), lastLines2.Length, string.Join("\n", lastLines2));
					}
					throw new Exception(errMsg2);
				}
				RaiseProgress(LanguageManager.Get("ProgressDone"), LanguageManager.Get("ProgressDoneMsg"), 100.0);
				RaiseLog(string.Format(LanguageManager.Get("LogLaunchSuccess"), process2.Id));
			}
			LaunchManager.LaunchCompleted?.Invoke();
		}
		catch (Exception ex3)
		{
			Exception ex = ex3;
			string detail = ex.Message;
			if (ex.InnerException != null)
			{
				detail = detail + "\n→ " + ex.InnerException.Message;
			}
			RaiseLog(string.Format(LanguageManager.Get("LogLaunchFail"), detail));
			RaiseLog(string.Format(LanguageManager.Get("LogStack"), ex.StackTrace));
			try
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogTitle"), DateTime.Now));
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogVersion"), versionId));
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogPlayer"), username));
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogMaxRam"), maxRamMb));
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogForceCheck"), forceCheckFiles));
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogGameDir"), GetVersionGameDir(versionId)));
				StringBuilder stringBuilder = sb;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
				handler.AppendLiteral("MinecraftPath: ");
				handler.AppendFormatted(MinecraftPath);
				stringBuilder.AppendLine(ref handler);
				sb.AppendLine();
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogExceptionType"), ex.GetType().FullName));
				sb.AppendLine(string.Format(LanguageManager.Get("ErrLogExceptionMsg"), ex.Message));
				if (ex.InnerException != null)
				{
					sb.AppendLine(string.Format(LanguageManager.Get("ErrLogInnerExceptionType"), ex.InnerException.GetType().FullName));
					sb.AppendLine(string.Format(LanguageManager.Get("ErrLogInnerExceptionMsg"), ex.InnerException.Message));
				}
				sb.AppendLine();
				sb.AppendLine(LanguageManager.Get("ErrLogStackTrace"));
				sb.AppendLine(ex.StackTrace);
				if (ex.InnerException?.StackTrace != null)
				{
					sb.AppendLine(LanguageManager.Get("ErrLogInnerStackTrace"));
					sb.AppendLine(ex.InnerException.StackTrace);
				}
				sb.AppendLine();
			}
			catch
			{
			}
			LaunchManager.LaunchFailed?.Invoke(ex);
		}
		finally
		{
			IsLaunching = false;
		}
	}

	private static async Task<MSession> CreateSessionForSkinAsync(string username, int skinType, string skinId)
	{
		try
		{
			int num;
			switch (skinType)
			{
			case 1:
				RaiseLog(LanguageManager.Get("LogSkinSteve"));
				return new MSession
				{
					Username = username,
					UUID = GenerateOfflineUuid("Steve"),
					AccessToken = "0",
					UserType = "legacy"
				};
			case 2:
				RaiseLog(LanguageManager.Get("LogSkinAlex"));
				return new MSession
				{
					Username = username,
					UUID = GenerateOfflineUuid("Alex"),
					AccessToken = "0",
					UserType = "legacy"
				};
			case 3:
				num = ((!string.IsNullOrWhiteSpace(skinId)) ? 1 : 0);
				break;
			default:
				num = 0;
				break;
			}
			if (num != 0)
			{
				RaiseLog(string.Format(LanguageManager.Get("LogSkinPremium"), skinId));
				string uuid = await FetchPremiumUuidAsync(skinId.Trim());
				if (!string.IsNullOrEmpty(uuid))
				{
					RaiseLog(string.Format(LanguageManager.Get("LogGotPremiumUuid"), uuid));
					return new MSession
					{
						Username = skinId.Trim(),
						UUID = uuid,
						AccessToken = "0",
						UserType = "legacy"
					};
				}
				RaiseLog(LanguageManager.Get("LogPremiumUuidFail"));
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			RaiseLog(string.Format(LanguageManager.Get("LogSkinSetupFail"), ex.Message));
		}
		return MSession.CreateOfflineSession(username);
	}

	private static string GenerateOfflineUuid(string playerName)
	{
		byte[] bytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + playerName);
		using MD5 mD = MD5.Create();
		byte[] array = mD.ComputeHash(bytes);
		array[6] = (byte)((array[6] & 0xFu) | 0x30u);
		array[8] = (byte)((array[8] & 0x3Fu) | 0x80u);
		return new Guid(array).ToString("N");
	}

	private static async Task<string?> FetchPremiumUuidAsync(string playerName)
	{
		try
		{
			using HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(10L);
			client.DefaultRequestHeaders.Add("User-Agent", "DIL/1.0");
			string url = "https://api.mojang.com/users/profiles/minecraft/" + Uri.EscapeDataString(playerName);
			using HttpResponseMessage resp = await client.GetAsync(url);
			if (!resp.IsSuccessStatusCode)
			{
				return null;
			}
			using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
			if (doc.RootElement.TryGetProperty("id", out var idEl))
			{
				string id = idEl.GetString();
				if (!string.IsNullOrEmpty(id) && id.Length == 32)
				{
					return $"{id.Substring(0, 8)}-{id.Substring(8, 4)}-{id.Substring(12, 4)}-{id.Substring(16, 4)}-{id.Substring(20, 12)}";
				}
				return id;
			}
		}
		catch
		{
		}
		return null;
	}

	private static void RaiseProgress(string stage, string message, double progress)
	{
		LaunchManager.ProgressChanged?.Invoke(new LaunchProgress
		{
			Stage = stage,
			Message = message,
			Progress = progress
		});
	}

	private static void RaiseLog(string message)
	{
		LaunchManager.LogReceived?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
	}

	private static (StringBuilder stdout, StringBuilder stderr) PrepareProcessForCapture(Process process)
	{
		StringBuilder stdoutBuilder = new StringBuilder();
		StringBuilder stderrBuilder = new StringBuilder();
		try
		{
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.CreateNoWindow = false;
		}
		catch
		{
		}
		process.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (!string.IsNullOrEmpty(e.Data))
			{
				stdoutBuilder.AppendLine(e.Data);
			}
		};
		process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (!string.IsNullOrEmpty(e.Data))
			{
				stderrBuilder.AppendLine(e.Data);
			}
		};
		return (stdout: stdoutBuilder, stderr: stderrBuilder);
	}

	private static void StartProcessWithCapture(Process process)
	{
		process.Start();
		try
		{
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
		}
		catch
		{
		}
	}

	private static bool HasVisibleGameWindow(int processId)
	{
		bool found = false;
		EnumWindows(delegate(nint hWnd, nint lParam)
		{
			GetWindowThreadProcessId(hWnd, out var processId2);
			if (processId2 != (uint)processId || !IsWindowVisible(hWnd) || !GetWindowRect(hWnd, out var lpRect) || lpRect.Right <= lpRect.Left || lpRect.Bottom <= lpRect.Top)
			{
				return true;
			}
			found = true;
			return false;
		}, IntPtr.Zero);
		return found;
	}

	private static async Task<bool> WaitForGameStartupAsync(Process process, int timeoutSeconds = 30)
	{
		TimeSpan pollInterval = TimeSpan.FromMilliseconds(100L, 0L);
		TimeSpan timeout = TimeSpan.FromSeconds(timeoutSeconds);
		DateTime startTime = DateTime.UtcNow;
		while (!(DateTime.UtcNow - startTime > timeout))
		{
			try
			{
				if (process.HasExited)
				{
					return false;
				}
			}
			catch (InvalidOperationException)
			{
				return false;
			}
			if (HasVisibleGameWindow(process.Id))
			{
				try
				{
					if (!process.HasExited)
					{
						return true;
					}
					return false;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}
			await Task.Delay(pollInterval);
		}
		try
		{
			return !process.HasExited;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}
}
