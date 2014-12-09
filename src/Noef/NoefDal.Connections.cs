using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public abstract partial class NoefDal
	{
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
			string key = m_uniqueDalKey + "_cn_" + connectionStringName; // FamiliaDal_cn_g_Adhoc

			// return a  cached one if we have it (local instance storage only - per dal instance)
			IDbConnection cn;
			if (OpenedConnections.ContainsKey(key))
			{
				cn = OpenedConnections[key];
				return cn;
			}

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
				
			cn.Open();
			bool added = OpenedConnections.TryAdd(key, cn);
			// TODO: How to handle failure here? Could just mean that another thread already added it?
			// What would you do in that case? Discard this one and use the one already added?
			if (!added)
			{
				Exception ex = new Exception("NOefDal.GetConnection() was unable to add a new connection to the OpenedConnections dictionary...");
				Trace.Write(ex);
			}
			return cn;
		}

		/// <summary>
		/// This should ALWAYS be called in your HttpApplication (Global.asax) EndRequest handler!
		/// </summary>
		public void CloseConnections()
		{
			foreach(var pair in OpenedConnections)
			{
				IDbConnection cn = pair.Value;
				if (cn.State != ConnectionState.Closed)
				{
					try
					{
						// Clear the cache! Otherwise next request for this connection will returned the cached (& closed...) one = exception.
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
	
	}
}
