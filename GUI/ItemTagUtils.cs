using Godot;
using System.Globalization;

public static class ItemTagUtils
{
	// Rozparsuje tag "klic:hodnota" (např. "speed:15") na klíč a číselnou hodnotu.
	// Vrací false (a jen zaloguje varování), pokud tag neodpovídá formátu -
	// jeden špatně napsaný tag na itemu tak nezhroutí celý systém.
	public static bool TryParse(string rawTag, out string key, out float value)
	{
		key = null;
		value = 0f;

		if (string.IsNullOrWhiteSpace(rawTag))
			return false;

		string[] parts = rawTag.Split(':', 2);
		if (parts.Length != 2)
		{
			GD.PushWarning($"Item tag '{rawTag}' neni ve formatu 'klic:hodnota', ignoruji.");
			return false;
		}

		if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
		{
			GD.PushWarning($"Item tag '{rawTag}' ma neciselnou hodnotu, ignoruji.");
			return false;
		}

		key = parts[0].Trim().ToLowerInvariant();
		return true;
	}
}
