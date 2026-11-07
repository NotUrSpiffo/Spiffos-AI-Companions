using System;
using System.Collections.Generic;

namespace Companions;

public sealed class MHR_List
{
	public readonly HashSet<string> Named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public readonly Dictionary<string, int> Amounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	public readonly List<string> Order = new List<string>();

	private static readonly Dictionary<string, MHR_List> s_cache = new Dictionary<string, MHR_List>();

	public bool All { get; private set; }

	public static MHR_List Get(string raw)
	{
		raw = raw ?? "";
		if (s_cache.TryGetValue(raw, out var value))
		{
			return value;
		}
		value = new MHR_List();
		string[] array = raw.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length == 0)
			{
				continue;
			}
			if (text == "*")
			{
				value.All = true;
				continue;
			}
			if (text.StartsWith("-"))
			{
				text = text.Substring(1).Trim();
				if (text.Length > 0)
				{
					value.Excluded.Add(text);
				}
				continue;
			}
			int num = text.IndexOf(':');
			if (num >= 0)
			{
				string s = text.Substring(num + 1).Trim();
				text = text.Substring(0, num).Trim();
				if (text.Length > 0 && int.TryParse(s, out var result))
				{
					value.Amounts[text] = Math.Max(0, result);
				}
			}
			if (text.Length != 0 && value.Named.Add(text))
			{
				value.Order.Add(text);
			}
		}
		if (s_cache.Count > 64)
		{
			s_cache.Clear();
		}
		s_cache[raw] = value;
		return value;
	}

	public bool Allows(params string[] names)
	{
		bool flag = false;
		foreach (string text in names)
		{
			if (!string.IsNullOrEmpty(text))
			{
				if (Excluded.Contains(text))
				{
					return false;
				}
				if (Named.Contains(text))
				{
					flag = true;
				}
			}
		}
		return All | flag;
	}

	public bool IsExcluded(params string[] names)
	{
		for (int i = 0; i < names.Length; i++)
		{
			if (!string.IsNullOrEmpty(names[i]) && Excluded.Contains(names[i]))
			{
				return true;
			}
		}
		return false;
	}

	public int AmountFor(string name, int dflt)
	{
		if (name == null || !Amounts.TryGetValue(name, out var value))
		{
			return dflt;
		}
		return value;
	}

	public bool HasAmount(string name)
	{
		if (name != null)
		{
			return Amounts.ContainsKey(name);
		}
		return false;
	}

	public static string Local(string token)
	{
		if (string.IsNullOrEmpty(token))
		{
			return null;
		}
		try
		{
			return (Localization.instance != null) ? Localization.instance.Localize(token) : null;
		}
		catch
		{
			return null;
		}
	}
}
