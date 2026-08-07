using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicIsland;

public static class VersionSettingsManager
{
	private static readonly object _lock = new object();

	private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version_settings.json");

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private static Dictionary<string, VersionSettings> _cache = new Dictionary<string, VersionSettings>();

	public static void Load()
	{
		lock (_lock)
		{
			try
			{
				if (File.Exists(SettingsPath))
				{
					string json = File.ReadAllText(SettingsPath);
					Dictionary<string, VersionSettings> dictionary = JsonSerializer.Deserialize<Dictionary<string, VersionSettings>>(json, JsonOpts);
					_cache = dictionary ?? new Dictionary<string, VersionSettings>();
				}
			}
			catch
			{
				_cache = new Dictionary<string, VersionSettings>();
			}
		}
	}

	public static void Save()
	{
		lock (_lock)
		{
			try
			{
				string contents = JsonSerializer.Serialize(_cache, JsonOpts);
				File.WriteAllText(SettingsPath, contents);
			}
			catch
			{
			}
		}
	}

	public static VersionSettings Get(string versionId)
	{
		lock (_lock)
		{
			if (_cache.TryGetValue(versionId, out VersionSettings value))
			{
				return value;
			}
			return new VersionSettings
			{
				VersionId = versionId
			};
		}
	}

	public static void Set(VersionSettings settings)
	{
		lock (_lock)
		{
			if (settings.VersionId == null)
			{
				string text2 = (settings.VersionId = "");
			}
			_cache[settings.VersionId] = settings;
			Save();
		}
	}

	public static void Remove(string versionId)
	{
		lock (_lock)
		{
			_cache.Remove(versionId);
			Save();
		}
	}

	public static string GetDisplayName(string versionId)
	{
		VersionSettings versionSettings = Get(versionId);
		return string.IsNullOrWhiteSpace(versionSettings.DisplayName) ? versionId : versionSettings.DisplayName;
	}

	public static int GetMemoryMb(string versionId, int globalRam)
	{
		VersionSettings versionSettings = Get(versionId);
		return versionSettings.UseCustomMemory ? versionSettings.CustomMemoryMb : globalRam;
	}

	public static string GetJvmArgs(string versionId, string globalArgs)
	{
		VersionSettings versionSettings = Get(versionId);
		return string.IsNullOrWhiteSpace(versionSettings.JvmArgs) ? globalArgs : versionSettings.JvmArgs;
	}

	public static string GetGameArgs(string versionId, string globalArgs)
	{
		VersionSettings versionSettings = Get(versionId);
		return string.IsNullOrWhiteSpace(versionSettings.GameArgs) ? globalArgs : versionSettings.GameArgs;
	}
}