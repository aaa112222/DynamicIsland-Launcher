using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace DynamicIsland;

public static class UpdateChecker
{
	private const string RepoOwner = "aaa112222";

	private const string RepoName = "DynamicIsland-Launcher";

	private const string ApiUrl = "https://api.github.com/repos/aaa112222/DynamicIsland-Launcher/releases/latest";

	private static readonly HttpClient Client = CreateClient();

	public static string CurrentVersion
	{
		get
		{
			try
			{
				Version version = Assembly.GetExecutingAssembly().GetName().Version;
				if (version != null)
				{
					return $"{version.Major}.{version.Minor}.{version.Build}";
				}
			}
			catch
			{
			}
			return "1.0.0";
		}
	}

	public static event Action<UpdateInfo>? UpdateAvailable;

	public static event Action<string>? CheckFailed;

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(15L);
		httpClient.DefaultRequestHeaders.Add("User-Agent", "DIL/1.0");
		httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
		return httpClient;
	}

	public static async Task CheckAsync(bool silent = true)
	{
		try
		{
			using HttpResponseMessage resp = await Client.GetAsync("https://api.github.com/repos/aaa112222/DynamicIsland-Launcher/releases/latest");
			if (!resp.IsSuccessStatusCode)
			{
				if (!silent)
				{
					UpdateChecker.CheckFailed?.Invoke($"HTTP {resp.StatusCode}");
				}
				return;
			}
			UpdateInfo info = JsonSerializer.Deserialize<UpdateInfo>(await resp.Content.ReadAsStringAsync());
			if (info == null || string.IsNullOrEmpty(info.TagName))
			{
				if (!silent)
				{
					UpdateChecker.CheckFailed?.Invoke("解析失败");
				}
				return;
			}
			string latest = info.TagName.TrimStart('v', 'V');
			if (IsNewerVersion(latest, CurrentVersion))
			{
				UpdateChecker.UpdateAvailable?.Invoke(info);
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			if (!silent)
			{
				UpdateChecker.CheckFailed?.Invoke(ex.Message);
			}
		}
	}

	private static bool IsNewerVersion(string latest, string current)
	{
		try
		{
			string[] array = latest.Split('.');
			string[] array2 = current.Split('.');
			int num = Math.Max(array.Length, array2.Length);
			for (int i = 0; i < num; i++)
			{
				int result;
				int num2 = ((i < array.Length && int.TryParse(array[i], out result)) ? result : 0);
				int result2;
				int num3 = ((i < array2.Length && int.TryParse(array2[i], out result2)) ? result2 : 0);
				if (num2 > num3)
				{
					return true;
				}
				if (num2 < num3)
				{
					return false;
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}
}
