using System.IO;
using System.Linq;
using System.Text;

namespace Noef.CodeGen.Generators
{
	public class MetadataGenerator : CodeGeneratorBase
	{
		public static readonly string CLASS_TEMPLATE = @"using System.Collections.Generic;
using System;
using System.Threading;
using <#= NoefNamespace #>;
using System.ComponentModel;
using <#= DtoNamespace #>;

// ReSharper disable CheckNamespace
namespace <#= MetadataNamespace #>
// ReSharper restore CheckNamespace
{
	[MetadataClass]
	public static class Metadata
	{
<#= PropertyDefs #>
		
		public static readonly object s_objSync = new object();
		public static bool s_isInit = false;
		public static IList<TableMetadata> _AllTables { get; private set; }

		public static void Initialize()
		{
			// dirty check (not thread safe)
			if (s_isInit)
				return;

			lock (s_objSync)
			{
				// thread safe check
				if (s_isInit)
					return;

				// Initialize the TableMetadata properties for each table/dto
<#= PropertyInits #>

				// The list of ALL tables
				_AllTables = new List<TableMetadata>()
				{
<#= PropertyList #>
				};

				// Add all of these tables to Noef's table metadata
				TableMetadata.AddTables(_AllTables);

				// Create hydrators for all mapped types
<#= HydratorDefs #>

				s_isInit = true;
			}
		}
	}
}

";

		// {0} is the table name (Which will be fully qualified if the connection string is not the default one)
		// {1} is the class name
		// {2} is the ColumnMetadata constructors for the List<T> initialization
		// {3} is the database name
		// {4} is the schema name
		// {5} is the connection string name
		private static readonly string INIT_FORMAT = @"
				// {0} => {1}
				PropertyDescriptorCollection {1}_props = TypeDescriptor.GetProperties(typeof({1}));
				{1} = new TableMetadata(typeof({1}), ""{0}"", ""{3}"", ""{4}"", {5}, new List<ColumnMetadata>()
				{{
{2}
				}});
";

		public static readonly string COLUMN_META_FORMAT = "\t\t\t\t\tnew ColumnMetadata(\"{0}\", {1}, {2}, {3}, {4}, {5}, {6}, \"{7}\", \"{8}\"),\r\n";

		public static readonly string HYDRATOR_FORMAT = @"
				TableMetadata.Hydrators<{0}>.Hydrator = (row, startingColumn) =>
				{{
					{0} obj = new {0}();
{1}
					return obj;
				}};
";

		public MetadataGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{
		}

		public override void Run()
		{
			StringBuilder sb = new StringBuilder(CLASS_TEMPLATE);
			sb.Replace("<#= NoefNamespace #>", Settings.NoefNamespace);
			sb.Replace("<#= DtoNamespace #>", Settings.DtoNamespace);
			sb.Replace("<#= MetadataNamespace #>", Settings.MetadataNamespace);
			sb.Replace("<#= PropertyDefs #>", getPropertyDefs());
			sb.Replace("<#= PropertyInits #>", getPropertyInits());
			sb.Replace("<#= PropertyList #>", getPropertyList());
			sb.Replace("<#= HydratorDefs #>", getHydratorDefs());
			Output.WriteLine(sb.ToString());
			Output.Flush();
		}

		private string getPropertyDefs()
		{
			StringBuilder sb = new StringBuilder();
			foreach (TableMapping table in Settings.TableMappings.Where(t => !t.IsEnum))
				sb.AppendLine("\t\tpublic static TableMetadata " + table.ClassName + " { get; private set; }");
			return sb.ToString();
		}

		private string getPropertyInits()
		{
			StringBuilder sb = new StringBuilder();
			foreach (TableMapping table in Settings.TableMappings.Where(t => !t.IsEnum))
			{
				StringBuilder sbColumns = new StringBuilder();
				foreach(Column col in table.Columns)
				{
					sbColumns.AppendFormat(COLUMN_META_FORMAT,
						col.ColumnName,
						table.ClassName + "_props[\"" + col.PropertyName + "\"]",
						col.IsNullable.ToString().ToLower(),
						col.IsIdentity.ToString().ToLower(),
						col.IsPrimaryKey.ToString().ToLower(),
						col.IsForeignKey.ToString().ToLower(),
						col.MaxLength.ToString(),
						col.Type,
						col.GetSqlDeclaration()
					);
				}
				sb.AppendFormat(INIT_FORMAT, table.TableName, table.ClassName, sbColumns, table.Database, table.Schema,
					table.ConnectionName == null ? "null" : "\"" + table.ConnectionName + "\""
					);
			}
			return sb.ToString();
		}

		private string getHydratorDefs()
		{
			StringBuilder sb = new StringBuilder();
			foreach (TableMapping table in Settings.TableMappings.Where(t => !t.IsEnum))
			{
				sb.AppendFormat(HYDRATOR_FORMAT, table.ClassName, getHydratorPropertyAssignments(table));
			}
			return sb.ToString();
		}


		private static string getHydratorPropertyAssignments(TableMapping table)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < table.Columns.Count; i++)
			{
				Column col = table.Columns[i];
				if (col.ClrTypeName.EndsWith("?"))
				{
					string nonNullableTypeName = col.ClrTypeName.Substring(0, col.ClrTypeName.Length - 1);
					sb.AppendFormat("\t\t\t\t\tobj.{0} = row[{1} + startingColumn] == DBNull.Value ? ({2}) null : ({3}) row[{1} + startingColumn];\r\n",
						col.PropertyName, i, col.ClrTypeName, nonNullableTypeName);
				}
				else
				{
					sb.AppendFormat("\t\t\t\t\tobj.{0} = ({1}) row[{2} + startingColumn]{3};\r\n",
						col.PropertyName, col.ClrTypeName, i, col.IsNullable ? ".NullOrValue()" : "");
				}
			}
			return sb.ToString();
		}

		private string getPropertyList()
		{
			StringBuilder sb = new StringBuilder();
			foreach (TableMapping table in Settings.TableMappings.Where(t => !t.IsEnum))
				sb.AppendLine("\t\t\t\t\t" + table.ClassName + ",");
			return sb.ToString();
		}

	}
}
