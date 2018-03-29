using System;
namespace Fiddler
{
	[Flags]
	public enum HotkeyModifiers
	{
		Alt = 1,
		Control = 2,
		Shift = 4,
		Windows = 8
	}
}
