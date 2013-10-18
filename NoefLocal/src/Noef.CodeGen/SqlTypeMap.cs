namespace Noef.CodeGen
{
	public static class SqlTypeMap
	{
		public static string GetClrTypeName(string sqlType)
		{
			switch (sqlType.ToLower())
			{
				// geography types
				case "hierarchyid": return "Microsoft.SqlServer.Types.SqlHierarchyId";
				case "geometry": return "Microsoft.SqlServer.Types.SqlGeometry";
				case "geography": return "Microsoft.SqlServer.Types.SqlGeography";

				// bool types
				case "bit": return "bool";

				// date types
				case "datetimeoffset": return "DateTimeOffset";
				case "time": return "TimeSpan";

				case "date":
				case "datetime2":
				case "smalldatetime":
				case "datetime":
					return "DateTime";

				// integer types
				case "tinyint": return "byte";
				case "smallint": return "short";
				case "int": return "int";
				case "bigint": return "long";

				// floating point number types
				case "real": return "Single";
				case "float": return "double";

				// decimal types
				case "money":
				case "decimal":
				case "numeric":
				case "smallmoney":
					return "decimal";

				// byte[] (binary) types
				case "image":
				case "varbinary":
				case "binary":
				case "timestamp":
					return "byte[]";

				// c# string types
				case "text":
				case "ntext":
				case "varchar":
				case "char":
				case "nvarchar":
				case "nchar":
				case "xml":
				case "sysname":
					return "string";

				// other types
				case "uniqueidentifier": return "Guid";
				case "sql_variant": return "object";
				default: return "object";
			}
		}

		public static bool SqlDeclRequiresParens(string sqlType)
		{
			switch (sqlType.ToLower())
			{
				// decimal types
				case "money":
				case "decimal":
				case "numeric":
				case "smallmoney":
					return true;

				// byte[] (binary) types
				case "varbinary":
				case "binary":
					return true;

				// c# string types
				case "text":
				case "ntext":
				case "varchar":
				case "char":
				case "nvarchar":
				case "nchar":
				case "xml":
				case "sysname":
					return true;

				default: return false;
			}
		}
	}
}
