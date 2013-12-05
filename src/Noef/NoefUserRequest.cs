using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Web;

namespace Noef
{
	/// <summary>
	/// Contains info about the current request when Noef is being used
	/// in a web environment. The NoefDal has an instance of this class type,
	/// and this class can be easily subclassed to add additional per-request fields.
	/// </summary>
	public class NoefUserRequest
	{
		// we'll consider it localhost if the entire word is found with word boundaries in the hostname.
		private static readonly Regex RX_LOCALHOST = new Regex(@"\blocalhost\b");

		// The IHttpModule will also populate these (as best as it can)
		public IPrincipal PrincipalUser { get; set; }
		public string Username { get; set; }

		public string IPAddress { get; set; }
		public string AppRoot { get; set; }
		public string Hostname { get; set; }
		public string HostRoot { get; set; }
		public bool IsAdmin { get; set; }
		public bool IsLocalhost { get; set; }

		/// <summary>
		/// Comma separated list of usernames in web.config appSettings under "NoefAdmins" (convention, baby)
		/// </summary>
		public string[] NoefAdmins { get; private set; }

		public NoefUserRequest(NoefDal dal)
		{
			HttpContext ctx = HttpContext.Current;
			HttpApplication app = ctx == null ? null : ctx.ApplicationInstance;
			IPAddress = GetIPAddress();

			string adminsList = ConfigurationManager.AppSettings["NoefAdmins"] ?? "";
			NoefAdmins = adminsList
				.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries)
				.Select(a => a.ToLower())
				.ToArray();

			if (app != null)
			{
				var r = app.Request;
				Hostname = r.Url.Host;
				HostRoot = r.Url.Scheme + Uri.SchemeDelimiter + r.Url.Host +
					(r.Url.IsDefaultPort ? "" : ":" + r.Url.Port);

				// AppRoot will always end with a trailing /
				AppRoot = app.Context.Request.ApplicationPath;
				Debug.Assert(AppRoot != null);
				if (!AppRoot.EndsWith("/"))
					AppRoot += "/";

				IsLocalhost = RX_LOCALHOST.IsMatch(Hostname.ToLower());

				if (app.User != null)
				{
					PrincipalUser = app.User;
					Username = app.User.Identity.Name;
				}

				// Roles are fetched from the DB at login, and stored in the user's auth cookie (encrypted)
				// SentryMembershipProvider is smart about how it accesses the values, but a user will have to
				// log out and back in if their roles change.

				IsAdmin = NoefAdmins.Contains(Username, StringComparer.OrdinalIgnoreCase)
					|| dal.IsCurrentUserAdmin();
			}
		}

		public string GetAbsUrlRoot()
		{
			return HostRoot + AppRoot;
		}

		public static string GetIPAddress()
		{
			if (HttpContext.Current == null)
				return null;

			// Look for a proxy address first
			string ip = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

			// If there is no proxy, get the standard remote address
			if (ip == null)
			{
				ip = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
			}
			else
			{
				if ((string.IsNullOrEmpty(ip) || ip.ToLower() == "unknown"))
				{
					ip = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
				}
				else
				{
					ip = "from proxy  " + ip;
				}
			}
			return ip;
		}

	}
}
