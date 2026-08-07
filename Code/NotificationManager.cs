using System;

namespace DynamicIsland;

public static class NotificationManager
{
	public static event Action<string>? Requested;

	public static void Show(string message)
	{
		if (!string.IsNullOrEmpty(message))
		{
			NotificationManager.Requested?.Invoke(message);
		}
	}
}
