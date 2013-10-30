using System;
#if !NET2
using System.Collections.Concurrent;
#endif
using System.Collections.Generic;
using System.Linq;


namespace Noef
{
	/// <summary>
	/// This is a static cache, so if it's used in ASP.NET, all data is shared in the single w3wp.exe process, and is shared by all users.
	/// Initialized once, when the app starts.
	/// Will be reset if the app pool is reinitialized.
	/// IIS *CAN* start additional w3wp.exe processes for the same app (google "IIS thread agility"), to handle high volume, so be aware of that.
	/// </summary>
	public static class StaticStore
	{
#if NET2
		private static readonly Dictionary<string, object> s_cache = new Dictionary<string, object>();
		private static readonly Dictionary<string, DateTime> s_cacheExpirations = new Dictionary<string, DateTime>();
#else
		private static readonly ConcurrentDictionary<string, object> s_cache = new ConcurrentDictionary<string, object>();
		private static readonly ConcurrentDictionary<string, DateTime> s_cacheExpirations = new ConcurrentDictionary<string, DateTime>();
#endif

		public static string[] AllKeys()
		{
			return s_cache.Keys.ToArray();
		}

		public static object GetData(string key)
		{
			if (!s_cache.ContainsKey(key))
				return null;

			if (DateTime.Now > s_cacheExpirations[key])
			{
				ClearData(key);
				return null;
			}
			return s_cache[key];
		}

		public static DateTime GetExpiration(string key)
		{
			if (!s_cacheExpirations.ContainsKey(key))
				return DateTime.MinValue;
			return s_cacheExpirations[key];
		}

		public static void SetData(string key, object data)
		{
			SetData(key, data, DateTime.MaxValue);
		}

		public static void SetData(string key, object data, DateTime expiration)
		{
			if (data != null)
			{
				s_cache[key] = data;
				s_cacheExpirations[key] = expiration;
			}
			else
			{
				ClearData(key);
			}
		}

		public static void ClearData(string key)
		{
			if (!s_cache.ContainsKey(key))
				// nothing to do
				return;

			object throwAwayValue;
#if NET2
			if (!s_cache.Remove(key))
#else
			if (!s_cache.TryRemove(key, out throwAwayValue))
#endif
			{
				// For some reason it couldn't remove it. So just set the value for that key to null, which will have the same effect (but the key is still annoyingly present - not the end of the world)
				s_cache[key] = null;
			}

			if (s_cacheExpirations.ContainsKey(key))
			{
				DateTime throwAwayDate;
#if NET2
				if (!s_cacheExpirations.Remove(key))
#else
				if (!s_cacheExpirations.TryRemove(key, out throwAwayDate))
#endif
				{
					// For some reason it couldn't remove it. So just set the value for that key to DateTime.Min, which will always report as expired.
					s_cacheExpirations[key] = DateTime.MinValue;
				}
			}
		}

	}
}
