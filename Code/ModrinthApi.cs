using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DynamicIsland;

public static class ModrinthApi
{
	private const string BaseUrl = "https://api.modrinth.com/v2";

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(30L);
		httpClient.DefaultRequestHeaders.Add("User-Agent", "DIL/1.0");
		return httpClient;
	}

	public static async Task<List<ModrinthProject>> SearchAsync(string query, ResourceType type, string minecraftVersion = "", string loader = "")
	{
		using HttpClient client = CreateClient();
		if (1 == 0)
		{
		}
		string text = type switch
		{
			ResourceType.Mod => "mod", 
			ResourceType.Shader => "shader", 
			ResourceType.ResourcePack => "resourcepack", 
			_ => "mod", 
		};
		if (1 == 0)
		{
		}
		string projectType = text;
		List<List<string>> facets = new List<List<string>>
		{
			new List<string> { "project_type:" + projectType }
		};
		if (!string.IsNullOrEmpty(minecraftVersion))
		{
			facets.Add(new List<string> { "versions:" + minecraftVersion });
		}
		if (!string.IsNullOrEmpty(loader))
		{
			facets.Add(new List<string> { "categories:" + loader.ToLowerInvariant() });
		}
		string facetsJson = JsonSerializer.Serialize(facets);
		string url = $"{"https://api.modrinth.com/v2"}/search?limit=24&index=downloads&query={Uri.EscapeDataString(query ?? "")}&facets={Uri.EscapeDataString(facetsJson)}";
		JsonDocument doc = JsonDocument.Parse(await client.GetStringAsync(url));
		List<ModrinthProject> result = new List<ModrinthProject>();
		if (doc.RootElement.TryGetProperty("hits", out var hits))
		{
			foreach (JsonElement hit in hits.EnumerateArray())
			{
				JsonElement pid;
				JsonElement slug;
				JsonElement title;
				JsonElement desc;
				JsonElement icon;
				JsonElement dl;
				ModrinthProject proj = new ModrinthProject
				{
					ProjectId = (hit.TryGetProperty("project_id", out pid) ? (pid.GetString() ?? "") : ""),
					Slug = (hit.TryGetProperty("slug", out slug) ? (slug.GetString() ?? "") : ""),
					Title = (hit.TryGetProperty("title", out title) ? (title.GetString() ?? "") : ""),
					Description = (hit.TryGetProperty("description", out desc) ? (desc.GetString() ?? "") : ""),
					IconUrl = (hit.TryGetProperty("icon_url", out icon) ? (icon.GetString() ?? "") : ""),
					Downloads = (hit.TryGetProperty("downloads", out dl) ? dl.GetInt64() : 0)
				};
				if (hit.TryGetProperty("versions", out var vers))
				{
					proj.GameVersions = (from v in vers.EnumerateArray()
						select v.GetString() ?? "" into v
						where !string.IsNullOrEmpty(v)
						select v).ToList();
				}
				if (hit.TryGetProperty("categories", out var cats))
				{
					proj.Loaders = (from v in cats.EnumerateArray()
						select v.GetString() ?? "" into v
						where !string.IsNullOrEmpty(v)
						select v).Where(delegate(string v)
					{
						int result3;
						switch (v)
						{
						default:
							result3 = ((v == "iris") ? 1 : 0);
							break;
						case "fabric":
						case "forge":
						case "quilt":
						case "neoforge":
						case "liteloader":
							result3 = 1;
							break;
						}
						return (byte)result3 != 0;
					}).Select(delegate(string v)
					{
						object result2;
						if (!(v == "neoforge"))
						{
							char reference = char.ToUpper(v[0]);
							result2 = string.Concat(new ReadOnlySpan<char>(ref reference), v.Substring(1));
						}
						else
						{
							result2 = "NeoForge";
						}
						return (string)result2;
					}).ToList();
				}
				result.Add(proj);
				pid = default(JsonElement);
				slug = default(JsonElement);
				title = default(JsonElement);
				desc = default(JsonElement);
				icon = default(JsonElement);
				dl = default(JsonElement);
				vers = default(JsonElement);
				cats = default(JsonElement);
			}
		}
		return result;
	}

	public static async Task<List<ModrinthVersion>> GetProjectVersions(string projectId)
	{
		using HttpClient client = CreateClient();
		string versionsUrl = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(projectId) + "/version";
		JsonDocument doc = JsonDocument.Parse(await client.GetStringAsync(versionsUrl));
		List<ModrinthVersion> versions = new List<ModrinthVersion>();
		foreach (JsonElement versionElem in doc.RootElement.EnumerateArray())
		{
			JsonElement idProp;
			JsonElement nameProp;
			JsonElement verProp;
			JsonElement dateProp;
			JsonElement vtProp;
			ModrinthVersion version = new ModrinthVersion
			{
				Id = (versionElem.TryGetProperty("id", out idProp) ? (idProp.GetString() ?? "") : ""),
				Name = (versionElem.TryGetProperty("name", out nameProp) ? (nameProp.GetString() ?? "") : ""),
				VersionNumber = (versionElem.TryGetProperty("version_number", out verProp) ? (verProp.GetString() ?? "") : ""),
				DatePublished = (versionElem.TryGetProperty("date_published", out dateProp) ? (dateProp.GetString() ?? "") : ""),
				VersionType = (versionElem.TryGetProperty("version_type", out vtProp) ? (vtProp.GetString() ?? "release") : "release")
			};
			if (versionElem.TryGetProperty("game_versions", out var gameVersions))
			{
				version.GameVersions = (from v in gameVersions.EnumerateArray()
					select v.GetString() ?? "" into v
					where !string.IsNullOrEmpty(v)
					select v).ToList();
			}
			if (versionElem.TryGetProperty("loaders", out var loaders))
			{
				version.Loaders = (from v in loaders.EnumerateArray()
					select v.GetString() ?? "" into v
					where !string.IsNullOrEmpty(v)
					select v).ToList();
			}
			if (versionElem.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
			{
				JsonElement file = files[0];
				version.DownloadUrl = (file.TryGetProperty("url", out var urlProp) ? (urlProp.GetString() ?? "") : "");
				version.FileName = (file.TryGetProperty("filename", out var fnProp) ? (fnProp.GetString() ?? "") : "");
				urlProp = default(JsonElement);
				fnProp = default(JsonElement);
			}
			versions.Add(version);
			idProp = default(JsonElement);
			nameProp = default(JsonElement);
			verProp = default(JsonElement);
			dateProp = default(JsonElement);
			vtProp = default(JsonElement);
			gameVersions = default(JsonElement);
			loaders = default(JsonElement);
			files = default(JsonElement);
		}
		return versions.OrderByDescending((ModrinthVersion v) => v.DatePublished).ToList();
	}

	public static async Task DownloadResourceAsync(ModrinthProject project, ResourceType type, string minecraftVersion = "", string loader = "")
	{
		using HttpClient client = CreateClient();
		string versionsUrl = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(project.ProjectId) + "/version";
		if (!string.IsNullOrEmpty(minecraftVersion) && !string.IsNullOrEmpty(loader))
		{
			string loaders = JsonSerializer.Serialize(new string[1] { loader.ToLowerInvariant() });
			string gameVersions = JsonSerializer.Serialize(new string[1] { minecraftVersion });
			versionsUrl = versionsUrl + "?loaders=" + Uri.EscapeDataString(loaders) + "&game_versions=" + Uri.EscapeDataString(gameVersions);
		}
		JsonDocument doc = JsonDocument.Parse(await client.GetStringAsync(versionsUrl));
		string downloadUrl = null;
		string fileName = null;
		if (doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
		{
			JsonElement file = files[0];
			downloadUrl = (file.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null);
			fileName = (file.TryGetProperty("filename", out var fnProp) ? fnProp.GetString() : null);
		}
		if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
		{
			throw new Exception(LanguageManager.Get("ErrNoFile"));
		}
		await DownloadFileAsync(downloadUrl, fileName, type);
	}

	public static async Task DownloadVersionAsync(ModrinthVersion version, ResourceType type, string mirror = "")
	{
		if (string.IsNullOrEmpty(version.DownloadUrl) || string.IsNullOrEmpty(version.FileName))
		{
			throw new Exception(LanguageManager.Get("ErrNoDownloadableFile"));
		}
		await DownloadFileAsync(version.DownloadUrl, version.FileName, type, mirror);
	}

	private static async Task DownloadFileAsync(string downloadUrl, string fileName, ResourceType type, string mirror = "")
	{
		string actualUrl = downloadUrl;
		if (!string.IsNullOrEmpty(mirror))
		{
			if (mirror == "ghproxy")
			{
				if (downloadUrl.Contains("github.com"))
				{
					actualUrl = "https://ghproxy.com/" + downloadUrl;
				}
			}
			else
			{
				actualUrl = downloadUrl.Replace("https://cdn.modrinth.com", mirror);
			}
		}
		using HttpClient client = CreateClient();
		string baseDir = DownloadManager.MinecraftPath;
		if (1 == 0)
		{
		}
		string text = type switch
		{
			ResourceType.Mod => Path.Combine(baseDir, "mods"), 
			ResourceType.Shader => Path.Combine(baseDir, "shaderpacks"), 
			ResourceType.ResourcePack => Path.Combine(baseDir, "resourcepacks"), 
			_ => baseDir, 
		};
		if (1 == 0)
		{
		}
		string targetDir = text;
		Directory.CreateDirectory(targetDir);
		string targetPath = Path.Combine(targetDir, fileName);
		byte[] data;
		try
		{
			data = await DownloadManager.DownloadBytesWithRetryAsync(client, actualUrl);
		}
		catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException || ex is IOException) ? 1 : 0) != 0)
		{
			if (!(actualUrl != downloadUrl))
			{
				throw;
			}
			data = await DownloadManager.DownloadBytesWithRetryAsync(client, downloadUrl);
		}
		await File.WriteAllBytesAsync(targetPath, data);
	}
}
