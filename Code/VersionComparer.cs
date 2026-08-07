using System;
using System.Collections.Generic;

namespace DynamicIsland;

internal class VersionComparer : IComparer<string>
{
	public int Compare(string? x, string? y)
	{
		if (x == null || y == null)
		{
			return 0;
		}
		string[] array = x.Split('.');
		string[] array2 = y.Split('.');
		for (int i = 0; i < Math.Min(array.Length, array2.Length); i++)
		{
			if (int.TryParse(array[i], out var result) && int.TryParse(array2[i], out var result2))
			{
				if (result != result2)
				{
					return result.CompareTo(result2);
				}
				continue;
			}
			return string.CompareOrdinal(array[i], array2[i]);
		}
		return array.Length.CompareTo(array2.Length);
	}
}
