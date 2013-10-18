using System.ComponentModel;

namespace Noef
{
	/// <summary>
	/// The metadata for a column in SQL Server. The name of the column, the corresponding .NET type that most closely matches the SQL type,
	/// etc.  Also contains a PropertyDescriptor for the corresponding property in the class that represents the table that this column is in. Very handy.
	/// </summary>
	public class ColumnMetadata
	{
		/// <summary>
		/// The name of the column as it is in SQL server.
		/// For the name of the property in C#, use <see cref="Property" />.Name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The C# property that maps to the SQL column.
		/// For the name of the property, use <see cref="Property" />.Name.
		/// For the C#/CLR/.NET Type of the property, use <see cref="Property" />.Type (a System.Type property).
		/// For the SQL type (stored as a string), use <see cref="Property" />.SqlType (a string)
		/// </summary>
		public PropertyDescriptor Property { get; set; }

		/// <summary>
		/// This will only be populated for types that are created from NoefGen.exe - types that are actually mapped
		/// to SQL tables (not ones created on the fly for adhoc queries, etc).
		/// This will be the SQL type of the column, as a string.
		/// Examples:
		/// varchar(100)
		/// int
		/// decimal(19,4)
		/// etc
		/// Nullability will not be part of the name.  For that use <see cref="IsNullable" />
		/// </summary>
		public string SqlType { get; set; }

		/// <summary>
		/// This is similar to SqlType, but includes parens for MaxLength, Precision and Scale when necessary.
		/// So:
		///		"varchar(50)" instead of "varchar",
		///		"decimal(19,4)" instead of "decimal"
		/// If you need JUST the type name (no parens), use <see cref="SqlType" />.
		/// If you need the column name, use <see cref="Name" />.
		/// If you need the C# Property name or type, use <see cref="Property" />.Name and <see cref="Property" />.Type.
		/// </summary>
		public string SqlTypeDeclaration { get; set; }

		// TODO: Implement these!!
		public bool IsNullable { get; set; }
		public bool IsIdentity { get; set; }
		public bool IsPk { get; set; }
		public bool IsFk { get; set; }
		public int MaxLength { get; set; }

		public ColumnMetadata(PropertyDescriptor property)
		{
			Name = property.Name;
			Property = property;
		}

		public ColumnMetadata(string columnName, PropertyDescriptor property, bool isNullable, bool isIdentity, bool isPk, bool isFk, int maxLength, string sqlType, string sqlTypeDeclaration)
		{
			Name = columnName;
			Property = property;
			IsNullable = isNullable;
			IsIdentity = isIdentity;
			IsPk = isPk;
			IsFk = isFk;
			MaxLength = maxLength;
			SqlType = sqlType;
			SqlTypeDeclaration = sqlTypeDeclaration;
		}
	}
}
