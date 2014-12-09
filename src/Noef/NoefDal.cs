using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public abstract partial class NoefDal : IDisposable
	{
		private string m_versionString;

		public abstract string ConnectionStringName { get; }
		public abstract NoefDbType DbType { get; }
		private readonly string m_uniqueDalKey;
		public int DefaultTimeout { get; set; }

		/// <summary>
		/// Can be assigned a transaction that will be used for ALL noef based queries.
		/// Make SURE you assign this to null in the finally clause of your try/catch/finally!
		/// </summary>
		public IDbTransaction DefaultTransaction { get; set; }

		public ConcurrentDictionary<string, IDbConnection> OpenedConnections { get; set; } 
		//public IList<IDbConnection> OpenedConnections { get; private set; } 

		protected NoefDal()
		{
			m_uniqueDalKey = GetType().FullName;
			//OpenedConnections = new List<IDbConnection>();
			OpenedConnections = new ConcurrentDictionary<string, IDbConnection>();
			DefaultTimeout = 30;
		}

		public void Dispose()
		{
			CloseConnections();
		}

		public Assembly GetDalAssembly()
		{
			Assembly asm = GetType().Assembly;
			return asm;
		}

		public string AsmFileVersionString()
		{
			if (m_versionString != null)
				return m_versionString;
			Assembly asm = GetDalAssembly();
			FileVersionInfo info = FileVersionInfo.GetVersionInfo(asm.Location);
			m_versionString = info.ProductVersion;
			return m_versionString;	
		}


		public IDbTransaction NewTransaction(IDbConnection cn = null)
		{
			cn = cn ?? GetConnection();
			IDbTransaction tx = cn.BeginTransaction();
			return tx;
		}


		public IDbCommand CreateCommand<T>(string sql, object sqlParams = null, IDbConnection cn = null,
		                                   CommandType commandType = CommandType.Text, IDbTransaction tx = null,
		                                   int timeout = -1)
		{
			if (cn == null)
				cn = GetConnection<T>();
			return CreateCommand(sql, sqlParams, cn, commandType, tx, timeout);
		}


		/// <summary>
		/// You are responsible for disposing the command object returned from this method!
		/// </summary>
		public IDbCommand CreateCommand(string sql, object sqlParams = null, IDbConnection cn = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, int timeout = -1)
		{
			// set default values
			if (timeout == -1)
				timeout = DefaultTimeout;
			if (tx == null)
				tx = DefaultTransaction;
			if (cn == null)
				cn = GetConnection();

			IDbCommand cmd;
			switch(DbType)
			{
				case NoefDbType.SqlServer2012:
				case NoefDbType.SqlServer:
					cmd = new SqlCommand(sql);
					break;

#if SQL_CE
				case NoefDbType.SqlCE:
					cmd = new SqlCeCommand(sql);
					break;
#endif

				default:
					throw new NotSupportedException("Your DbType (" + DbType + ") is not currently supported");
			}
			cmd.CommandType = commandType;
			cmd.Connection = cn;
			if (tx != null)
				cmd.Transaction = tx;
			cmd.AddParams(sqlParams);
			cmd.CommandTimeout = timeout;
			return cmd;
		}

		public int ExecuteNonQuery(string sql, object sqlParams = null, IDbConnection cn = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, int timeout = -1)
		{
			using (IDbCommand cmd = CreateCommand(sql, sqlParams, cn, commandType, tx, timeout))
			{
				return cmd.ExecuteNonQuery();
			}
		}

		public object ExecuteScalar(string sql, object sqlParams = null, IDbConnection cn = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, int timeout = -1)
		{
			using (IDbCommand cmd = CreateCommand(sql, sqlParams, cn, commandType, tx, timeout))
			{
				return cmd.ExecuteScalar();
			}
		}

		public IList<object[]> GetRows(string sql, object sqlParams = null, IDbConnection cn = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, int timeout = -1)
		{
			using (IDbCommand cmd = CreateCommand(sql, sqlParams, cn, commandType, tx, timeout))
			using (IDataReader reader = cmd.ExecuteReader())
			{
				IList<object[]> rows = reader.GetRows();
				reader.Close();
				return rows;
			}
		}

		public IList<IList<object[]>> GetSets(string sql, object sqlParams = null, IDbConnection cn = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, int timeout = -1)
		{
			using (IDbCommand cmd = CreateCommand(sql, sqlParams, cn, commandType, tx, timeout))
			using (IDataReader reader = cmd.ExecuteReader())
			{
				IList<IList<object[]>> sets = new List<IList<object[]>>();

				// get the first set
				IList<object[]> rows = reader.GetRows();
				sets.Add(rows);

				// process all additional sets while there are still more result sets
				while (reader.NextResult())
				{
					rows = reader.GetRows();
					sets.Add(rows);
				}
				reader.Close();
				return sets;
			}
		}

		public RawTable Query(string sql, object sqlParams = null, int startingColumn = 0, IDbConnection cn = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, int timeout = -1)
		{
			using (IDbCommand cmd = CreateCommand(sql, sqlParams, cn, commandType, tx, timeout))
			using (IDataReader reader = cmd.ExecuteReader())
			{
				RawTable table = reader.GetRawTable(startingColumn);
				reader.Close();
				return table;
			}
		}

		public IList<T> Query<T>(string sql, object sqlParams = null, int startingColumn = 0, int pkColumn = -1, IDbConnection cn = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null, int timeout = -1)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			using (IDbCommand cmd = CreateCommand<T>(sql, sqlParams, cn, commandType, tx, timeout))
			using (IDataReader reader = cmd.ExecuteReader())
			{
				IList<T> rows = reader.HydrateList(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback);
				reader.Close();
				return rows;
			}
		}


		/// <summary>
		/// NOTE: The "where" parameter does no checks for sql injection! Do not use raw user input!!
		/// </summary>
		public IList<T> Where<T>(string where, object sqlParams, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = String.Format("SELECT {0} FROM {1} WHERE {2}", TableMetadata.GetColumnsString<T>(), tmeta.Name, where);
			using (IDbCommand cmd = CreateCommand<T>(sql, sqlParams, cn, CommandType.Text, tx, timeout))
			{
				if (beforeExecute != null)
					beforeExecute(cmd);
				using (IDataReader reader = cmd.ExecuteReader())
				{
					IList<T> list = reader.HydrateList<T>();
					reader.Close();
					return list;
				}
			}
		}


		public IList<T> Unique<T>(string sql, int keyIndex, object sqlParams = null, IDbConnection cn = null, int startingColumn = 0, int pkColumn = -1, CommandType commandType = CommandType.Text, IDbTransaction tx = null, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null, int timeout = -1)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;
			using (IDbCommand cmd = CreateCommand<T>(sql, sqlParams, cn, commandType, tx, timeout))
			using (IDataReader reader = cmd.ExecuteReader())
			{
				IList<object[]> rawRows = reader.GetRows();
				IList<T> rows = rawRows.HydrateUniqueList(keyIndex, startingColumn, pkColumn, propsToHydrate, hydrateRowCallback);
				reader.Close();
				return rows;
			}
		}


		public T SingleOrDefault<T>(string sql, object sqlParams = null, IDbConnection cn = null, int startingColumn = 0, int pkColumn = -1, CommandType commandType = CommandType.Text, IDbTransaction tx = null, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null, int timeout = -1)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			using (IDbCommand cmd = CreateCommand<T>(sql, sqlParams, cn, commandType, tx, timeout))
			using (IDataReader reader = cmd.ExecuteReader())
			{
				T record = reader.Hydrate(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback);
				reader.Close();
				return record;
			}
		}

		public void EnsureColumnsExist<T>(IEnumerable<string> columns)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			foreach (string namedColumn in columns)
			{
				if (!tmeta.Columns.Any(col => String.Equals(col.Name, namedColumn, StringComparison.OrdinalIgnoreCase)))
					throw new Exception("Column \"" + namedColumn + "\" does not exist in table " + tmeta.Name);
			}
		}

	}
}
