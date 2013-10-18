using System.Runtime.Remoting.Messaging;
using System.Web;

namespace Noef
{
	/// <summary>
	/// HttpContext.Current.Items is the perfect place to store items that you want to be unique per user (each
	/// user of the ASP.NET app has their own copy of the storage), and that you also want to be limited to the
	/// lifetime of the current request.
	///
	/// This storage class will utilize HttpContext.Current.Items if it's available.  If it's not (console app, unit tests, etc),
	/// it falls back on CallContext for storage.
	/// </summary>
	public static class PerRequestStore
	{
		public static object GetData(string key)
		{
			if (HttpContext.Current != null)
				return HttpContext.Current.Items[key];
			return CallContext.GetData(key);
		}


		public static void SetData(string key, object data)
		{
			if (HttpContext.Current != null)
				HttpContext.Current.Items[key] = data;
			else
				CallContext.SetData(key, data);
		}

	}
}
