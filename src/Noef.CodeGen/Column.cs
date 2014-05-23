using System.Linq;

namespace Noef.CodeGen
{
	/// <summary>
	/// See the Table.GetAll() method to see how Column objects are populated.
	/// </summary>
	public class Column
	{
		/// <summary>
		/// All of the type names in getClrPropertyTypeName() that are reference types
		/// </summary>
		private static readonly string[] REF_TYPES = {
			"object",
			"string",
			"byte[]",
			"Microsoft.SqlServer.Types.SqlGeometry",
			"Microsoft.SqlServer.Types.SqlGeography"
		};

		public string ColumnName { get; set; }
		public string PropertyName { get; set; }

		/// <summary>
		/// This is the name of the SQL Type, but without any parens even when it's required by that type.
		/// So values here look like "int", "datetime", "varchar", but not "varchar(50)".
		/// If you need the parens, use <see cref="GetSqlDeclaration" />.
		/// </summary>
		public string Type { get; set; }

		public int ID { get; set; }
		public short MaxLength { get; set; }
		public byte Precision { get; set; }
		public byte Scale { get; set; }
		public bool IsNullable { get; set; }
		public bool IsIdentity { get; set; }
		public bool IsComputed { get; set; }
		public bool IsPrimaryKey { get; set; }
		public bool IsForeignKey { get; set; }
		public bool IsPropertyExcluded { get; set; }

		/// <summary>
		/// If the user specifies a specific type in the config file, set this to true.
		/// </summary>
		public bool IsUserType { get; set; }

		/// <summary>
		/// Null if it's not a foreign key
		/// </summary>
		public string FkName { get; set; }

		public bool IsUnique { get; set; }

		public string ClrTypeName { get { return GetClrTypeName(); } }

		public string GetClrTypeName()
		{
			if (IsUserType)
				// The user specified a type to use in the config, and we'll just take it literally, whatever it is.
				return Type;

			string propertyType = SqlTypeMap.GetClrTypeName(Type);

			bool isValueType = !REF_TYPES.Contains(propertyType);

			// Don't add a ? to the end of nullable reference types (ref types are already nullable)
			if (isValueType && IsNullable)
				propertyType += "?";
			return propertyType;
		}

		public string GetSqlDeclaration()
		{
			bool needsParens = SqlTypeMap.SqlDeclRequiresParens(Type);
			if (needsParens)
			{
				if (ClrTypeName == "string")
					return Type + "(" + MaxLength + ")";
				return Type + "(" + Precision + "," + Scale + ")";
			}
			return Type;
		}
	}
}
