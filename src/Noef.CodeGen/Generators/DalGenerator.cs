using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace Noef.CodeGen.Generators
{
	public class DalGenerator : CodeGeneratorBase
	{
		public static readonly string DAL_TEMPLATE = @"using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using <#= NoefNamespace #>;
using <#= DtoNamespace #>;


// ReSharper disable CheckNamespace
namespace <#= DalNamespace #>
// ReSharper restore CheckNamespace
{
	/// <summary>
	/// The dal implementation class (inherits from NoefDal).
	/// If you want to access the <#= ClassName #> singleton instance, use <#= ClassName #>.Instance.
	/// </summary>
	public partial class <#= ClassName #>Impl : <#= BaseClassName #>
	{
		// Required overrides
		public override string ConnectionStringName
		{
			get { return ""<#= CnStringName #>""; }
		}

		public override DbType DbType
		{
			get { return DbType.SqlServer; }
		}

		// NOTE: This file was auto generated, and can be overwritten. The <#= ClassName #>Impl class is partial. If you want to add anything to it, do it in a different file with a partial class.

		// You can optionally override GetConnectionString() or GetConnection() (which calls GetConnectionString(), which uses the ConnectionStringName property),
		// if you want custom behavior for getting the default connection string or connection.

		// Add any additional DAL methods in a separate partial class file. You can make them static if you want, in which case you'd call them like: <#= ClassName #>Impl.StaticMethodName().
		// If they are non-static, you call them like: <#= ClassName #>.Instance.MethodName() (preferred).
	}


	/// <summary>
	/// The singleton instance of <#= ClassName #>Impl. Use <#= ClassName #>.Instance.
	/// Rarely should code be added to this class.  Add it to the <#= ClassName #>Impl class instead, and access the singleton instance of <#= ClassName #>Impl via <#= ClassName #>.Instance.
	/// Note that that covers ASP.NET scenarios (the singleton instance is ""per request""). If you have some other application, you can just create a new instance of <#= ClassName #>Impl
	/// and use it however you'd like.
	/// </summary>
	public partial class <#= ClassName #> : PerRequestSingleton<<#= ClassName #>Impl>
	{
		private static string s_versionString;
		public static string VersionString
		{
			get
			{
				if (s_versionString != null)
					return s_versionString;
				Assembly asm = Assembly.GetExecutingAssembly();
				FileVersionInfo info = FileVersionInfo.GetVersionInfo(asm.Location);
				s_versionString = info.ProductVersion;
				return s_versionString;	
			}
		}
	}
}

";

		public static readonly string GET_METHOD_TEMPLATE = @"
		public <#= DtoType #> Get<#= DtoType #>ByKey(<#= PkType #> key)
		{
			string sql = ""SELECT "" + TableMetadata.GetColumnsString<<#= DtoType #>>() + "" FROM <#= TableName #> WHERE <#= PkColumn #> = @key"";
			<#= DtoType #> obj = SingleOrDefault<<#= DtoType #>>(sql, new { key });
			return obj;
		}
";

		public DalGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{ } 

		public override void Run()
		{
			string cnStringName = Settings.DalConnectionName;
			if (cnStringName == null)
			{
				// Get the name of the database from the connection string
				SqlConnection cn = new SqlConnection(Settings.GetDalConnectionString());
				cnStringName = cn.Database;
			}

			StringBuilder sb = new StringBuilder(DAL_TEMPLATE);
			sb
				.Replace("<#= NoefNamespace #>", Settings.NoefNamespace)
				.Replace("<#= DtoNamespace #>", Settings.DtoNamespace)
				.Replace("<#= DalNamespace #>", Settings.DalNamespace)
				.Replace("<#= ClassName #>", Settings.DalClassName)
				.Replace("<#= BaseClassName #>", Settings.DalBaseClassName)
				.Replace("<#= CnStringName #>", cnStringName)
				;
			Output.WriteLine(sb.ToString());
			Output.Flush();
		}

	}
}
