namespace BizHawk.Common
{
	public static class Locks //added as psuedo-global as there was no other way to get the scope to the http event where it is needed
	{
		public static readonly object LuaLock = new object();
	}
}
