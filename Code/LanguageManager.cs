using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace DynamicIsland;

public static class LanguageManager
{
	public const string English = "en_US";

	public const string Chinese = "zh_CN";

	private static string _current = "en_US";

	public static string Current => _current;

	public static bool Applied { get; private set; }

	public static event Action? LanguageChanged;

	public static void Apply(string lang)
	{
		if (string.IsNullOrEmpty(lang))
		{
			lang = "en_US";
		}
		if (lang == _current && Applied)
		{
			return;
		}
		_current = lang;
		ResourceDictionary item = new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Langs/Lang." + lang + ".xaml", UriKind.Absolute)
		};
		Collection<ResourceDictionary> collection = Application.Current?.Resources.MergedDictionaries;
		if (collection == null)
		{
			return;
		}
		for (int num = collection.Count - 1; num >= 0; num--)
		{
			if (collection[num].Source != null && collection[num].Source.OriginalString.Contains("Lang."))
			{
				collection.RemoveAt(num);
			}
		}
		collection.Add(item);
		Applied = true;
		LanguageManager.LanguageChanged?.Invoke();
	}

	public static string Get(string key)
	{
		try
		{
			return (Application.Current?.TryFindResource(key) as string) ?? key;
		}
		catch
		{
			return key;
		}
	}
}
