using System;
using System.Collections.Concurrent;

namespace DynamicIsland;

public static class DataCache
{
	private static readonly ConcurrentDictionary<string, (object? Data, DateTime Time)> _cache = new ConcurrentDictionary<string, (object?, DateTime)>();
	private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10L);

	private static string Key(params object[] parts)
	{
		return string.Join("|", parts);
	}

	public static T? Get<T>(params object[] parts)
	{
		string key = Key(parts);
		if (_cache.TryGetValue(key, out (object?, DateTime) value) && DateTime.Now - value.Item2 < DefaultTtl)
		{
			return (T)value.Item1;
		}
		_cache.TryRemove(key, out (object?, DateTime) _);
		return default;
	}

	public static void Set<T>(T data, params object[] parts)
	{
		string key = Key(parts);
		_cache[key] = (data, DateTime.Now);
	}

	public static bool Has(params object[] parts)
	{
		string key = Key(parts);
		if (_cache.TryGetValue(key, out (object?, DateTime) value) && DateTime.Now - value.Item2 < DefaultTtl)
		{
			return true;
		}
		_cache.TryRemove(key, out (object?, DateTime) _);
		return false;
	}

	public static void Clear()
	{
		_cache.Clear();
	}
}