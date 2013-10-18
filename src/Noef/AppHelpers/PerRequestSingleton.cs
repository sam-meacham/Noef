using System;

namespace Noef
{
	/// <summary>
	/// A singleton whose storage is based on HttpContext.Current.Items, meaning the instance is unique
	/// per user, per request (and is discarded once the response is sent, since the current HttpContext goes away)
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class PerRequestSingleton<T>
	{
		private static readonly object s_objSync = new object();

		public static T Instance
		{
			get
			{
				string key = typeof(T).FullName;
				// First check
				if (PerRequestStore.GetData(key) == null)
				{
					// lock
					lock (s_objSync)
					{
						// second check
						if (PerRequestStore.GetData(key) == null)
						{
							T instance = (T)Activator.CreateInstance(typeof(T), true);
							PerRequestStore.SetData(key, instance);
						}
					}
				}

				return (T) PerRequestStore.GetData(key);
			}
		}

	}
}
