using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

#if SQL_TABLE_VALUED_PARAMS
using Microsoft.SqlServer.Server;
using SqlMetaData = Microsoft.SqlServer.Server.SqlMetaData;
#endif

#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public static class IDbCommandExtensions
	{
		/// <summary>
		/// Factory method to create the correct type of IDataParameter
		/// </summary>
		private static IDataParameter getParam(IDbCommand cmd, string paramName, object value)
		{
			IDataParameter param;
			if (cmd is SqlCommand)
			{
				param = value == null ? new SqlParameter(paramName, DBNull.Value) : new SqlParameter(paramName, value);
			}
#if SQL_CE
			else if (cmd is SqlCeCommand)
			{
				param = value == null ? new SqlCeParameter(paramName, DBNull.Value) : new SqlCeParameter(paramName, value);
			}
#endif
			else
			{
				throw new NotSupportedException("You have provided an unsupported command type. Noef currently supports SQL Server and SQL Server CE");
			}
			return param;
		}


		public static void AddParam<T>(this IDbCommand cmd, string paramName, T value)
		{
			IDataParameter param = getParam(cmd, paramName, value);
			cmd.Parameters.Add(param);
		}


		public static void AddParams(this IDbCommand cmd, object sqlParams)
		{
			if (sqlParams != null)
			{
				IDictionary<string, object> dict = sqlParams.ToDictionary();
				foreach (var pair in dict)
				{
					if (pair.Value is IDataParameter)
						cmd.Parameters.Add(pair.Value);
					else
						cmd.Parameters.Add(getParam(cmd, "@" + pair.Key, pair.Value));
				}
			}
		}


#if SQL_TABLE_VALUED_PARAMS
		/// <summary>
		/// This is specific to SQL Server.
		/// Converts ints to a List{SqlDataRecord} that can be passed to sql as a table valued param.
		/// http://www.sommarskog.se/arrays-in-sql-2008.html
		/// </summary>
		public static List<SqlDataRecord> IntsAsList(IEnumerable<int> ints, string intColName)
		{
			if (ints == null)
				return null;

			int[] intsArray = ints.ToArray();

			// If you try to use an empty List<SqlDataRecord> for a table valued param, you'll get this error:
			// Message: There are no records in the SqlDataRecord enumeration. To send a table-valued parameter with no rows, use a null reference for the value instead.
			// So we return null here instead, if that's the case
			if (intsArray.Length == 0)
				return null;

			List<SqlDataRecord> list = new List<SqlDataRecord>();

			// Create an SqlMetaData object that describes our table type.
			SqlMetaData[] tvp_definition = { new SqlMetaData(intColName, SqlDbType.Int) };

			// Loop over the ints.
			foreach (int i in intsArray)
			{
				// Create a new record, using the metadata array above.
				SqlDataRecord rec = new SqlDataRecord(tvp_definition);
				// Set the value and add it to the list
				rec.SetInt32(0, i);
				list.Add(rec);
			}
			return list;
		}


		/// <summary>
		/// This is specific to SQL Server.
		/// Converts strings to a List{SqlDataRecord} that can be passed to sql as a table valued param.
		/// http://www.sommarskog.se/arrays-in-sql-2008.html
		/// </summary>
		public static List<SqlDataRecord> StringsToVarcharList(IEnumerable<string> strings, string colName)
		{
			if (strings == null)
				return null;

			string[] strArray = strings.ToArray();

			// If you try to use an empty List<SqlDataRecord> for a table valued param, you'll get this error:
			// Message: There are no records in the SqlDataRecord enumeration. To send a table-valued parameter with no rows, use a null reference for the value instead.
			// So we return null here instead, if that's the case
			if (strArray.Length == 0)
				return null;

			List<SqlDataRecord> list = new List<SqlDataRecord>();

			// Create an SqlMetaData object that describes our table type.
			SqlMetaData[] tvp_definition = { new SqlMetaData(colName, SqlDbType.VarChar) };

			// Loop over the strings.
			foreach (string s in strArray)
			{
				// Create a new record, using the metadata array above.
				SqlDataRecord rec = new SqlDataRecord(tvp_definition);
				// Set the value and add it to the list
				rec.SetString(0, s);
				list.Add(rec);
			}
			return list;
		}

		/// <summary>
		/// This is specific to SQL Server.
		/// </summary>
		public static SqlParameter IntListParam(string paramName, IEnumerable<int> ints, string intColName)
		{
			SqlParameter param = new SqlParameter(paramName, IntsAsList(ints, intColName))
			{
				SqlDbType = SqlDbType.Structured,
				Direction = ParameterDirection.Input,
				TypeName = "int_list"
			};
			return param;
		}
#endif

	}
}
