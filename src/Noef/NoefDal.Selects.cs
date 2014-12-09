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
		/// Constructs and executes a SELECT statement based on the table, name of the PK column (taken from the TableMetadata entry), and the PK value provided.
		/// Returns a single result: the matching record, or null if not found.
		/// </summary>
		public T Select<T>(object pkValue, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			string pkColumn = TableMetadata.For<T>().Pks[0].Name;
			return Select<T>(pkColumn, pkValue, cn, beforeExecute, tx, timeout);
		}

		/// <summary>
		/// Constructs and executes a SELECT statement based on the table, name of the PK column (taken from the TableMetadata entry), and the PK value provided.
		/// Returns a single result: the matching record, or null if not found.
		/// </summary>
		public T Select<T>(object[] pkValues, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			string[] ands = tmeta.Pks.Select(pk => "(" + pk.Name + " = @" + pk.Name + ")").ToArray();
			string where = String.Join(" AND ", ands);

			string sql = String.Format("SELECT {0} FROM {1} WHERE {2}", TableMetadata.GetColumnsString<T>(), tmeta.Name, where);
			using (IDbCommand cmd = CreateCommand<T>(sql, null, cn, CommandType.Text, tx, timeout))
			{
				for (int i = 0; i < tmeta.Pks.Length; i++)
					cmd.AddParam("@" + tmeta.Pks[i].Name, pkValues[i]);
				if (beforeExecute != null)
					beforeExecute(cmd);
				using (IDataReader reader = cmd.ExecuteReader())
				{
					T obj = reader.Hydrate<T>();
					reader.Close();
					return obj;
				}
			}
		}


		/// <summary>
		/// Constructs and executes a SELECT statement based on the table, name of the PK column, and the PK value provided.
		/// Returns a single result: the matching record, or null if not found.
		/// </summary>
		public T Select<T>(string pkColumn, object pkValue, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = String.Format("SELECT {0} FROM {1} WHERE {2} = @{2}", TableMetadata.GetColumnsString<T>(), tmeta.Name, pkColumn);
			using (IDbCommand cmd = CreateCommand<T>(sql, null, cn, CommandType.Text, tx, timeout))
			{
				cmd.AddParam("@" + pkColumn, pkValue);
				if (beforeExecute != null)
					beforeExecute(cmd);
				using (IDataReader reader = cmd.ExecuteReader())
				{
					T obj = reader.Hydrate<T>();
					reader.Close();
					return obj;
				}
			}
		}

	}
}
