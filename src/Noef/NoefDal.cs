using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Security;
#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public abstract class NoefDal : IDisposable
	{
		private string m_versionString;

		public abstract string ConnectionStringName { get; }
		public abstract NoefDbType DbType { get; }
		private readonly string m_uniqueDalKey;
		public int DefaultTimeout { get; set; }
		public NoefUserRequest Req { get; set; }

		/// <summary>
		/// Can be assigned a transaction that will be used for ALL noef based queries.
		/// Make SURE you assign this to null in the finally clause of your try/catch/finally!
		/// </summary>
		public IDbTransaction DefaultTransaction { get; set; }

		public IList<IDbConnection> OpenedConnections { get; private set; } 

		protected NoefDal()
		{
			m_uniqueDalKey = GetType().FullName;
			OpenedConnections = new List<IDbConnection>();
			DefaultTimeout = 30;

			HttpContext context = HttpContext.Current;
			HttpApplication app = context == null ? null : context.ApplicationInstance;

			// virtual calls here should be ok, because we'll always be using the most specific subclass.
// ReSharper disable DoNotCallOverridableMethodsInConstructor
			Req = CreateUserRequest();
			AuthorizeRequest(app);
// ReSharper restore DoNotCallOverridableMethodsInConstructor
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
			Assembly asm = Assembly.GetExecutingAssembly();
			FileVersionInfo info = FileVersionInfo.GetVersionInfo(asm.Location);
			m_versionString = info.ProductVersion;
			return m_versionString;	
		}

		public virtual NoefUserRequest CreateUserRequest()
		{
			return new NoefUserRequest(this);
		}

		public virtual bool IsCurrentUserAdmin()
		{
			return false;
		}

		public virtual void AuthorizeRequest(HttpApplication app)
		{
			// stub
		}

		public virtual void EndRequest(HttpApplication app)
		{
			// Will call CloseConnections()
			Dispose();
		}

		public virtual string GetConnectionString(string connectionStringName = null)
		{
			if (String.IsNullOrEmpty(connectionStringName))
				connectionStringName = ConnectionStringName;

			ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[connectionStringName];
			if (settings == null)
				throw new Exception("No connection string found with name " + connectionStringName);
			return settings.ConnectionString;
		}

		/// <summary>
		/// Will get a connection string appropriate for the mapped type you pass in, which MAY be different than your DAL's default connection.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="connectionStringName"></param>
		/// <returns></returns>
		public virtual string GetConnectionString<T>(string connectionStringName = null)
		{
			// If they didn't pass in a connection string name,
			// try to get the one that's specific for this table (TableMetadata.ConnectionName).
			// If that's null, use the default connection string name for this DAL.
			if (String.IsNullOrEmpty(connectionStringName))
				connectionStringName = TableMetadata.For<T>().ConnectionName ?? ConnectionStringName;
			return GetConnectionString(connectionStringName);
		}

		/// <summary>
		/// Connections are cached per-request.
		/// </summary>
		public virtual IDbConnection GetConnection<T>(string connectionStringName = null)
		{
			// If they didn't pass in a connection string name,
			// try to get the one that's specific for this table (TableMetadata.ConnectionName).
			// If that's null, use the default connection string name for this DAL.
			if (String.IsNullOrEmpty(connectionStringName))
				connectionStringName = TableMetadata.For<T>().ConnectionName ?? ConnectionStringName;
			return GetConnection(connectionStringName);
		}

		/// <summary>
		/// Connections are cached per-request.
		/// </summary>
		public virtual IDbConnection GetConnection(string connectionStringName = null)
		{
			if (String.IsNullOrEmpty(connectionStringName))
				connectionStringName = ConnectionStringName;
			string key = m_uniqueDalKey + "_cn_" + connectionStringName;
			IDbConnection cn = (IDbConnection)PerRequestStore.GetData(key);
			if (cn == null)
			{
				string cnString = GetConnectionString(connectionStringName);
				switch(DbType)
				{
					case NoefDbType.SqlServer2012:
					case NoefDbType.SqlServer:
						cn = new SqlConnection(cnString);
						break;
#if SQL_CE
					case NoefDbType.SqlCE:
						cn = new SqlCeConnection(cnString);
						break;
#endif

					default:
						throw new NotSupportedException("Your DbType (" + DbType + ") is not currently supported");
				}
				PerRequestStore.SetData(key, cn);
				cn.Open();
				OpenedConnections.Add(cn);
			}
			return cn;
		}

		/// <summary>
		/// This should ALWAYS be called in your HttpApplication (Global.asax) EndRequest handler!
		/// </summary>
		public void CloseConnections()
		{
			foreach(IDbConnection cn in OpenedConnections)
			{
				if (cn.State != ConnectionState.Closed)
				{
					try
					{
						cn.Close();
						cn.Dispose();
					}
// ReSharper disable EmptyGeneralCatchClause
					catch
// ReSharper restore EmptyGeneralCatchClause
					{
						// Eat it =/
					}
				}
			}
			OpenedConnections.Clear();
		}

		public static string GetCompleteStackTrace(Exception ex)
		{
			if (ex == null)
				return null;

			StringBuilder sb = new StringBuilder();
			while (ex != null)
			{
				sb.AppendLine("Type: " + ex.GetType().Name);
				sb.AppendLine("Message: " + ex.Message);
				sb.AppendLine("Stack Trace:");
				sb.AppendLine(ex.StackTrace).AppendLine();
				ex = ex.InnerException;
				if (ex != null)
					sb.AppendLine("Inner exception:");
			}
			return sb.ToString();
		}

		/// <summary>
		/// Checks to see if the current request can skip authorization, either because context.SkipAuthorization is true,
		/// or because UrlAuthorizationModule.CheckUrlAccessForPrincipal() returns true for the current request/user/url.
		/// </summary>
		/// <returns></returns>
		public bool SkipUrlAuth()
		{
			HttpContext context = HttpContext.Current;
			string path = context.Request.AppRelativeCurrentExecutionFilePath;
			Debug.Assert(path != null);
			return context.SkipAuthorization || UrlAuthorizationModule.CheckUrlAccessForPrincipal(path, context.User, context.Request.RequestType);
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

		// ********************************************************************************
		// *** Page queries ***************************************************************
		// *** NOTE: These are taken and modified from PetaPoco, adapted to work in Noef **
		// *** http://www.toptensoftware.com/petapoco/ ************************************
		// ********************************************************************************

		private static readonly Regex rxColumns = new Regex(@"\A\s*SELECT\s+((?:\((?>\((?<depth>)|\)(?<-depth>)|.?)*(?(depth)(?!))\)|.)*?)(?<!,\s+)\bFROM\b", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
		private static readonly Regex rxOrderBy = new Regex(@"\bORDER\s+BY\s+(?:\((?>\((?<depth>)|\)(?<-depth>)|.?)*(?(depth)(?!))\)|[\w\(\)\.])+(?:\s+(?:ASC|DESC))?(?:\s*,\s*(?:\((?>\((?<depth>)|\)(?<-depth>)|.?)*(?(depth)(?!))\)|[\w\(\)\.])+(?:\s+(?:ASC|DESC))?)*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
		private static readonly Regex rxDistinct = new Regex(@"\ADISTINCT\s", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

		private static bool splitSqlForPaging(string originalQuery, out string sqlCount, out string columnsList, out string sqlOrderBy)
		{
			columnsList = null;
			sqlCount = null;
			sqlOrderBy = null;

			// Extract the columns from "SELECT <whatever> FROM"
			Match m = rxColumns.Match(originalQuery);
			if (!m.Success)
				return false;

			// Save column list and replace with COUNT(*)
			Group g = m.Groups[1];
			columnsList = originalQuery.Substring(g.Index);

			if (rxDistinct.IsMatch(columnsList))
				sqlCount = originalQuery.Substring(0, g.Index) + "COUNT(" + m.Groups[1].ToString().Trim() + ") " + originalQuery.Substring(g.Index + g.Length);
			else
				sqlCount = originalQuery.Substring(0, g.Index) + "COUNT(*) " + originalQuery.Substring(g.Index + g.Length);


			// Look for an "ORDER BY <whatever>" clause
			m = rxOrderBy.Match(sqlCount);
			if (m.Success)
			{
				g = m.Groups[0];
				sqlOrderBy = g.ToString();
				sqlCount = sqlCount.Substring(0, g.Index) + sqlCount.Substring(g.Index + g.Length);
			}

			return true;
		}

		private static void buildPageQueries(int skip, int take, string originalSql, out string sqlCount, out string sqlPage) 
		{
			// Split the SQL into the bits we need
			string columnsList, sqlOrderBy;
			if (!splitSqlForPaging(originalSql, out sqlCount, out columnsList, out sqlOrderBy))
				throw new Exception("Unable to parse SQL statement for paged query");

			// Build the SQL for the actual final result
			columnsList = rxOrderBy.Replace(columnsList, "");
			if (rxDistinct.IsMatch(columnsList))
			{
				columnsList = "__noef_inner.* FROM (SELECT " + columnsList + ") __noef_inner";
			}
			sqlPage = string.Format("SELECT * FROM (SELECT ROW_NUMBER() OVER ({0}) __noef_rownum, {1}) peta_paged WHERE __noef_rownum > " + skip + " AND __noef_rownum <= " + (skip + take),
										sqlOrderBy ?? "ORDER BY (SELECT NULL)", columnsList);
		}

		// Fetch a page
		public PagedData<T> Page<T>(int pageIndex, int pageSize, string sql, object sqlParams, IDbConnection cn = null, IDbTransaction tx = null, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null, int timeout = -1)
		{
			string sqlCount, sqlPage;
			buildPageQueries(pageIndex * pageSize, pageSize, sql, out sqlCount, out sqlPage);

			// Get the records and the total count (we do startingColum=1 to skip the row number column)
			IList<T> data = Query(sqlPage, sqlParams, 1, propsToHydrate: propsToHydrate, hydrateRowCallback: hydrateRowCallback, cn: cn, tx: tx, timeout: timeout);
			int totalRecords = (int)ExecuteScalar(sqlCount, sqlParams);

			// Create the PagedData object and return it
			PagedData<T> result = new PagedData<T>(data, pageSize, pageIndex, totalRecords);
			return result;
		}


		// Fetch a page (non-generic version)
		public PagedData Page(int pageIndex, int pageSize, string sql, object sqlParams, IDbConnection cn = null, IDbTransaction tx = null, int timeout = -1)
		{
			string sqlCount, sqlPage;
			buildPageQueries(pageIndex * pageSize, pageSize, sql, out sqlCount, out sqlPage);

			// Get the records and the total count (we do startingColum=1 to skip the row number column)
			RawTable rawTable = Query(sqlPage, sqlParams, 1, cn, tx: tx, timeout: timeout);
			int totalRecords = (int)ExecuteScalar(sqlCount, sqlParams);

			// Create the PagedData object and return it
			PagedData result = new PagedData(rawTable, pageSize, pageIndex, totalRecords);
			return result;
		}


		// ********************************************************************************
		// *** CRUD helpers (select, insert, update, delete) ******************************
		// ********************************************************************************

		public IList<TPrimary> InnerJoin<TPrimary, TJoined>(string where, string orderBy, object sqlParams,
				Expression<Func<TPrimary, object>> joinOnPrimary,
				Expression<Func<TJoined, object>> joinOn,
				Expression<Func<TPrimary, object>> propToHydrate,
				IDbConnection cn = null,
				IDbTransaction tx = null,
				int timeout = -1)
			where TPrimary : class
			where TJoined : class
		{
			return join("INNER", where, orderBy, sqlParams, joinOnPrimary, joinOn, propToHydrate, cn, tx, timeout);
		}

		public IList<TPrimary> LeftJoin<TPrimary, TJoined>(string where, string orderBy, object sqlParams,
				Expression<Func<TPrimary, object>> joinOnPrimary,
				Expression<Func<TJoined, object>> joinOn,
				Expression<Func<TPrimary, object>> propToHydrate,
				IDbConnection cn = null,
				IDbTransaction tx = null,
				int timeout = -1)
			where TPrimary : class
			where TJoined : class
		{
			return join("LEFT", where, orderBy, sqlParams, joinOnPrimary, joinOn, propToHydrate, cn, tx, timeout);
		}


		/// <summary>
		/// NOTE: This is for a one-to-many currently. Might want to change the name to reflect that, if I ever do one that is one-to-one.
		/// TODO: Currently only supports a single primary key (Pks[0])
		/// </summary>
		private IList<TPrimary> join<TPrimary, TJoined>(string joinType, string where, string orderBy, object sqlParams,
				Expression<Func<TPrimary, object>> joinOnPrimary,
				Expression<Func<TJoined, object>> joinOn,
				Expression<Func<TPrimary, object>> propToHydrate,
				IDbConnection cn = null,
				IDbTransaction tx = null,
				int timeout = -1)
			where TPrimary : class
			where TJoined : class
		{
			TableMetadata t1 = TableMetadata.For<TPrimary>();
			TableMetadata t2 = TableMetadata.For<TJoined>();

			string col1 = joinOnPrimary == null ? t1.Pks[0].Name : ReflectionHelper.GetPropertyName(joinOnPrimary);
			string col2 = ReflectionHelper.GetPropertyName(joinOn);
			string propName = ReflectionHelper.GetPropertyName(propToHydrate);

			// We know the column names we're joining on between the 2 tables, now get refs to the appropriate ColumnMetadata objects
			//ColumnMetadata colMeta1 = t1.Columns.Single(c => String.Equals(c.Name, col1, StringComparison.OrdinalIgnoreCase));
			//ColumnMetadata colMeta2 = t2.Columns.Single(c => String.Equals(c.Name, col2, StringComparison.OrdinalIgnoreCase));

			// Default values for where and orderBy if none were provided
			if (String.IsNullOrEmpty(where))
				where = "1 = 1";
			if (String.IsNullOrEmpty(orderBy))
				orderBy = t1.Name + "." + t1.Pks[0].Name + " DESC, " + t2.Name + "." + t2.Pks[0].Name + " DESC";

			// Set up the query
			string sql = String.Format(
@"SELECT
	{0},
	{1}
FROM
	{2} {2}
	{3} JOIN {4} {4}
	ON {2}.{5} = {4}.{6}
WHERE
	{7}
ORDER BY
	{8}",
		TableMetadata.GetColumnsString<TPrimary>(t1.Name, ""),
		TableMetadata.GetColumnsString<TJoined>(t2.Name, ""),
		t1.Name, joinType, t2.Name, col1, col2, where, orderBy);

			// Get the raw rows
			IList<object[]> rows = GetRows(sql, sqlParams, cn, CommandType.Text, tx, timeout);
			// If there were no results, return an empty list
			if (rows.Count == 0)
				return new List<TPrimary>();

			// Get a unique list of the "primary" objects (the left table in the join)
			IList<TPrimary> primaries = rows.HydrateUniqueList<TPrimary>(0, t1.Columns.IndexOf(t1.Pks[0]));

			// Is the property we're hydrating in the primary class a list or a single property?
			bool isList = typeof(IEnumerable).IsAssignableFrom(t1.Properties[propName].PropertyType);
			int joinedOffset = t1.Columns.Count;
			if (isList)
			{
				// (one-to-many)
				// Get a "list of lists" for the related objects (each list corresponds to 1 of the 
				int splitIndex = t1.Columns.IndexOf(t1.Pks[0]);
				IDictionary<object, IList<TJoined>> joinedLists = rows.HydrateNonNullListPerKey<TJoined>(splitIndex, joinedOffset, joinedOffset + t2.Columns.IndexOf(t2.Pks[0]));
				foreach(TPrimary primary in primaries)
				{
					object pk = t1.Pks[0].Property.GetValue(primary);
					if (pk == null)
						throw new Exception("Null PK value");
					IList<TJoined> list = joinedLists[pk];
					ReflectionHelper.SetValue(t1.Properties[propName], primary, list);
				}
			}
			else
			{
				// (one-to-one)
				// We're hydrating a single property in the primary class
				IList<TJoined> joined = rows.HydrateList<TJoined>(joinedOffset, joinedOffset + t2.Columns.IndexOf(t2.Pks[0]));
				for (int i = 0; i < primaries.Count; i++)
					ReflectionHelper.SetValue(t1.Properties[propName], primaries[i], joined[i]);
			}

			return primaries;
		}


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


		/// <summary>
		/// Constructs and executes an UPDATE statement based on the table specified by T (using the column metadata).
		/// A single row will be updated using a WHERE clause based on the pkColumn name (taken from the TableMetadata.Pk entry)
		/// Each column in the table will be updated with the corresponding property in "obj". If you want to override any, use the overrides dictionary.
		/// Example key/pair in overrides: {"DateModified", "GetDate()"}.
		/// In the overrides, the exact text you use for value will be placed in the query.  So you can add custom params if you want, and give them values using the beforeExecute Action.
		/// Example: {"SomeColumn", "@someParam"} for overrides, and for beforeExecute use: cmd => { cmd.AddParam("@someParam", getSomeValue()); }.
		/// Make sure your param names are not equal to any of the column names, since those will be used already.
		/// If you want to exclude any columns from being in the SET clause of the UPDATE, put them in the excluded columns enumerable.
		/// This will never try to update the PK column (and not under the noob assumption that PKs are always identity fields, but because you shouldn't be changing PK values.
		/// They should be surrogate values that do not change.  Ever. If you want to update the PK column, write the SQL query manually.  These helper methods are for the 99% of cases
		/// where you do sane things).
		/// </summary>
		/// <typeparam name="T">The class that represents the table in the database that you want to update a record for</typeparam>
		/// <param name="obj">The object representing the record you want to update</param>
		/// <param name="cn">The connection to use</param>
		/// <param name="columnUpdateOverrides">Literal string values to use for specific column names (instead of default param names and "obj"'s values)</param>
		/// <param name="excludedColumns">Columns to exclude from the SET column list</param>
		/// <param name="beforeExecute">An Action to be executed (on the IDbCommand) before ExecuteReader() is called (can be used to add params, etc)</param>
		/// <param name="tx"> </param>
		/// <param name="timeout"></param>
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database (fields only, no collections. This is not an ORM, duh)</returns>
		public void Update<T>(T obj, IDictionary<string, string> columnUpdateOverrides, IEnumerable<string> excludedColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			string pkColumn = TableMetadata.For<T>().Pk.Name;
			UpdateByKey(obj, pkColumn, columnUpdateOverrides, excludedColumns, cn, beforeExecute, tx, timeout);
		}


		/// <summary>
		/// Constructs and executes an UPDATE statement based on the table specified by T (using the column metadata).
		/// A single row will be updated using a WHERE clause based on the pkColumn name (the value will be taken from the obj parameter, getting the correct
		/// property's value, also based on the value of pkColumn).
		/// Each column in the table will be updated with the corresponding property in "obj". If you want to override any, use the overrides dictionary.
		/// Example key/pair in overrides: {"DateModified", "GetDate()"}.
		/// In the overrides, the exact text you use for value will be placed in the query.  So you can add custom params if you want, and give them values using the beforeExecute Action.
		/// Example: {"SomeColumn", "@someParam"} for overrides, and for beforeExecute use: cmd => { cmd.AddParam("@someParam", getSomeValue()); }.
		/// Make sure your param names are not equal to any of the column names, since those will be used already.
		/// If you want to exclude any columns from being in the SET clause of the UPDATE, put them in the excluded columns enumerable.
		/// This will never try to update the PK column (and not under the noob assumption that PKs are always identity fields, but because you shouldn't be changing PK values.
		/// They should be surrogate values that do not change.  Ever. If you want to update the PK column, write the SQL query manually.  These helper methods are for the 99% of cases
		/// where you do sane things).
		/// </summary>
		/// <typeparam name="T">The class that represents the table in the database that you want to update a record for</typeparam>
		/// <param name="obj">The object representing the record you want to update</param>
		/// <param name="cn">The connection to use</param>
		/// <param name="pkColumn">The name of the column that is the primary key. No support for composite keys.</param>
		/// <param name="columnUpdateOverrides">Literal string values to use for specific column names (instead of default param names and "obj"'s values)</param>
		/// <param name="excludedColumns">Columns to exclude from the SET column list</param>
		/// <param name="beforeExecute">An Action to be executed (on the IDbCommand) before ExecuteReader() is called (can be used to add params, etc)</param>
		/// <param name="tx"> </param>
		/// <param name="timeout"></param>
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database (fields only, no collections. This is not an ORM, duh)</returns>
		public void UpdateByKey<T>(T obj, string pkColumn, IDictionary<string, string> columnUpdateOverrides, IEnumerable<string> excludedColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			StringBuilder sb = new StringBuilder();
			List<string> excluded = excludedColumns == null ? new List<string>() : excludedColumns.ToList();

			// Set up the sql statement
			sb.AppendLine("UPDATE " + tmeta.Name);
			sb.AppendLine("SET");
			List<string> sets = new List<string>();
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				// Do not try to update the PK column (sanity, please).
				if (String.Equals(col.Name, pkColumn, StringComparison.OrdinalIgnoreCase))
					continue;
				// Skip columns the user explicitly said to exclude.
				if (excluded.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
					continue;
				// If they've provided an override for the current column, use the exact string they provided
				if (columnUpdateOverrides != null && columnUpdateOverrides.Keys.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
					sets.Add(col.Name + " = " + columnUpdateOverrides[col.Name]);
				else
					// Otherwise, set the col value to a param with the same name as the column.
					sets.Add(col.Name + " = @" + col.Name);
			}
			sb.AppendLine(String.Join(",", sets.ToArray()));
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);
			sb.AppendLine();
			sb.AppendLine("-- Return the updated record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);

			// Set up the command
			T updatedObj;
			using (IDbCommand cmd = CreateCommand<T>(sb.ToString(), null, cn, CommandType.Text, tx, timeout))
			{
				// Add the pk param
				cmd.AddParam("@" + pkColumn, tmeta.GetColumn(pkColumn).Property.GetValue(obj));

				// Add the remaining params
				foreach (ColumnMetadata col in tmeta.Columns)
				{
					// Don't add the pk column, it's already been added.
					if (String.Equals(col.Name, pkColumn, StringComparison.OrdinalIgnoreCase))
						continue;
					if (excluded.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
						continue;
					if (columnUpdateOverrides != null && columnUpdateOverrides.Keys.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
						continue;
					cmd.AddParam("@" + col.Name, col.Property.GetValue(obj));
				}

				if (beforeExecute != null)
					beforeExecute(cmd);

				// Execute the command
				using (IDataReader reader = cmd.ExecuteReader())
				{
					// Get the updated object
					updatedObj = reader.Hydrate<T>();
					reader.Close();
				}
			}

			// Update the original passed in obj with the updated obj's values
			// (We can't just assign, cause we want to change the object from the calling site. .NET passes objects as "references by value")
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				object val = col.Property.GetValue(updatedObj);
// ReSharper disable AssignNullToNotNullAttribute
				col.Property.SetValue(obj, val);
// ReSharper restore AssignNullToNotNullAttribute
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


		/// <summary>
		/// Constructs and executes an UPDATE statement based on the table specified by T (using the column metadata).
		/// A single row will be updated using a WHERE clause based on the pkColumn name (the value will be taken from the obj parameter, getting the correct
		/// property's value, also based on the value of pkColumn).
		/// ONLY columns specified in the whitelist will be updated.
		/// Each column in the table will be updated with the corresponding property in "obj". No overrides in this method, this is for very simple updates.
		/// </summary>
		/// <typeparam name="T">The class that represents the table in the database that you want to update a record for</typeparam>
		/// <param name="obj">The object representing the record you want to update</param>
		/// <param name="cn">The connection to use</param>
		/// <param name="whitelistColumns">The columns you want to be updated (will be in the SET columns list)</param>
		/// <param name="beforeExecute"></param>
		/// <param name="tx"> </param>
		/// <param name="timeout"></param>
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database, including columns that were
		/// not in the update statement (triggers, who knows)</returns>
		public void Update<T>(T obj, IEnumerable<string> whitelistColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			string pkColumn = TableMetadata.For<T>().Pk.Name;
			UpdateByKey(obj, pkColumn, whitelistColumns, cn, beforeExecute, tx, timeout);
		}


		/// <summary>
		/// Constructs and executes an UPDATE statement based on the table specified by T (using the column metadata).
		/// A single row will be updated using a WHERE clause based on the pkColumn name (taken from the TableMetadata.Pk entry)
		/// ONLY columns specified in the whitelist will be updated.
		/// Each column in the table will be updated with the corresponding property in "obj". No overrides in this method, this is for very simple updates.
		/// </summary>
		/// <typeparam name="T">The class that represents the table in the database that you want to update a record for</typeparam>
		/// <param name="obj">The object representing the record you want to update</param>
		/// <param name="cn">The connection to use</param>
		/// <param name="pkColumn">The name of the column that is the primary key. No support for composite keys.</param>
		/// <param name="whitelistColumns">The columns you want to be updated (will be in the SET columns list)</param>
		/// <param name="beforeExecute"></param>
		/// <param name="tx"> </param>
		/// <param name="timeout"></param>
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database, including columns that were
		/// not in the update statement (triggers, who knows)</returns>
		public void UpdateByKey<T>(T obj, string pkColumn, IEnumerable<string> whitelistColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, int timeout = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			StringBuilder sb = new StringBuilder();

			// validate the whitelisted columns -- throw an exception if one specified doesn't exist
			List<string> whitelist = whitelistColumns == null ? new List<string>() : whitelistColumns.ToList();
			EnsureColumnsExist<T>(whitelist);

			// Set up the sql statement
			sb.AppendLine("UPDATE " + tmeta.Name);
			sb.AppendLine("SET");


			// here's the old way, for reference. I went with the weird resharper suggestion to use a linq statement.
			// Generally I don't favor the "sql" flavor of linq, but I want to be more familiar with it, so I'm forcing it here.
			/*
			List<string> sets = new List<string>();
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				// TODO: potentially more efficient to loop through the whitelist columns, instead of all columns and checking for membership in the list.
				if (!whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
					continue;
				sets.Add(col.Name + " = @" + col.Name);
			}
			sb.AppendLine(String.Join(",", sets.ToArray()));
			*/

			// new way (linq)
			string[] sets = (from col in tmeta.Columns
				where whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase)
				select col.Name + " = @" + col.Name).ToArray();

			sb.AppendLine(String.Join(",", sets));
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);
			sb.AppendLine();
			sb.AppendLine("-- Return the updated record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);

			// Set up the command
			T updatedObj;
			using (IDbCommand cmd = CreateCommand<T>(sb.ToString(), null, cn, CommandType.Text, tx, timeout))
			{
				// Add the pk param
				cmd.AddParam("@" + pkColumn, tmeta.GetColumn(pkColumn).Property.GetValue(obj));

				// Add the remaining params
				foreach (ColumnMetadata col in tmeta.Columns)
				{
					// Don't add the pk column, it's already been added
					if (String.Equals(col.Name, pkColumn, StringComparison.OrdinalIgnoreCase))
						continue;
					if (!whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
						continue;
					cmd.AddParam("@" + col.Name, col.Property.GetValue(obj));
				}

				// Execute the command
				if (beforeExecute != null)
					beforeExecute(cmd);
				using (IDataReader reader = cmd.ExecuteReader())
				{
					// Get the updated object
					updatedObj = reader.Hydrate<T>();
					reader.Close();
				}
			}

			// Update the original passed in obj with the updated obj's values
			// (We can't just assign, cause we want to change the object from the calling site. .NET passes objects as "references by value")
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				object val = col.Property.GetValue(updatedObj);
// ReSharper disable AssignNullToNotNullAttribute
				col.Property.SetValue(obj, val);
// ReSharper restore AssignNullToNotNullAttribute
			}
		}


		/// <summary>
		/// Constructs and executes an INSERT statement based on the table specified by T (using the column metadata).
		/// The values used for the insert will be taken from "obj".
		/// Since not all tables have an IDENTITY PK, you'll have to exclude the PK column if you don't want it in the column list or VALUES() values.
		/// Similiar to Update(), you can override column values (the exact string provided will be used instead of a param, populated with "obj"'s corresponding property value).
		/// Any params specified in the overrides can be populated using the beforeExecute Action.
		/// This returns the same "obj" that was passed in, but with all properties updated to reflect their current values in the database (so if you have an IDENTITY PK field, you'll have the value now).
		/// </summary>
		public void Insert<T>(T obj, IDictionary<string, string> columnInsertOverrides, IEnumerable<string> excludedColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, CommandType commandType = CommandType.Text, IDbTransaction tx = null, string newRecordIdSelector = "SCOPE_IDENTITY()", int timeout = -1)
		{
			string pkColumn = TableMetadata.For<T>().Pk.Name;
			InsertByKey(obj, pkColumn, columnInsertOverrides, excludedColumns, cn, beforeExecute, tx, newRecordIdSelector, timeout);
		}


		/// <summary>
		/// Constructs and executes an INSERT statement based on the table specified by T (using the column metadata).
		/// The values used for the insert will be taken from "obj".
		/// Since not all tables have an IDENTITY PK, you'll have to exclude the PK column if you don't want it in the column list or VALUES() values.
		/// Similiar to Update(), you can override column values (the exact string provided will be used instead of a param, populated with "obj"'s corresponding property value).
		/// Any params specified in the overrides can be populated using the beforeExecute Action.
		/// This returns the same "obj" that was passed in, but with all properties updated to reflect their current values in the database (so if you have an IDENTITY PK field, you'll have the value now).
		/// </summary>
		public void InsertByKey<T>(T obj, string pkColumn, IDictionary<string, string> columnInsertOverrides, IEnumerable<string> excludedColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, IDbTransaction tx = null, string newRecordIdSelector = "SCOPE_IDENTITY()", int timeout = -1)
		{
			if (newRecordIdSelector == null)
				newRecordIdSelector = "SCOPE_IDENTITY()";
			TableMetadata tmeta = TableMetadata.For<T>();
			StringBuilder sb = new StringBuilder();

			List<string> excluded = excludedColumns == null ? new List<string>() : excludedColumns.ToList();

			// Set up the sql statement
			sb.AppendLine("INSERT INTO " + tmeta.Name);

			IEnumerable<string> insertCols = tmeta.Columns
				.Where(col => !excluded.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
				.Select(c => c.Name);

			sb.AppendLine("(" + String.Join(",", insertCols.ToArray()) + ")");
			sb.Append("VALUES(");

			List<string> insertVals = new List<string>();
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				if (excluded.Contains(col.Name))
					continue;
				if (columnInsertOverrides != null && columnInsertOverrides.Keys.Contains(col.Name))
					insertVals.Add(columnInsertOverrides[col.Name]);
				else
					insertVals.Add("@" + col.Name);
			}
			sb.Append(String.Join(",", insertVals.ToArray()));
			sb.AppendLine(")");

			sb.AppendLine("-- Return the inserted record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + pkColumn + " = " + newRecordIdSelector);

			// Set up the command
			T insertedObj;
			using (IDbCommand cmd = CreateCommand<T>(sb.ToString(), null, cn, CommandType.Text, tx, timeout))
			{
				foreach (ColumnMetadata col in tmeta.Columns)
				{
					if (excluded.Contains(col.Name))
						continue;
					if (columnInsertOverrides != null && columnInsertOverrides.Keys.Contains(col.Name))
						continue;
					cmd.AddParam("@" + col.Name, col.Property.GetValue(obj));
				}

				// Execute the command
				if (beforeExecute != null)
					beforeExecute(cmd);
				using (IDataReader reader = cmd.ExecuteReader())
				{
					// Get the inserted object
					insertedObj = reader.Hydrate<T>();
					reader.Close();
				}
			}

			// Update the original passed in obj with the inserted obj's values
			// (We can't just assign, cause we want to change the object from the calling site. .NET passes objects as "references by value")
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				object val = col.Property.GetValue(insertedObj);
// ReSharper disable AssignNullToNotNullAttribute
				col.Property.SetValue(obj, val);
// ReSharper restore AssignNullToNotNullAttribute
			}
		}



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
