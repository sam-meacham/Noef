/*
 * This will be used when I get rid of singletons entirely.
 * 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace Noef
{
	/// <summary>
	/// An IHttpModule implementation that can be used to set up your DAL for use in an ASP.NET app.
	/// This can be configured in your app's web.config file, like any other IHttpModule.
	/// </summary>
	public class NoefHttpModule : IHttpModule
	{
		public void Dispose()
		{
			// calls CloseConnections()
			<#= DalClassName #>.Instance.Dispose();
		}

		public void Init(HttpApplication app)
		{
			app.AuthorizeRequest += onAuthorizeRequest;
			app.EndRequest += onEndRequest;

			<#= DalClassName #>.Instance.Init(app);
		}

		/// <summary>
		/// DO NOT put any code in here that will cause database access.  This is called for EVERY. SINGLE. REQUEST.
		/// Even for static resources (images, js files, css, etc). All this should do is analyze the request url and cookie values,
		/// and set the appropriate values in the MosDal.Instance.
		/// </summary>
		private static void onAuthorizeRequest(object sender, EventArgs eventArgs)
		{
			HttpApplication application = (HttpApplication)sender;
			<#= DalClassName #>.Instance.AuthorizeRequest(application);
		}

		private static void onEndRequest(object sender, EventArgs eventArgs)
		{
			HttpApplication application = (HttpApplication)sender;
			<#= DalClassName #>.Instance.EndRequest(application);
		}
	}
}
*/
