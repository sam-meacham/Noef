using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;

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

		public string AppRoot { get; set; }
		public string Hostname { get; set; }
		public string HostRoot { get; set; }

		public HttpContext Context { get; private set; }
		public HttpApplication App { get; private set; }
		public HttpRequest Request { get; private set; }
		public HttpResponse Response { get; private set; }

		/// <summary>
		/// Comma separated list of usernames in web.config appSettings under "NoefAdmins" (convention, baby)
		/// </summary>
		public string[] NoefAdmins { get; private set; }

		public NoefUserRequest()
		{
			Context = HttpContext.Current;
			if (Context != null)
			{
				Request = Context.Request;
				Response = Context.Response;
				App = Context.ApplicationInstance;
			}

			NoefAdmins = Cfg("NoefAdmins")
				.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries)
				.Select(a => a.ToLower())
				.ToArray();

			if (App != null)
			{
				var r = App.Request;
				Hostname = r.Url.Host;
				HostRoot = r.Url.Scheme + Uri.SchemeDelimiter + r.Url.Host +
					(r.Url.IsDefaultPort ? "" : ":" + r.Url.Port);

				// AppRoot will always end with a trailing /
				AppRoot = App.Context.Request.ApplicationPath;
				Debug.Assert(AppRoot != null);
				if (!AppRoot.EndsWith("/"))
					AppRoot += "/";

				if (App.User != null)
				{
					PrincipalUser = App.User;
					Username = App.User.Identity.Name;
				}
			}
		}

		public string Cfg(string appSettingKey)
		{
			return ConfigurationManager.AppSettings[appSettingKey] ?? "";
		}

		public virtual bool IsAdmin()
		{
			return NoefAdmins.Contains(Username, StringComparer.OrdinalIgnoreCase);
		}

		public bool IsLocalhost()
		{
			return RX_LOCALHOST.IsMatch(Hostname.ToLower());
		}

		public string GetAbsUrlRoot()
		{
			return HostRoot + AppRoot;
		}

		/// <summary>
		/// Checks to see if the current request can skip authorization, either because context.SkipAuthorization is true,
		/// or because UrlAuthorizationModule.CheckUrlAccessForPrincipal() returns true for the current request/user/url.
		/// </summary>
		/// <returns></returns>
		public bool SkipUrlAuth()
		{
			HttpContext context = HttpContext.Current;
			string path = context.Request.AppRelativeCurrentExecutionFilePath;
			Debug.Assert(path != null);
			return context.SkipAuthorization || UrlAuthorizationModule.CheckUrlAccessForPrincipal(path, context.User, context.Request.RequestType);
		}

		public string GetIPAddress()
		{
			if (Context == null)
				return null;

			// Look for a proxy address first
			string ip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

			// If there is no proxy, get the standard remote address
			if (ip == null)
			{
				ip = Request.ServerVariables["REMOTE_ADDR"];
			}
			else
			{
				if ((string.IsNullOrEmpty(ip) || ip.ToLower() == "unknown"))
				{
					ip = Request.ServerVariables["REMOTE_ADDR"];
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
