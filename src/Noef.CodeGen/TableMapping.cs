using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Noef.CodeGen
{
	public class TableMapping
	{
		// This query provides all kinds of nice table and column metadata based on the sql server system tables.
		// in the WHERE clause, you can see that results are limited to tables that the user is actually going to import.
		// TODO: If more metadata is ever needed, the Column class can have more properties added, and this query can be adjusted to retrieve that data from
		// the system tables.  One option that comes to mind is for FKs; it would be possible to gather metadata on WHAT the fk references (the referenced type), etc.
		// TODO: this query only works for sql server. I need to find out if this same metadata can be easily retrieved from other db systems that I'd like to support
		// (sql ce, mysql, oracle, etc)
		private static readonly string QUERY = @"
select
	t.name table_name,
	c.name column_name,
	typ.name type_name,
	c.column_id,
	c.max_length,
	c.precision,
	c.scale,
	c.is_nullable,
	c.is_identity,
	c.is_computed,
	coalesce(
	(
		select top 1
			ixs.is_primary_key
		from
			sys.indexes ixs
			inner join sys.index_columns ic
			on
				ixs.object_id = ic.object_id
				and ixs.index_id = ic.index_id
		where
			ixs.object_id = t.object_id
			and ic.column_id = c.column_id
	), cast(0 as bit)) as is_primary_key,
	coalesce(
	(
		select top 1
			ixs.is_unique
		from
			sys.indexes ixs
			inner join sys.index_columns ic
			on
				ixs.object_id = ic.object_id
				and ixs.index_id = ic.index_id
		where
			ixs.object_id = t.object_id
			and ic.column_id = c.column_id
	), cast(0 as bit)) as is_unique,
	(
		select 
			fk.name
		from
			sys.foreign_key_columns fkcols
			inner join sys.foreign_keys fk
			on fkcols.constraint_object_id = fk.object_id
		where
			fkcols.parent_object_id = t.object_id
			and fkcols.parent_column_id = c.column_id
	) as fk_name
from
	(
		select name, object_id from sys.tables where type = 'u'
		union
		select name, object_id from sys.views where type = 'v'
		union
		-- table valued inline functions
		select name, object_id from sys.all_objects where type = 'if'
	) t
	inner join sys.columns c
	on t.object_id = c.object_id
	
	inner join sys.types typ
	on
		c.system_type_id = typ.system_type_id
		and c.user_type_id = typ.user_type_id
where
	t.name = '{0}'
	and typ.name <> 'sysname'
order by
	t.name,
	c.column_id
";

		/// <summary>
		/// The name of the table in the database.
		/// If a table does not use the same connection as the DAL, this will be always fully qualified (DBName.Schema.TableName)
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// Sometimes the TableName is fully qualified.  This will ALWAYS be JUST the table name (no db or schema prefix).
		/// </summary>
		public string TableNameOnly { get; set; }

		/// <summary>
		/// The name of the DTO class that will be used to represent the table in the DAL.
		/// </summary>
		public string ClassName { get; set; }

		/// <summary>
		/// The name of a base class that the DTO will inherit from
		/// </summary>
		public string BaseClass { get; set; }

		/// <summary>
		/// The name of the connection string to use for this table.  Will default to the default one the DAL uses.
		/// </summary>
		public string ConnectionName { get; set; }

		public string Database { get; set; }

		/// <summary>
		/// Will default to "dbo", but can be set in the noef-config in the {import} tag for each table.
		/// </summary>
		public string Schema { get; set; }

		/// <summary>
		/// The columns in this table.
		/// </summary>
		public IList<Column> Columns { get; set; }

		/// <summary>
		/// Whether or not this table is going to be imported as an enum.
		/// For now, tables mapped to enums do NOT have DTO types created for them.
		/// </summary>
		public bool IsEnum { get; set; }

		/// <summary>
		/// The name of the column in the table that will be used for the enum int value
		/// </summary>
		public string EnumKey { get; set; }

		/// <summary>
		/// The name of the column in the table that will be used for the enum field name
		/// </summary>
		public string EnumLabel { get; set; }

		/// <summary>
		/// For tables that are to be imported as enums, this will hold all of the EnumKey values,
		/// which will be the field values in the enum
		/// </summary>
		public object[] EnumKeys { get; set; }

		/// <summary>
		/// For tables that are to be imported as enums, this will hold all of the EnumLabel values,
		/// which will be the field names in the enum
		/// </summary>
		public string[] EnumLabels { get; set; }

		/// <summary>
		/// Columns that are not to be imported into TableMetadata, ColumnMetadata, the DTOs, etc.
		/// The Noef DAL will have no knowledge of these columns (and don't ever use * in your SQL queries!).
		/// </summary>
		public string[] ExcludedColumns { get; set; }

		/// <summary>
		/// Columns that WILL be imported into TableMetadata, ColumnMetadata, the hydrators, but will NOT be included in the DTO generation.
		/// It's up to you to create the property definition in your own DTO partial classes!
		/// This is so you can put custom logic in the getters/setters if need be.
		/// </summary>
		public string[] ExcludedProperties { get; set; }


		public IList<Relationship> AsPrincipal { get; set; }
		public IList<Relationship> AsDependent { get; set; }

		/// <summary>
		/// Constructor for a table to DTO mapping
		/// </summary>
		public TableMapping(string tableName, string database, string schema, string className, string baseClass, string[] excludedColumns, string[] exludedProperties)
		{
			TableName = tableName;
			TableNameOnly = getPlainTableName(tableName);
			Database = database;
			Schema = schema;
			ClassName = className;
			BaseClass = baseClass;
			Columns = new List<Column>();
			IsEnum = false;
			ExcludedColumns = excludedColumns;
			ExcludedProperties = exludedProperties;
			AsPrincipal = new List<Relationship>();
			AsDependent = new List<Relationship>();
		}


		/// <summary>
		/// Constructor for a table to Enum mapping
		/// </summary>
		public TableMapping(string tableName, string database, string schema, string className, string enumKey, string enumLabel)
		{
			TableName = tableName;
			TableNameOnly = getPlainTableName(tableName);
			Database = database;
			Schema = schema;
			ClassName = className;
			Columns = new List<Column>();
			IsEnum = true;
			EnumKey = enumKey;
			EnumLabel = enumLabel;
			AsPrincipal = new List<Relationship>();
			AsDependent = new List<Relationship>();
			ExcludedColumns = new string[0];
			ExcludedProperties = new string[0];
		}

		private static string getPlainTableName(string tableName)
		{
			int ixDot = tableName.LastIndexOf(".", StringComparison.Ordinal);
			if (ixDot > -1)
				return tableName.Substring(ixDot + 1);
			return tableName;
		}

		public static void PopulateColumns(SqlConnection cn, TableMapping mapping) 
		{
			string sql = String.Format(QUERY, mapping.TableNameOnly);
			SqlCommand cmd = new SqlCommand(sql, cn);
			SqlDataReader reader = cmd.ExecuteReader();

			// Iterate over all of the sql columns in the table (metadata provided by the sql system tables)
			while(reader.Read())
			{
				string columnName = (string) reader["column_name"];

				// Skip excluded columns
				if (mapping.ExcludedColumns.Contains(columnName, StringComparer.InvariantCultureIgnoreCase))
					continue;

				// Build up the current column based on the metadata returned by sql server
				Column col = new Column();
				col.ColumnName = columnName;
				// TODO: based on mapping info, the property name might be different than the column name. This is not currently supported in the noef-config.xsd.
				col.PropertyName = col.ColumnName;
				col.Type = (string) reader["type_name"];
				col.ID = (int) reader["column_id"];
				col.MaxLength = (short) reader["max_length"];
				col.Precision = (byte) reader["precision"];
				col.Scale = (byte) reader["scale"];
				col.IsNullable = (bool) reader["is_nullable"];
				col.IsIdentity = (bool) reader["is_identity"];
				col.IsComputed = (bool) reader["is_computed"];
				col.IsPrimaryKey = (bool) reader["is_primary_key"];

				if (reader["fk_name"] == DBNull.Value)
					col.FkName = null;
				else
					col.FkName = (string) reader["fk_name"];

				col.IsForeignKey = col.FkName != null;
				col.IsUnique = (bool)reader["is_unique"];
				col.IsPropertyExcluded = mapping.ExcludedProperties.Contains(col.ColumnName, StringComparer.InvariantCultureIgnoreCase);

				mapping.Columns.Add(col);
			}
			reader.Close();

			if (mapping.IsEnum)
			{
				string enumSql = "SELECT " + mapping.EnumKey + ", " + mapping.EnumLabel + " FROM " + mapping.TableName + " ORDER BY " + mapping.EnumLabel;
				SqlCommand enumCmd = new SqlCommand(enumSql, cn);
				SqlDataReader enumReader = enumCmd.ExecuteReader();
				List<object> keys = new List<object>();
				List<string> labels = new List<string>();
				while(enumReader.Read())
				{
					keys.Add(enumReader[0]);
					labels.Add(enumReader.GetString(1));
				}
				enumReader.Close();
				mapping.EnumKeys = keys.ToArray();
				mapping.EnumLabels = labels.ToArray();
			}
		}


	}
}
