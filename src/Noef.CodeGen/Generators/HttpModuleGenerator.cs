using System.IO;
using System.Text;

namespace Noef.CodeGen.Generators
{
	public class HttpModuleGenerator : CodeGeneratorBase
	{
		public static readonly string DAL_TEMPLATE = @"using System;
using System.Web;
using System.ComponentModel;
using <#= NoefNamespace #>;

/*
	This IHttpModule can be installed in your ASP.NET app's web.config like this:

IIS6:
	TODO: example

IIS7:
	<system.webServer>
		<validation validateIntegratedModeConfiguration=""false"" />
		<modules runAllManagedModulesForAllRequests=""true"">
			<add name=""<#= DalClassName #>HttpModule"" type=""<#= DalNamespace #>.<#= DalClassName #>HttpModule"" />
		</modules>
	</system.webServer>

*/


// ReSharper disable CheckNamespace
namespace <#= DalNamespace #>
// ReSharper restore CheckNamespace
{
	/// <summary>
	/// An IHttpModule implementation that can be used to set up your DAL for use in an ASP.NET app.
	/// This can be configured in your app's web.config file, like any other IHttpModule.
	/// </summary>
	[Description(""NoefDalHttpModule"")]
	public partial class <#= DalClassName #>HttpModule : IHttpModule
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

";
		
		public HttpModuleGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{ }

		public override void Run()
		{
			StringBuilder sb = new StringBuilder(DAL_TEMPLATE);
			sb
				.Replace("<#= NoefNamespace #>", Settings.NoefNamespace)
				.Replace("<#= DalNamespace #>", Settings.DalNamespace)
				.Replace("<#= DalClassName #>", Settings.DalClassName)
				;
			Output.WriteLine(sb.ToString());
			Output.Flush();
		}

	}
}
