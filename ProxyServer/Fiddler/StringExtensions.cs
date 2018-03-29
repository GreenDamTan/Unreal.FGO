using System;
namespace Fiddler
{
	public static class StringExtensions
	{
		public static bool OICContains(this string inStr, string toMatch)
		{
			return inStr.IndexOf(toMatch, StringComparison.OrdinalIgnoreCase) > -1;
		}
		public static bool OICEquals(this string inStr, string toMatch)
		{
			return inStr.Equals(toMatch, StringComparison.OrdinalIgnoreCase);
		}
		public static bool OICStartsWith(this string inStr, string toMatch)
		{
			return inStr.StartsWith(toMatch, StringComparison.OrdinalIgnoreCase);
		}
		public static bool OICStartsWithAny(this string inStr, params string[] toMatch)
		{
			for (int i = 0; i < toMatch.Length; i++)
			{
				if (inStr.StartsWith(toMatch[i], StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}
		public static bool OICEndsWithAny(this string inStr, params string[] toMatch)
		{
			for (int i = 0; i < toMatch.Length; i++)
			{
				if (inStr.EndsWith(toMatch[i], StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}
		public static bool OICEndsWith(this string inStr, string toMatch)
		{
			return inStr.EndsWith(toMatch, StringComparison.OrdinalIgnoreCase);
		}
	}
}
