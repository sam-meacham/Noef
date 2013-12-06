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
	/// Generated DAL class (inherits from <#= BaseClassName #>).
	/// </summary>
	public partial class <#= DalClassName #> : <#= BaseClassName #>
	{
		// Required overrides
		public override string ConnectionStringName
		{
			get { return ""<#= CnStringName #>""; }
		}

		public override NoefDbType DbType
		{
			get { return NoefDbType.SqlServer; }
		}

		// NOTE: This file was auto generated, and can/will be overwritten.
		// The <#= DalClassName #> class is partial. If you want to augment it, do it in a different file (YourDal.cs, etc)

		// You can optionally override GetConnectionString() or GetConnection() (which calls GetConnectionString(), which uses the ConnectionStringName property),
		// if you want custom behavior for getting the default connection string or connection.
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
				.Replace("<#= DalClassName #>", Settings.DalClassName)
				.Replace("<#= BaseClassName #>", Settings.DalBaseClassName)
				.Replace("<#= CnStringName #>", cnStringName)
				;
			Output.WriteLine(sb.ToString());
			Output.Flush();
		}

	}
}
