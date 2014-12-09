using System;
using System.Data;
using System.Linq;
#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public abstract partial class NoefDal
	{
	
		/// <summary>
		/// Constructs and executes a DELETE statement based on the table specified by T (using the column metadata).
		/// Will delete 1 row, based on a WHERE clause that uses TableMetadata.Pk.Name as the pk column.
		/// The value of the pkColumn is taken from "obj" (finding the property that corresponds to the PK column specified, and taking its value).
		/// Returns the number of affected (deleted) records.
		/// </summary>
		public int Delete<T>(T obj, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			string pkColumn = TableMetadata.For<T>().Pk.Name;
			return Delete(obj, pkColumn, cn, beforeExecute, tx, timeout);
		}


		/// <summary>
		/// Constructs and executes a DELETE statement based on the table specified by T (using the column metadata).
		/// Will delete 1 row, based on a WHERE clause that uses pkColumn as the column to restrict by (so you could potentially
		/// delete a range of values, if you used a column other than the PK.  Useful, but be careful).
		/// The value of the pkColumn is taken from "obj" (finding the property that corresponds to the PK column specified, and taking its value).
		/// Returns the number of affected (deleted) records.
		/// </summary>
		public int Delete<T>(T obj, string pkColumn, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = "DELETE FROM " + tmeta.Name + " WHERE " + pkColumn + " = @" + pkColumn;
			using (IDbCommand cmd = CreateCommand<T>(sql, null, cn, CommandType.Text, tx, timeout))
			{
				// Find the column the user indicated by name
				ColumnMetadata col = tmeta.Columns.First(c => String.Equals(c.Name, pkColumn, StringComparison.OrdinalIgnoreCase));
				object pkValue = col.Property.GetValue(obj);
				cmd.AddParam("@" + pkColumn, pkValue);
				if (beforeExecute != null)
					beforeExecute(cmd);
				return cmd.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Constructs and executes a DELETE statement based on the table specified by T (using the column metadata).
		/// Will delete rows based on a WHERE clause that uses keyColumn and key values specified by the caller.
		/// Returns the number of affected (deleted) records.
		/// </summary>
		public int Delete<T>(object key, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			string pkColumn = TableMetadata.For<T>().Pk.Name;
			return Delete<T>(pkColumn, key, cn, beforeExecute, tx, timeout);
		}


		/// <summary>
		/// Constructs and executes a DELETE statement based on the table specified by T (using the column metadata).
		/// Will delete rows based on a WHERE clause that uses keyColumn and key values specified by the caller.
		/// Returns the number of affected (deleted) records.
		/// </summary>
		public int Delete<T>(string keyColumn, object key, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = "DELETE FROM " + tmeta.Name + " WHERE " + keyColumn + " = @" + keyColumn;
			using (IDbCommand cmd = CreateCommand<T>(sql, null, cn, CommandType.Text, tx, timeout))
			{
				cmd.AddParam("@" + keyColumn, key);
				if (beforeExecute != null)
					beforeExecute(cmd);
				return cmd.ExecuteNonQuery();
			}
		}
	}
}
