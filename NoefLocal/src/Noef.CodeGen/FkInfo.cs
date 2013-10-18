using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Noef.CodeGen
{
	public class FkInfo
	{
		public static readonly string FK_QUERY = @"
select
	fk.name fk_name,
	dep_obj.name as dependent_table,
	dep_col.name as dep_col,
	prin_obj.name principal_table,
	prin_col.name as principal_col,
	dep_col.is_nullable as dep_col_is_nullable,
	cast(COALESCE(dep_ixs.is_primary_key, 0) as bit) as dep_col_is_pk
from
	sys.foreign_key_columns fk_cols
	inner join sys.foreign_keys fk
	on fk_cols.constraint_object_id = fk.object_id
	
	-- principal table
	inner join sys.objects prin_obj
	on fk_cols.referenced_object_id = prin_obj.object_id

	inner join sys.columns prin_col
	on
		fk_cols.referenced_object_id = prin_col.object_id
		and fk_cols.referenced_column_id = prin_col.column_id
		
	-- dependent table
	inner join sys.objects dep_obj
	on fk_cols.parent_object_id = dep_obj.object_id
		
	inner join sys.columns dep_col
	on
		fk_cols.parent_object_id = dep_col.object_id
		and fk_cols.parent_column_id = dep_col.column_id
	
	-- join to index_columns where the FK is an index
	left join sys.index_columns dep_ix_cols
	on
		dep_obj.object_id = dep_ix_cols.object_id
		and fk_cols.parent_column_id = dep_ix_cols.index_column_id
	
	-- join to indexes to find out if the FK index is the PK
	left join sys.indexes dep_ixs
	on
		dep_obj.object_id = dep_ixs.object_id
		and dep_ix_cols.index_id = dep_ixs.index_id
where
	dep_obj.name IN({0})
	and prin_obj.name IN({0})
order by
	fk.name
";


		public string FkName { get; set; }
		public string DependentTable { get; set; }
		public string DependentColumn { get; set; }
		public string PrincipalTable { get; set; }
		public string PrincipalColumn { get; set; }

		/// <summary>
		/// Don't know if I'll use this info.
		/// </summary>
		public bool IsFkNullable { get; set; }

		/// <summary>
		/// If a FK is also the PK, then you have a 1:1 (or a 1:0-1), as opposed to a 1:*
		/// </summary>
		public bool IsFkPk { get; set; }


		private static FkInfo fromRow(object[] row)
		{
			FkInfo info = new FkInfo();
			info.FkName = (string)row[0];
			info.DependentTable = (string)row[1];
			info.DependentColumn = (string)row[2];
			info.PrincipalTable = (string)row[3];
			info.PrincipalColumn = (string)row[4];
			info.IsFkNullable = (bool)row[5];
			info.IsFkPk = (bool)row[6];
			return info;
		}

		private static IList<FkInfo> getListFromReader(SqlDataReader reader)
		{
			var rows = new List<object[]>();
			while (reader.Read())
			{
				object[] row = new object[reader.FieldCount];
				reader.GetValues(row);
				rows.Add(row);
			}

			var list = rows.Select(row => fromRow(row)).ToList();
			return list;
		}

		public static IList<FkInfo> GetAll(SqlConnection cn, IEnumerable<string> tables)
		{
			string in_list = String.Join(",", tables.Select(t => "'" + t + "'"));
			string sql = String.Format(FK_QUERY, in_list);
			SqlCommand cmd = new SqlCommand(sql, cn);
			SqlDataReader reader = cmd.ExecuteReader();
			var list = getListFromReader(reader);
			reader.Close();
			return list;
		}

	}
}
