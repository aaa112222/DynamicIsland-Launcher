using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace DynamicIsland;

public static class LauncherConfig
{
	private static readonly object _lock = new object();

	private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.cfg");

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static LauncherConfigData Current { get; private set; } = new LauncherConfigData();


	public static string ConfigFilePath => ConfigPath;

	public static event EventHandler? Changed;

	public static void Load()
	{
		lock (_lock)
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					string json = File.ReadAllText(ConfigPath);
					LauncherConfigData launcherConfigData = JsonSerializer.Deserialize<LauncherConfigData>(json, JsonOpts);
					if (launcherConfigData != null)
					{
						launcherConfigData.Version = 1;
						Current = launcherConfigData;
					}
				}
			}
			catch
			{
				Current = new LauncherConfigData();
			}
		}
	}

	public static void Save()
	{
		lock (_lock)
		{
			try
			{
				string directoryName = Path.GetDirectoryName(ConfigPath);
				if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				string contents = JsonSerializer.Serialize(Current, JsonOpts);
				File.WriteAllText(ConfigPath, contents);
			}
			catch
			{
			}
		}
		LauncherConfig.Changed?.Invoke(null, EventArgs.Empty);
	}

	public static void ResetSection(Action<LauncherConfigData> resetAction)
	{
		lock (_lock)
		{
			resetAction(Current);
		}
		Save();
	}

	public static void Reset()
	{
		lock (_lock)
		{
			Current = new LauncherConfigData();
		}
		Save();
	}

	public static Color GetThemeColor()
	{
		return GetThemeColor(Current.Theme);
	}

	public static Color GetThemeColor(int theme)
	{
		if (1 == 0)
		{
		}
		Color result = theme switch
		{
			0 => Color.FromRgb(72, 144, 245), 
			1 => Color.FromRgb(94, 197, 207), 
			2 => Color.FromRgb(122, 200, 79), 
			3 => Color.FromRgb(240, 199, 64), 
			4 => Color.FromRgb(180, 130, 90), 
			5 => Color.FromRgb(96, 96, 102), 
			6 => Color.FromRgb(236, 90, 156), 
			7 => Color.FromRgb(150, 94, 222), 
			8 => Color.FromRgb(212, 162, 76), 
			9 => Color.FromRgb(232, 138, 64), 
			10 => Color.FromRgb(228, 80, 80), 
			11 => Color.FromRgb(64, 158, byte.MaxValue), 
			12 => Color.FromRgb(120, 200, 120), 
			13 => Color.FromRgb(byte.MaxValue, 215, 0), 
			_ => Color.FromRgb(72, 144, 245), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public static Color ApplyHslAdjust(Color baseColor)
	{
		if (Current.Hue == 0 && Current.Saturation == 0 && Current.Lightness == 0)
		{
			return baseColor;
		}
		RgbToHsl(baseColor, out var h, out var s, out var l);
		h = (h + (double)Current.Hue) % 360.0;
		if (h < 0.0)
		{
			h += 360.0;
		}
		s = Math.Clamp(s + (double)Current.Saturation / 100.0, 0.0, 1.0);
		l = Math.Clamp(l + (double)Current.Lightness / 100.0, 0.0, 1.0);
		return HslToRgb(h, s, l);
	}

	private static void RgbToHsl(Color c, out double h, out double s, out double l)
	{
		double num = (double)(int)c.R / 255.0;
		double num2 = (double)(int)c.G / 255.0;
		double num3 = (double)(int)c.B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		l = (num4 + num5) / 2.0;
		if (Math.Abs(num4 - num5) < 1E-09)
		{
			h = 0.0;
			s = 0.0;
			return;
		}
		double num6 = num4 - num5;
		s = ((l > 0.5) ? (num6 / (2.0 - num4 - num5)) : (num6 / (num4 + num5)));
		if (Math.Abs(num4 - num) < 1E-09)
		{
			h = (num2 - num3) / num6 + (double)((num2 < num3) ? 6 : 0);
		}
		else if (Math.Abs(num4 - num2) < 1E-09)
		{
			h = (num3 - num) / num6 + 2.0;
		}
		else
		{
			h = (num - num2) / num6 + 4.0;
		}
		h *= 60.0;
	}

	private static Color HslToRgb(double h, double s, double l)
	{
		double num3;
		double num2;
		double num;
		if (s == 0.0)
		{
			num3 = (num2 = (num = l));
		}
		else
		{
			double num4 = ((l < 0.5) ? (l * (1.0 + s)) : (l + s - l * s));
			double p = 2.0 * l - num4;
			num3 = HueToRgb(p, num4, h / 360.0 + 1.0 / 3.0);
			num2 = HueToRgb(p, num4, h / 360.0);
			num = HueToRgb(p, num4, h / 360.0 - 1.0 / 3.0);
		}
		return Color.FromRgb((byte)Math.Round(num3 * 255.0), (byte)Math.Round(num2 * 255.0), (byte)Math.Round(num * 255.0));
	}

	private static double HueToRgb(double p, double q, double t)
	{
		if (t < 0.0)
		{
			t += 1.0;
		}
		if (t > 1.0)
		{
			t -= 1.0;
		}
		if (t < 1.0 / 6.0)
		{
			return p + (q - p) * 6.0 * t;
		}
		if (t < 0.5)
		{
			return q;
		}
		if (t < 2.0 / 3.0)
		{
			return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
		}
		return p;
	}
}
