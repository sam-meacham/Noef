// Noef single cs file distribution
// Generated: 1/6/2012 9:29:05 AM
// Root namespace: Noef.UnitTests
// SQL CE support: True
// SQL Server Table Valued Params support: False

#define SQL_CE

using System;
using System.Web;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Security.Permissions;
using System.Runtime.Remoting.Messaging;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Noef.UnitTests.Hyper;

#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef.UnitTests
{
//
// ColumnMetadata.cs
//
	/// <summary>
	/// The metadata for a column in SQL Server. The name of the column, the corresponding .NET type that most closely matches the SQL type,
	/// etc.  Also contains a PropertyDescriptor for the corresponding property in the class that represents the table that this column is in. Very handy.
	/// </summary>
	public class ColumnMetadata
	{
		public string Name { get; set; }
		public Type Type { get; set; }
		public PropertyDescriptor Property { get; set; }

		public ColumnMetadata(PropertyDescriptor property)
		{
			Name = property.Name;
			Type = property.PropertyType;
			Property = property;
		}
	}

//
// DbType.cs
//
	public enum DbType
	{
		SqlServer2012, // I'm putting this in here prematurely, because 2012 has some awesome new paging features that make things really easy
		SqlServer,
		SqlCE
	}

//
// HydrateException.cs
//
	public class HydrateException : Exception
	{
		public HydrateException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		public HydrateException(string message) : base(message)
		{
		}
	}

//
// HydrateExtensions.cs
//
	public static class HydrateExtensions
	{
		// ********************************************************************************
		// *** object hydrators ***********************************************************
		// *** (these are extension methods for object[] and IList<object[]>) *************
		// ********************************************************************************

		public static IList<object[]> GetRows(this IDbCommand cmd)
		{
			IDataReader reader = cmd.ExecuteReader();
			IList<object[]> rows = reader.GetRows();
			reader.Close();
			return rows;
		}

		public static RawTable GetRawTable(this IDbCommand cmd, int startingColumn = 0)
		{
			IDataReader reader = cmd.ExecuteReader();
			RawTable table = reader.GetRawTable(startingColumn);
			reader.Close();
			return table;
		}

		/// <summary>
		/// Hydrate a single object from an object[] that contains the row's values.
		/// Hydrating into a string or primitive type will simply use a cast to type T.
		/// Hydrating into a user or DTO type, the TableMetadata entry will be used.  But since a startingColumn index was provided, the ColumnMetadata entries are NOT used.
		/// This means it doesn't matter what the fields are NAMED in the result set.  It only matters that they are in the same order as the properties in the user or DTO type.
		/// (Unit tests: HydrateTests.HydrateSingleString_FromRow_1, 2 and 3 - tests overloads as well)
		/// (Unit tests: HydrateTests.HydrateSingleObject_FromRow_1, 2 and 3 - tests overloads as well)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="row"></param>
		/// <param name="startingColumn"></param>
		/// <param name="pkColumn"></param>
		/// <returns></returns>
		public static T Hydrate<T>(this object[] row, int startingColumn = 0, int pkColumn = 0)
		{
			Type type = typeof(T);
			if (row == null)
				throw new HydrateException("Error hydrating to type " + type.Name + ". row cannot be null");

			// If the PK column is null, return null (this should only be the case with LEFT/RIGHT joins, where there was no corresponding record in the join)
			// Note that we're operating on an assumption here, which becomes a de-facto convention of Noef.  YOUR DATA MUST HAVE PKS.
			if (row[pkColumn] == DBNull.Value)
				return default(T);

			T obj;
			// Is this a built in system type?
			if (type.Module.Name == "mscorlib.dll")
			{
				if (type == typeof(string) || type.IsPrimitive)
					obj = (T)row[startingColumn];
				else
					throw new Exception("Unsupported type for hydration");
			}
			else
			{
				// A custom user type.
				// Note that we don't use the ColumnMetadata entries. We are just accessing the fields ordinally, so the order of the result set MUST match
				// the order of the properties in the user or dto type.
				try
				{
					obj = (T)Activator.CreateInstance(typeof(T), true);
				}
				catch (Exception ex)
				{
					throw new HydrateException("An object of type " + type.Name + " could not be created. Check the inner exception for details.", ex);
				}

				TableMetadata tmeta = TableMetadata.For<T>();
				int i = 0;
				try
				{
					for (i = 0; i < tmeta.Columns.Count; i++)
						tmeta.Columns[i].Property.SetValue(obj, row[i + startingColumn].NullOrValue());
				}
				catch (Exception ex)
				{
					// Tread carefully here, I don't want to cause another exception, so let's look for all possibilities
					// Valid TableMetadata entry?
					if (tmeta == null)
						throw new HydrateException("The TableMetadata entry for type " + type.Name + " is null.", ex);
					// Were the ColumnMetadatas created for this TableMetadata entry?
					if (tmeta.Columns == null)
						throw new HydrateException("The TableMetadata entry for type " + type.Name + " has a null Columns list", ex);
					// Valid ColumnMetadata entry?
					ColumnMetadata colMeta = tmeta.Columns[i];
					if (colMeta == null)
						throw new HydrateException("Error setting the properties of " + type.Name + ". The ColumnMetadata entry at index " + i + " is null", ex);
					// Valid PropertyDescriptor for this ColumnMetadata entry?
					PropertyDescriptor prop = colMeta.Property;
					if (prop == null)
						throw new HydrateException("Error setting the properties of " + type.Name + ". The PropertyDescriptor for the ColumnMetadata entry at index " + i + " (" + colMeta.Name + ") is null", ex);
					// simple index out of range error? (for the source row object[])
					if (i > row.Length)
						throw new HydrateException("Error setting " + type.Name + "." + prop.Name + ". Your source row (an object[]) has " + row.Length
							+ " elements. The " + prop.Name + " property is at index " + i + " in " + type.Name + " (zero-based). You specified " + startingColumn
							+ " as your startingColumn, so you're trying to access element " + (i + startingColumn) + " in your source row, which is out of range."
							+ " Check your startingColumn offset, and make sure your result set (rows) matches the target type you are trying to hydrate into."
							+ " Make sure you're not using \"SELECT *\", and that your Dto classes are up to date (read: match the fields in the database).", ex);
					// At this point, we have a valid TableMetadata, ColumnMetadata, and PropertyDescriptor, and a valid value in our source row (object[]).
					// null value -> non-null property?
					object val = row[i + startingColumn];
					if (prop.PropertyType.IsValueType && val == null)
						throw new HydrateException("Error setting " + type.Name + "." + prop.Name + ". Your source row has a null value at index " + (i + startingColumn)
							+ " (you specified a startingColumn of " + startingColumn + " and " + prop.Name + " is property #" + i + " in " + type.Name + " (zero-based))."
							+ type.Name + "." + prop.Name + " is a value type and cannot be assigned null."
							+ " Check your startingColumn offset, and make sure your result set (rows) matches the target type you are trying to hydrate into."
							+ " Make sure you're not using \"SELECT *\", and that your Dto classes are up to date (read: match the fields in the database)."
							+ " You may have a data integrity issue in your database as well (a NULL value in a non-nullable field). If that is the case, either fix it"
							+ " in the database, or make the property in the Dto class nullable", ex);
					// Not sure what else could have gone wrong...
					throw new HydrateException("Error setting " + type.Name + "." + prop.Name + ". startingColumn = " + startingColumn
						+ ", i = " + i + " (that's the property offset for " + prop.Name + "), row[i + startingColumn] = " + val
						+ ". See the inner exception for details", ex);
				}
			}
			return obj;
		}

		public static IList<T> HydrateList<T>(this IList<object[]> rows, int startingColumn = 0, int pkColumn = 0)
		{
			return rows
				.Select(row => row.Hydrate<T>(startingColumn, pkColumn))
				.ToList();
		}

		public static IList<T> HydrateNonNullList<T>(this IList<object[]> rows, int startingColumn = 0, int pkColumn = 0)
		{
			return rows
				.Select(row => row.Hydrate<T>(startingColumn, pkColumn))
				.Where(obj => obj != null)
				.ToList();
		}

		/// <summary>
		/// Hydrates a unique/distict list of objects, determining uniqueness based on the value of the keyIndex field.
		/// This does not reject null values from the resulting list that's returned.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="rows"></param>
		/// <param name="keyIndex"></param>
		/// <param name="startingColumn"></param>
		/// <param name="pkColumn"></param>
		/// <returns></returns>
		public static IList<T> HydrateUniqueList<T>(this IList<object[]> rows, int keyIndex, int startingColumn = 0, int pkColumn = 0)
		{
			List<T> list = new List<T>();
			List<object> keys = new List<object>();
			foreach(object[] row in rows)
			{
				object curKey = row[keyIndex];
				// Make sure we're encountering a new key
				if (!keys.Any(k => Equals(k, curKey)))
				{
					// Previously unencountered key.  Hydrate the object, add it to the list, and add the key to the list of encountered keys.
					list.Add(row.Hydrate<T>(startingColumn, pkColumn));
					keys.Add(curKey);
				}
			}
			return list;
		}


		/// <summary>
		/// This returns a list of lists.  Each list contains all objects that had the same value in the "keyIndex" column.
		/// Null values are not included in the lists.
		/// This is useful for 1-* queries.
		/// </summary>
		/// <typeparam name="T">The type of object to hydrate into. String, primitive type, or user/dto type.</typeparam>
		/// <param name="rows">The "raw table" (list of object[]) that contains the result set values to hydrate from.</param>
		/// <param name="keyIndex">This is what the lists will be "split" on.  Each list (in the list of lists) contains all objects that have the same value in this column.</param>
		/// <param name="startingColumn">The column where your object of type T starts in the result set. Note that because this method is using the starting column, for user/DTO types,
		/// the ColumnMetadata entries will NOT be used. This means that the columns must be in the same order (ordinal position) as the properties of your user/dto type.</param>
		/// <param name="pkColumn">The value in this column will be used to determine if an object should be hydrated (non-null value in the column), or if a null value is returned (DBNull in the column)</param>
		/// <returns>A list of lists, each list containing all objects that had the same value in the "keyIndex" column.</returns>
		public static IDictionary<object, IList<T>> HydrateNonNullListPerKey<T>(this IList<object[]> rows, int keyIndex, int startingColumn = 0, int pkColumn = 0)
		{
			return HydrateListPerKey<T>(rows, keyIndex, false, startingColumn, pkColumn);
		}


		/// <summary>
		/// This returns a list of lists.  Each list contains all objects that had the same value in the "keyIndex" column.
		/// This is useful for 1-* queries.
		/// </summary>
		/// <typeparam name="T">The type of object to hydrate into. String, primitive type, or user/dto type.</typeparam>
		/// <param name="rows">The "raw table" (list of object[]) that contains the result set values to hydrate from.</param>
		/// <param name="keyIndex">This is what the lists will be "split" on.  Each list (in the list of lists) contains all objects that have the same value in this column.</param>
		/// <param name="startingColumn">The column where your object of type T starts in the result set. Note that because this method is using the starting column, for user/DTO types,
		/// the ColumnMetadata entries will NOT be used. This means that the columns must be in the same order (ordinal position) as the properties of your user/dto type.</param>
		/// <param name="pkColumn">The value in this column will be used to determine if an object should be hydrated (non-null value in the column), or if a null value is returned (DBNull in the column)</param>
		/// <returns>A list of lists, each list containing all objects that had the same value in the "keyIndex" column.</returns>
		public static IDictionary<object, IList<T>> HydrateListPerKey<T>(this IList<object[]> rows, int keyIndex, int startingColumn = 0, int pkColumn = 0)
		{
			return HydrateListPerKey<T>(rows, keyIndex, true, startingColumn, pkColumn);
		}


		/// <summary>
		/// This returns a list of lists.  Each list contains all objects that had the same value in the "keyIndex" column.
		/// This is useful for 1-* queries.
		/// </summary>
		/// <typeparam name="T">The type of object to hydrate into. String, primitive type, or user/dto type.</typeparam>
		/// <param name="rows">The "raw table" (list of object[]) that contains the result set values to hydrate from.</param>
		/// <param name="keyIndex">This is what the lists will be "split" on.  Each list (in the list of lists) contains all objects that have the same value in this column.</param>
		/// <param name="includeNulls">Whether to include an object in the list, if the value in the "pkColumn" column is null (if so, the object will NOT be an "empty" object with default values, it will be null)</param>
		/// <param name="startingColumn">The column where your object of type T starts in the result set. Note that because this method is using the starting column, for user/DTO types,
		/// the ColumnMetadata entries will NOT be used. This means that the columns must be in the same order (ordinal position) as the properties of your user/dto type.</param>
		/// <param name="pkColumn">The value in this column will be used to determine if an object should be hydrated (non-null value in the column), or if a null value is returned (DBNull in the column)</param>
		/// <returns>A list of lists, each list containing all objects that had the same value in the "keyIndex" column.</returns>
		public static IDictionary<object, IList<T>> HydrateListPerKey<T>(this IList<object[]> rows, int keyIndex, bool includeNulls, int startingColumn = 0, int pkColumn = 0)
		{
			// The final list of lists that we'll return to the caller
			IDictionary<object, IList<T>> dict = new Dictionary<object, IList<T>>();

			foreach(object[] row in rows)
			{
				object curKey = row[keyIndex];
				// If we encounter a new "key" value, we need to create a new list in our dictionary
				if (!dict.ContainsKey(curKey))
					dict.Add(curKey, new List<T>());
				T obj = row.Hydrate<T>(startingColumn, pkColumn);
				if (includeNulls || obj != null)
					dict[curKey].Add(obj);
			}
			return dict;
		}


		// ********************************************************************************
		// *** reader hydrators ***********************************************************
		// *** (extension methods for IDataReader) ****************************************
		// ********************************************************************************

		public static IList<object[]> GetRows(this IDataReader reader)
		{
			List<object[]> rows = new List<object[]>();
			while (reader.Read())
			{
				object[] values = new object[reader.FieldCount];
				reader.GetValues(values);
				rows.Add(values);
			}
			return rows;
		}


		public static RawTable GetRawTable(this IDataReader reader, int startingColumn = 0)
		{
			int numFields = reader.FieldCount - startingColumn;
			RawTable table = new RawTable
			{
				Rows = new List<object[]>(),
				FieldNames = new string[numFields],
				FieldTypes = new Type[numFields]
			};

			for (int i = 0; i < numFields; i++)
			{
				table.FieldNames[i] = reader.GetName(i + startingColumn);
				table.FieldTypes[i] = reader.GetFieldType(i + startingColumn);
			}

			while (reader.Read())
			{
				object[] values = new object[numFields];
				if (startingColumn == 0)
				{
					reader.GetValues(values);
				}
				else
				{
					for (int i = 0; i < numFields; i++)
						values[i] = reader.GetValue(i + startingColumn);
				}
				table.Rows.Add(values);
			}
			return table;
		}


		/// <summary>
		/// Don't use this method for String or primitive types!
		/// This method should only be used to hydrate into a user or DTO type, since the type's TableMetadata entry will be used to fetch the column values by field name.
		/// Use the overload that takes the startingColumn or fieldName if you want to hydrate into a string or primitive type.
		/// There is no startingColumn index provided here, so the ColumnMetadata entries will be used to pull the values out of the reader's current row BY NAME.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader"></param>
		/// <param name="columnNamePrefix"></param>
		/// <param name="pkColumn"></param>
		/// <returns></returns>
		public static T HydrateByFieldName<T>(this IDataReader reader, string columnNamePrefix = "", int pkColumn = 0)
		{
			Type type = typeof(T);
			if (reader == null)
				throw new HydrateException("Error hydrating to type " + type.Name + ". reader cannot be null");

			// Make sure there is a valid row to read from
			if (!reader.Read())
				return default(T);

			// If the PK column is null, return null (this should only be the case with LEFT/RIGHT joins, where there was no corresponding record in the join)
			if (reader[pkColumn] == DBNull.Value)
				return default(T);

			if (type.Module.Name == "mscorlib.dll")
				throw new HydrateException("System types are not supported in this overload of Hydrate(). This is strictly for user/dto types that have TableMetadata entries (since this is hydrating by field name and not ordinal position)");

			// The object we will return
			T obj;
			try
			{
				obj = (T)Activator.CreateInstance(type, true);
			}
			catch (Exception ex)
			{
				throw new HydrateException("An object of type " + type.Name + " could not be created. Check the inner exception for details.", ex);
			}
			TableMetadata tmeta = TableMetadata.For<T>();
			int i = 0;
			try
			{
				for (i = 0; i < tmeta.Columns.Count; i++)
				{
					ColumnMetadata col = tmeta.Columns[i];
					col.Property.SetValue(obj, reader[columnNamePrefix + col.Name].NullOrValue());
				}
			}
			catch (Exception ex)
			{
				// Tread carefully here, I don't want to cause another exception, so let's look for all possibilities
				// Valid TableMetadata entry?
				if (tmeta == null)
					throw new HydrateException("The TableMetadata entry for type " + type.Name + " is null.", ex);
				// Were the ColumnMetadatas created for this TableMetadata entry?
				if (tmeta.Columns == null)
					throw new HydrateException("The TableMetadata entry for type " + type.Name + " has a null Columns list", ex);
				// Valid ColumnMetadata entry?
				ColumnMetadata colMeta = tmeta.Columns[i];
				if (colMeta == null)
					throw new HydrateException("Error setting the properties of " + type.Name + ". The ColumnMetadata entry at index " + i + " is null", ex);
				// Valid PropertyDescriptor for this ColumnMetadata entry?
				PropertyDescriptor prop = colMeta.Property;
				if (prop == null)
					throw new HydrateException("Error setting the properties of " + type.Name + ". The PropertyDescriptor for the ColumnMetadata entry at index " + i + " (" + colMeta.Name + ") is null", ex);
				// Valid column name in the reader? (we get the column name from the ColumnMetadata entry)
				List<string> colnames = GetFieldNames(reader);
				if (!colnames.Contains(colMeta.Name, StringComparer.InvariantCultureIgnoreCase))
					throw new HydrateException("Error setting " + type.Name + "." + prop.Name + ". The ColumnMetadata entry indicates that the field should be named " + colMeta.Name
						+ " (with a columnNamePrefix of \"" + columnNamePrefix + "\"), but no field with that name was found in the IDataReader. Check to see if there is a columnNamePrefix in the result set."
						+ " Make sure you're not using \"SELECT *\", and that your Dto classes are up to date (read: match the fields in the database)."
						+ " If you're using SELECT * and a column was removed in the database that still exists in the Dto class, hydrating will look for that field by name, and it will not find it.", ex);

				// At this point, we have a valid TableMetadata, ColumnMetadata, and PropertyDescriptor, and a valid value in our source reader.
				// null value -> non-null property?
				object val = reader[columnNamePrefix + colMeta.Name];
				if (prop.PropertyType.IsValueType && val == null)
					throw new HydrateException("Error setting " + type.Name + "." + prop.Name + ". Your source reader has a null value for the field named " + (columnNamePrefix + colMeta.Name)
						+ type.Name + "." + prop.Name + " is a value type and cannot be assigned null."
						+ " Make sure your reader matches the target type you are trying to hydrate into."
						+ " Make sure you're not using \"SELECT *\", and that your Dto classes are up to date (read: match the fields in the database)."
						+ " If you're using SELECT * and a column was removed in the database that still exists in the Dto class, hydrating will look for that field by name, and it will not find it."
						+ " You may have a data integrity issue in your database as well (a NULL value in a non-nullable field). If that is the case, either fix it"
						+ " in the database, or make the property in the Dto class nullable", ex);
				// Not sure what else could have gone wrong...
				throw new HydrateException("Error setting " + type.Name + "." + prop.Name + ". columnNamePrefix = " + columnNamePrefix
					+ ", i = " + i + ", ColumnMetadata name = " + colMeta.Name + ", reader field value = " + val + ". See the inner exception for details", ex);
			}
			return obj;
		}

		/// <summary>
		/// The name couldn't be just Hydrate because the signature conflicts with other Hydrate methods.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader"></param>
		/// <param name="pkColumn"></param>
		/// <param name="fieldName"></param>
		/// <returns></returns>
		public static T HydrateByFieldName<T>(this IDataReader reader, int pkColumn, string fieldName)
		{
			int columnIndex = reader.GetOrdinal(fieldName);
			return Hydrate<T>(reader, columnIndex, pkColumn);
		}

		public static T Hydrate<T>(this IDataReader reader, int startingColumn = 0, int pkColumn = 0)
		{
			// Make sure we can advance to the first (or next) row in the reader
			if (!reader.Read())
				return default(T);
			// Get a single row's values
			object[] values = new object[reader.FieldCount];
			reader.GetValues(values);
			// Hydrate that rows values into type T
			return values.Hydrate<T>(startingColumn, pkColumn);
		}

		/// <summary>
		/// (Unit tests: HydrateTests.HydrateListOfString_FromReader_1, 2 and 3 - tests overloads as well)
		/// (Unit tests: HydrateTests.HydrateListOfObject_FromReader_1, 2 and 3 - tests overloads as well)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader"></param>
		/// <param name="startingColumn"></param>
		/// <param name="pkColumn"></param>
		/// <returns></returns>
		public static IList<T> HydrateList<T>(this IDataReader reader, int startingColumn = 0, int pkColumn = 0)
		{
			IList<object[]> rows = reader.GetRows();
			return rows.HydrateList<T>(startingColumn, pkColumn);
		}


		public static List<string> GetFieldNames(this IDataReader reader)
		{
			List<string> names = new List<string>();
			for(int i=0; i < reader.FieldCount; i++)
				names.Add(reader.GetName(i));
			return names;
		}


	}

//
// MetadataClassAttribute.cs
//
	[AttributeUsage(AttributeTargets.Class)]
	public class MetadataClassAttribute : Attribute
	{
	}

//
// NoefDal.cs
//
	public abstract class NoefDal
	{
		private static readonly IDictionary<string, string> s_connectionStrings = new ConcurrentDictionary<string, string>();
		public abstract string ConnectionStringName { get; }
		public abstract DbType DbType { get; }
		private readonly string m_uniqueDalKey;

		protected NoefDal()
		{
			m_uniqueDalKey = GetType().FullName;
		}

		public virtual string GetConnectionString(string connectionStringName = null)
		{
			if (String.IsNullOrWhiteSpace(connectionStringName))
				connectionStringName = ConnectionStringName;
			if (s_connectionStrings.ContainsKey(connectionStringName))
				return s_connectionStrings[connectionStringName];
			ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[connectionStringName];
			if (settings == null)
				throw new Exception("No connection string found with name " + connectionStringName);
			s_connectionStrings.Add(connectionStringName, settings.ConnectionString);
			return settings.ConnectionString;
		}

		/// <summary>
		/// Connections are cached per-request.
		/// </summary>
		public virtual IDbConnection GetConnection(string connectionStringName = null)
		{
			if (String.IsNullOrWhiteSpace(connectionStringName))
				connectionStringName = ConnectionStringName;
			string key = m_uniqueDalKey + "_cn_" + connectionStringName;
			IDbConnection cn = (IDbConnection)PerRequestStore.GetData(key);
			if (cn == null)
			{
				string cnString = GetConnectionString(connectionStringName);
				switch(DbType)
				{
					case DbType.SqlServer2012:
					case DbType.SqlServer:
						cn = new SqlConnection(cnString);
						break;
#if SQL_CE
					case DbType.SqlCE:
						cn = new SqlCeConnection(cnString);
						break;
#endif

					default:
						throw new NotSupportedException("Your DbType (" + DbType + ") is not currently supported");
				}
				PerRequestStore.SetData(key, cn);
				cn.Open();
			}
			return cn;
		}

		public IDbCommand CreateStoredProc(string name, IDbConnection cn = null)
		{
			IDbCommand cmd;
			switch(DbType)
			{
				case DbType.SqlServer2012:
				case DbType.SqlServer:
					cmd = new SqlCommand(name) { CommandType = CommandType.StoredProcedure };
					break;

#if SQL_CE
				case DbType.SqlCE:
					cmd = new SqlCeCommand(name) { CommandType = CommandType.StoredProcedure };
					break;
#endif

				default:
					throw new NotSupportedException("Your DbType (" + DbType + ") is not currently supported");
			}
			cmd.Connection = cn ?? GetConnection();
			return cmd;
		}

		public IDbCommand CreateCommand(string sql, object sqlParams = null, IDbConnection cn = null)
		{
			IDbCommand cmd;

			switch(DbType)
			{
				case DbType.SqlServer2012:
				case DbType.SqlServer:
					cmd = new SqlCommand(sql);
					break;

#if SQL_CE
				case DbType.SqlCE:
					cmd = new SqlCeCommand(sql);
					break;
#endif

				default:
					throw new NotSupportedException("Your DbType (" + DbType + ") is not currently supported");
			}
			cmd.Connection = cn ?? GetConnection();
			cmd.AddParams(sqlParams);
			return cmd;
		}

		public int ExecuteNonQuery(string sql, object sqlParams = null, IDbConnection cn = null)
		{
			IDbCommand cmd = CreateCommand(sql, sqlParams, cn);
			return cmd.ExecuteNonQuery();
		}

		public object ExecuteScalar(string sql, object sqlParams = null, IDbConnection cn = null)
		{
			IDbCommand cmd = CreateCommand(sql, sqlParams, cn);
			return cmd.ExecuteScalar();
		}

		public IList<object[]> GetRows(string sql, object sqlParams = null, IDbConnection cn = null)
		{
			IDbCommand cmd = CreateCommand(sql, sqlParams, cn);
			IDataReader reader = cmd.ExecuteReader();
			IList<object[]> rows = reader.GetRows();
			reader.Close();
			return rows;
		}

		public RawTable Query(string sql, object sqlParams = null, IDbConnection cn = null, int startingColumn = 0)
		{
			IDbCommand cmd = CreateCommand(sql, sqlParams, cn);
			IDataReader reader = cmd.ExecuteReader();
			RawTable table = reader.GetRawTable(startingColumn);
			reader.Close();
			return table;
		}

		public IList<T> Query<T>(string sql, object sqlParams = null, IDbConnection cn = null, int startingColumn = 0, int pkColumn = 0)
		{
			IDbCommand cmd = CreateCommand(sql, sqlParams, cn);
			IDataReader reader = cmd.ExecuteReader();
			IList<T> rows = reader.HydrateList<T>(startingColumn, pkColumn);
			reader.Close();
			return rows;
		}

		public IList<T> Unique<T>(string sql, int keyIndex, object sqlParams = null, IDbConnection cn = null, int startingColumn = 0, int pkColumn = 0)
		{
			IDbCommand cmd = CreateCommand(sql, sqlParams, cn);
			IDataReader reader = cmd.ExecuteReader();
			IList<object[]> rawRows = reader.GetRows();
			IList<T> rows = rawRows.HydrateUniqueList<T>(keyIndex, startingColumn, pkColumn);
			reader.Close();
			return rows;
		}

		public T SingleOrDefault<T>(string sql, object sqlParams = null, IDbConnection cn = null, int startingColumn = 0, int pkColumn = 0)
		{
			IDbCommand cmd = CreateCommand(sql, sqlParams, cn);
			IDataReader reader = cmd.ExecuteReader();
			T record = reader.Hydrate<T>(startingColumn, pkColumn);
			reader.Close();
			return record;
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

		private void buildPageQueries(int skip, int take, string originalSql, out string sqlCount, out string sqlPage) 
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
		public PagedData<T> Page<T>(int pageIndex, int pageSize, string sql, object sqlParams, IDbConnection cn = null)
		{
			if (cn == null)
				cn = GetConnection();
			string sqlCount, sqlPage;
			buildPageQueries(pageIndex * pageSize, pageSize, sql, out sqlCount, out sqlPage);

			// Get the records and the total count (we do startingColum=1 to skip the row number column)
			IList<T> data = Query<T>(sqlPage, sqlParams, cn, 1);
			int totalRecords = (int)ExecuteScalar(sqlCount, sqlParams);

			// Create the PagedData object and return it
			PagedData<T> result = new PagedData<T>(data, pageSize, pageIndex, totalRecords);
			return result;
		}

		// Fetch a page (non-generic version)
		public PagedData Page(int pageIndex, int pageSize, string sql, object sqlParams, IDbConnection cn = null)
		{
			if (cn == null)
				cn = GetConnection();
			string sqlCount, sqlPage;
			buildPageQueries(pageIndex * pageSize, pageSize, sql, out sqlCount, out sqlPage);

			// Get the records and the total count (we do startingColum=1 to skip the row number column)
			RawTable rawTable = Query(sqlPage, sqlParams, cn, 1);
			int totalRecords = (int)ExecuteScalar(sqlCount, sqlParams);

			// Create the PagedData object and return it
			PagedData result = new PagedData(rawTable, pageSize, pageIndex, totalRecords);
			return result;
		}


		// ********************************************************************************
		// *** CRUD helpers (select, insert, update, delete) ******************************
		// ********************************************************************************

		/// <summary>
		/// Constructs and executes a SELECT statement based on the table, name of the PK column, and the PK value provided.
		/// Returns a single result: the matching record, or null if not found.
		/// </summary>
		public T Select<T>(string pkColumn, object pkValue, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null)
		{
			if (cn == null)
				cn = GetConnection();
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = String.Format("SELECT {0} FROM {1} WHERE {2} = @{2}", TableMetadata.GetColumnsString<T>(), tmeta.Name, pkColumn);
			IDbCommand cmd = CreateCommand(sql, null, cn);
			cmd.AddParam("@" + pkColumn, pkValue);
			if (beforeExecute != null)
				beforeExecute(cmd);
			IDataReader reader = cmd.ExecuteReader();
			T obj = reader.Hydrate<T>();
			reader.Close();
			return obj;
		}


		/// <summary>
		/// Constructs and executes a SELECT statement based on the table and a user WHERE clause.
		/// If you want to parameterize the IDbCommand, use beforeExecute modify it however you want.
		/// Example: Select(cn, "col = @col", cmd => cmd.AddParam("@col", someValue))
		/// This is not useful if you want to do joins.  This is only for single objects of a type.
		/// </summary>
		/// <returns>An IList{T} of the results (empty if no results)</returns>
		public IList<T> Select<T>(string where, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null)
		{
			if (cn == null)
				cn = GetConnection();
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = String.Format("SELECT {0} FROM {1} WHERE {2}", TableMetadata.GetColumnsString<T>(), tmeta.Name, where);
			IDbCommand cmd = CreateCommand(sql, null, cn);
			if (beforeExecute != null)
				beforeExecute(cmd);
			IDataReader reader = cmd.ExecuteReader();
			IList<T> list = reader.HydrateList<T>();
			reader.Close();
			return list;
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
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database (fields only, no collections. This is not an ORM, duh)</returns>
		public void Update<T>(T obj, string pkColumn, IDictionary<string, string> columnUpdateOverrides, IEnumerable<string> excludedColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null)
		{
			if (cn == null)
				cn = GetConnection();
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
				if (String.Equals(col.Name, pkColumn, StringComparison.InvariantCultureIgnoreCase))
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
			sb.AppendLine(String.Join(",", sets));
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);
			sb.AppendLine();
			sb.AppendLine("-- Return the updated record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);

			// Set up the command
			IDbCommand cmd = CreateCommand(sb.ToString(), null, cn);
			// Add the pk param
			cmd.AddParam("@" + pkColumn, tmeta.GetColumn(pkColumn).Property.GetValue(obj));

			// Add the remaining params
			foreach (ColumnMetadata col in tmeta.Columns)
			{
				// Don't add the pk column, it's already been added.
				if (String.Equals(col.Name, pkColumn, StringComparison.InvariantCultureIgnoreCase))
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
			IDataReader reader = cmd.ExecuteReader();

			// Get the updated object
			T updatedObj = reader.Hydrate<T>();
			reader.Close();

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
		/// Constructs and executes an UPDATE statement based on the table specified by T (using the column metadata).
		/// A single row will be updated using a WHERE clause based on the pkColumn name (the value will be taken from the obj parameter, getting the correct
		/// property's value, also based on the value of pkColumn).
		/// ONLY columns specified in the whitelist will be updated.
		/// Each column in the table will be updated with the corresponding property in "obj". No overrides in this method, this is for very simple updates.
		/// </summary>
		/// <typeparam name="T">The class that represents the table in the database that you want to update a record for</typeparam>
		/// <param name="obj">The object representing the record you want to update</param>
		/// <param name="cn">The connection to use</param>
		/// <param name="pkColumn">The name of the column that is the primary key. No support for composite keys.</param>
		/// <param name="whitelistColumns">The columns you want to be updated (will be in the SET columns list)</param>
		/// <param name="beforeExecute"></param>
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database, including columns that were
		/// not in the update statement (triggers, who knows)</returns>
		public void Update<T>(T obj, string pkColumn, IEnumerable<string> whitelistColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null)
		{
			if (cn == null)
				cn = GetConnection();
			TableMetadata tmeta = TableMetadata.For<T>();
			StringBuilder sb = new StringBuilder();

			List<string> whitelist = whitelistColumns == null ? new List<string>() : whitelistColumns.ToList();

			// Set up the sql statement
			sb.AppendLine("UPDATE " + tmeta.Name);
			sb.AppendLine("SET");
			List<string> sets = new List<string>();
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				// TODO: potentially more efficient to loop through the whitelist columns, instead of all columns and checking for membership in the list.
				if (!whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
					continue;
				sets.Add(col.Name + " = @" + col.Name);
			}
			sb.AppendLine(String.Join(",", sets));
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);
			sb.AppendLine();
			sb.AppendLine("-- Return the updated record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + pkColumn + " = @" + pkColumn);

			// Set up the command
			IDbCommand cmd = CreateCommand(sb.ToString(), null, cn);
			// Add the pk param
			cmd.AddParam("@" + pkColumn, tmeta.GetColumn(pkColumn).Property.GetValue(obj));

			// Add the remaining params
			foreach (ColumnMetadata col in tmeta.Columns)
			{
				// Don't add the pk column, it's already been added
				if (String.Equals(col.Name, pkColumn, StringComparison.InvariantCultureIgnoreCase))
					continue;
				if (!(String.Equals(col.Name, pkColumn, StringComparison.InvariantCultureIgnoreCase)
				      || !whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase)))
					continue;
				cmd.AddParam("@" + col.Name, col.Property.GetValue(obj));
			}

			// Execute the command
			if (beforeExecute != null)
				beforeExecute(cmd);
			IDataReader reader = cmd.ExecuteReader();

			// Get the updated object
			T updatedObj = reader.Hydrate<T>();
			reader.Close();

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
		public void Insert<T>(T obj, string pkColumn, IDictionary<string, string> columnInsertOverrides, IEnumerable<string> excludedColumns, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null, string newRecordIdSelector = "SCOPE_IDENTITY()")
		{
			if (cn == null)
				cn = GetConnection();
			TableMetadata tmeta = TableMetadata.For<T>();
			StringBuilder sb = new StringBuilder();

			List<string> excluded = excludedColumns == null ? new List<string>() : excludedColumns.ToList();

			// Set up the sql statement
			sb.AppendLine("INSERT INTO " + tmeta.Name);

			IEnumerable<string> insertCols = tmeta.Columns
				.Where(col => !excluded.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
				.Select(c => c.Name);

			sb.AppendLine("(" + String.Join(",", insertCols) + ")");
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
			sb.Append(String.Join(",", insertVals));
			sb.AppendLine(")");

			sb.AppendLine("-- Return the inserted record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + pkColumn + " = " + newRecordIdSelector);

			// Set up the command
			IDbCommand cmd = CreateCommand(sb.ToString(), null, cn);
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
			IDataReader reader = cmd.ExecuteReader();

			// Get the inserted object
			T insertedObj = reader.Hydrate<T>();
			reader.Close();

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
		/// Will delete 1 row, based on a WHERE clause that uses pkColumn as the column to restrict by (so you could potentially
		/// delete a range of values, if you used a column other than the PK.  Useful, but be careful).
		/// The value of the pkColumn is taken from "obj" (finding the property that corresponds to the PK column specified, and taking its value).
		/// Returns the number of affected (deleted) records.
		/// </summary>
		public int Delete<T>(T obj, string pkColumn, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null)
		{
			if (cn == null)
				cn = GetConnection();
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = "DELETE FROM " + tmeta.Name + " WHERE " + pkColumn + " = @" + pkColumn;
			IDbCommand cmd = CreateCommand(sql, null, cn);

			// Find the column the user indicated by name
			ColumnMetadata col = tmeta.Columns.First(c => String.Equals(c.Name, pkColumn, StringComparison.InvariantCultureIgnoreCase));

			object pkValue = col.Property.GetValue(obj);
			cmd.AddParam("@" + pkColumn, pkValue);
			if (beforeExecute != null)
				beforeExecute(cmd);
			return cmd.ExecuteNonQuery();
		}


		/// <summary>
		/// Constructs and executes a DELETE statement based on the table specified by T (using the column metadata).
		/// Will delete 1 row, based on a WHERE clause that uses keyColumn and key values specified by the caller.
		/// Returns the number of affected (deleted) records.
		/// </summary>
		public int Delete<T>(string keyColumn, object key, IDbConnection cn = null, Action<IDbCommand> beforeExecute = null)
		{
			if (cn == null)
				cn = GetConnection();
			TableMetadata tmeta = TableMetadata.For<T>();
			string sql = "DELETE FROM " + tmeta.Name + " WHERE " + keyColumn + " = @" + keyColumn;
			IDbCommand cmd = CreateCommand(sql, null, cn);
			cmd.AddParam("@" + keyColumn, key);
			if (beforeExecute != null)
				beforeExecute(cmd);
			return cmd.ExecuteNonQuery();
		}



	}

//
// ObjectExtensions.cs
//
	public static class ObjectExtensions
	{
		/// <summary>
		/// Convert an anonymous object into a Dictionary{string, object}.
		/// Ex: new { id = 1, name = "Sam" } would be turned into a dictionary: { {"id", 1}, {"name", "Sam"} }
		/// </summary>
		public static IDictionary<string, object> ToDictionary(this object data)
		{
			return data
				.GetType()
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(property => property.CanRead)
				.ToDictionary(property => property.Name, property => property.GetValue(data, null));
		}

		/// <summary>
		/// If val is null, returns DBNull.Value, otherwise it just returns the val.
		/// </summary>
		public static object NullOrValue(this object val)
		{
			return (val == DBNull.Value) ? null : val;
		}
	}

//
// PagedData.cs
//
	public class PagedData<T>
	{
		public int PageSize { get; set; }
		public int PageIndex { get; set; }
		public int TotalPages { get; set; }
		public int TotalRecords { get; set; }
		public IEnumerable<T> Data;

		/// <summary>
		/// In case you want to pass something else along with your page
		/// </summary>
		public object Context { get; set; }

		public PagedData(IEnumerable<T> data, int pageSize, int pageIndex, int totalRecords)
		{
			Data = data;
			PageSize = pageSize;
			PageIndex = pageIndex;
			TotalRecords = totalRecords;
			TotalPages = (int) Math.Ceiling((double) TotalRecords/PageSize);
		}

		// With serialization, it's a good idea to have a parameterless constructor
		public PagedData()
		{ }
	}

	/// <summary>
	/// A non-generic version that uses a RawTable instead of IEnumerable{T} for storing the data.
	/// </summary>
	public class PagedData
	{
		public int PageSize { get; set; }
		public int PageIndex { get; set; }
		public int TotalPages { get; set; }
		public int TotalRecords { get; set; }
		public RawTable Table;

		/// <summary>
		/// In case you want to pass something else along with your page
		/// </summary>
		public object Context { get; set; }

		public PagedData(RawTable table, int pageSize, int pageIndex, int totalRecords)
		{
			Table = table;
			PageSize = pageSize;
			PageIndex = pageIndex;
			TotalRecords = totalRecords;
			TotalPages = (int) Math.Ceiling((double) TotalRecords/PageSize);
		}

		// With serialization, it's a good idea to have a parameterless constructor
		public PagedData()
		{ }
	}

//
// PerRequestSingleton.cs
//
	/// <summary>
	/// A singleton whose storage is based on HttpContext.Current.Items, meaning the instance is unique
	/// per user, per request (and is discarded once the response is sent, since the current HttpContext goes away)
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class PerRequestSingleton<T>
	{
		private static readonly object s_objSync = new object();

		public static T Instance
		{
			get
			{
				string key = typeof(T).FullName;
				// First check
				if (PerRequestStore.GetData(key) == null)
				{
					// lock
					lock (s_objSync)
					{
						// second check
						if (PerRequestStore.GetData(key) == null)
						{
							T instance = (T)Activator.CreateInstance(typeof(T), true);
							PerRequestStore.SetData(key, instance);
						}
					}
				}

				return (T) PerRequestStore.GetData(key);
			}
		}

	}

//
// PerRequestStore.cs
//
	/// <summary>
	/// HttpContext.Current.Items is the perfect place to store items that you want to be unique per user (each
	/// user of the ASP.NET app has their own copy of the storage), and that you also want to be limited to the
	/// lifetime of the current request.
	///
	/// This storage class will utilize HttpContext.Current.Items if it's available.  If it's not (console app, unit tests, etc),
	/// it falls back on CallContext for storage.
	/// </summary>
	public static class PerRequestStore
	{
		public static object GetData(string key)
		{
			if (HttpContext.Current != null)
				return HttpContext.Current.Items[key];
			return CallContext.GetData(key);
		}


		public static void SetData(string key, object data)
		{
			if (HttpContext.Current != null)
				HttpContext.Current.Items[key] = data;
			else
				CallContext.SetData(key, data);
		}

	}

//
// RawTable.cs
//
	/// <summary>
	/// Sometimes you had adhoc queries that you don't want to create specific user/dto types to represent, because they are seldomly used.
	/// You can use the non-generic methods in NoefDal (such as Query()) and the results of your query will simply be dumped into a RawTable.
	/// You get a simple IList{object[]} to represent your rows and columns, and some metadata that tells you the field names and types.
	/// </summary>
	public class RawTable
	{
		public IList<object[]> Rows { get; set; }
		public string[] FieldNames { get; set; }
		public Type[] FieldTypes { get; set; }
	}

//
// ReflectionHelper.cs
//
	public static class ReflectionHelper
	{
		public static IEnumerable<Type> GetTypesWithAttribute(Assembly assembly, Type attributeType)
		{
			return assembly.GetTypes().Where(type => type.GetCustomAttributes(attributeType, true).Length > 0);
		}

		public static IEnumerable<MethodInfo> GetMethodsWithAttribute(Type classType, Type attributeType)
		{
			return classType.GetMethods().Where(methodInfo => methodInfo.GetCustomAttributes(attributeType, true).Length > 0);
		}

		public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute(Type classType, Type attributeType)
		{
			return classType.GetProperties().Where(propertyInfo => propertyInfo.GetCustomAttributes(attributeType, true).Length > 0);
		}


		public static T GetAttribute<T>(MethodInfo method)
			where T : Attribute
		{
			T[] attribs = (T[])method.GetCustomAttributes(typeof(T), true);
			return attribs.SingleOrDefault();
		}


		public static T GetAttribute<T>(Type @class)
			where T : Attribute
		{
			T[] attribs = (T[])@class.GetCustomAttributes(typeof(T), true);
			return attribs.SingleOrDefault();
		}


		public static T GetAttribute<T>(MemberInfo member)
			where T : Attribute
		{
			T[] attribs = (T[])member.GetCustomAttributes(typeof(T), true);
			return attribs.SingleOrDefault();
		}


		public static void ApplyValues(object obj, IEnumerable<KeyValuePair<string, object>> values, bool throwOnBadProp = true)
		{
			Type type = obj.GetType();
			TableMetadata tmeta = TableMetadata.For(type);
			foreach(KeyValuePair<string, object> pair in values)
			{
				try
				{
					PropertyDescriptor prop = tmeta.Properties[pair.Key];
					if (prop == null && throwOnBadProp)
						throw new Exception("Invalid property name " + pair.Key + " for type + " + type.Name);
					if (prop == null)
						// Bad property, but the user specified to not throw an exception, so just skip this one
						continue;

					bool isNullable = prop.PropertyType.IsGenericType
						&& prop.PropertyType.FullName != null
						&& prop.PropertyType.FullName.StartsWith("System.Nullable");

					if (!prop.PropertyType.IsValueType && pair.Value == null)
					{
						prop.SetValue(obj, null);
					}
					else if (prop.PropertyType.IsAssignableFrom(pair.Value.GetType())
						|| (isNullable && prop.PropertyType.GetGenericArguments()[0].IsAssignableFrom(pair.Value.GetType())))
					{
						prop.SetValue(obj, pair.Value);
					}
					else if (isNullable)
					{
						Type actualType = prop.PropertyType.GetGenericArguments()[0];
						prop.SetValue(obj, Convert.ChangeType(pair.Value, actualType));
					}
					else
					{
						prop.SetValue(obj, Convert.ChangeType(pair.Value, prop.PropertyType));
					}
				}
				catch (Exception)
				{
					if (throwOnBadProp)
						throw;
				}
			}
		}

	}

//
// SqlCommandExtensions.cs
//
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

//
// StaticStore.cs
//
	/// <summary>
	/// This is a static cache, so if it's used in ASP.NET, all data is shared in the single w3wp.exe process, and is shared by all users.
	/// Initialized once, when the app starts.
	/// Will be reset if the app pool is reinitialized.
	/// IIS *CAN* start additional w3wp.exe processes for the same app, to handle high volume, so be aware of that.
	/// </summary>
	public static class StaticStore
	{
		private static readonly ConcurrentDictionary<string, object> s_cache = new ConcurrentDictionary<string, object>();

		public static object GetData(string key)
		{
			if (s_cache.ContainsKey(key))
				return s_cache[key];
			return null;
		}

		public static void SetData(string key, object data)
		{
			s_cache[key] = data;
		}

	}

//
// StringExtensions.cs
//
	public static class StringExtensions
	{
		public static string NullIfBlank(this string s)
		{
			return String.IsNullOrWhiteSpace(s) ? null : s;
		}
	}

//
// TableMetadata.cs
//
	/// <summary>
	/// An instance of this class contains the metadata for a table in SQL Server (the name of the table, and all of the column metadata).
	/// This class also has many static members: s_tables, which is the metadata for all tables, and the bulk of the static helper methods that
	/// are used to help construct and execute SQL statements.
	/// </summary>
	public class TableMetadata
	{
		// ********************************************************************************
		// *** Static properties and methods **********************************************
		// ********************************************************************************

		/// <summary>
		/// The metadata on all of the classes registered via <see cref="AddTables" />.
		/// </summary>
		private static readonly IDictionary<Type, TableMetadata> s_tables = new ConcurrentDictionary<Type, TableMetadata>();

		// Used for verifying the string after an ORDER BY.  Splits the column name from an optional ASC or DESC, grouping them both for capture.
		private static readonly Regex RX_COL = new Regex(@"^(\w+)(?:\s+|$)(DESC|ASC)?$", RegexOptions.IgnoreCase);

		public static void AddTable(TableMetadata table)
		{
			s_tables[table.Type] = table;
		}

		public static void AddTables(IList<TableMetadata> tables)
		{
			foreach (TableMetadata table in tables)
				s_tables[table.Type] = table;
		}

		/// <summary>
		/// All instances of TableMetadata should be fetched through this static method. This is where, if a type is not registered, it will be registered.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static TableMetadata For<T>()
		{
			Type type = typeof(T);
			if (!s_tables.ContainsKey(type))
				AddTable(ForType(type));
			return s_tables[type];
		}

		public static TableMetadata For(Type type)
		{
			if (!s_tables.ContainsKey(type))
				AddTable(ForType(type));
			return s_tables[type];
		}


		// ********************************************************************************
		// *** Instance members ***********************************************************
		// ********************************************************************************

		/// <summary>
		/// The .NET type that maps to the db table (a POCO or DTO, whatever)
		/// </summary>
		public Type Type { get; set; }

		/// <summary>
		/// The name of the table as it is in the database.  Does not include the schema.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The columns in this table. Each <see cref="ColumnMetadata" /> object has a PropertyDescriptor object to tell you about the corresponding property in the class.
		/// If you need to find a column name name instead of ordinal position, use <see cref="GetColumn" />.
		/// The reason we store this in addition to the PropertyDescriptors, is a class might have some extra properties that AREN'T in the SQL table.
		/// At times we need to be able to identify only the properties that represent a table's columns.
		/// </summary>
		public IList<ColumnMetadata> Columns { get; set; }


		/// <summary>
		/// There may be additional properties that are not mapped directly to columns, and you still want to be able to dynamically access them for some reason
		/// (such as ReflectionHelper.ApplyValues(), to populate a collection coming in from a service call).
		/// </summary>
		public PropertyDescriptorCollection Properties { get; set; }


		/// <summary>
		/// Private constructor, called from the static ForType factory method.
		/// </summary>
		private TableMetadata (Type type, bool useHyperPropertyDescriptor = true)
		{
			// Create a new entry based on reflection of the type.
			if (useHyperPropertyDescriptor)
				HyperTypeDescriptionProvider.Add(type);
			Type = type;
			Name = Type.Name;
			Properties = TypeDescriptor.GetProperties(type);
			Columns = new List<ColumnMetadata>();
			foreach (PropertyDescriptor property in Properties)
				Columns.Add(new ColumnMetadata(property));
		}


		/// <summary>
		/// Constructor.
		/// </summary>
		public TableMetadata(Type type, IList<ColumnMetadata> columns, bool useHyperPropertyDescriptor = true)
		{
			if (useHyperPropertyDescriptor)
				HyperTypeDescriptionProvider.Add(type);
			Type = type;
			Name = Type.Name;
			Columns = columns;
			Properties = TypeDescriptor.GetProperties(type);
		}

		/// <summary>
		/// Private constructor, called from the static ForType factory method.
		/// </summary>
		public static TableMetadata ForType(Type type, bool useHyperPropertyDescriptor = true)
		{
			// We need to see if there's a "Metadata" class that has info for this type.
			// If there is, we just need to call that class's Initialize() method.
			// There are 2 assemblies to search for the Metadata class.  The one that contains our type, and the current one (since Noef is used as a single file,
			// the current executing assembly will be our DataAccess or DAL assembly).  The current assembly is more likely.
			// NOTE: If Noef is being used as an assembly reference and NOT a single file distribution, and the Metadata is NOT in the same assembly as the type
			// (could be the case if the type is in a separate Dtos project), this won't be able to find the Metadata class!
			var typesInCurrent = ReflectionHelper.GetTypesWithAttribute(Assembly.GetExecutingAssembly(), typeof(MetadataClassAttribute)).ToList();
			if (typesInCurrent.Count > 0)
			{
				foreach(Type t in typesInCurrent)
				{
					var initializeMethod = t.GetMethods().Where(m => m.Name == "Initialize").FirstOrDefault();
					if (initializeMethod != null)
						initializeMethod.Invoke(null, null);
				}
			}
			else
			{
				// Let's try the assembly that our type is contained in
				var typesInContainingAsm = ReflectionHelper.GetTypesWithAttribute(Assembly.GetAssembly(type), typeof(MetadataClassAttribute)).ToList();
				if (typesInContainingAsm.Count > 0)
				{
					foreach(Type t in typesInContainingAsm)
					{
						var initializeMethod = t.GetMethods().Where(m => m.Name == "Initialize").FirstOrDefault();
						if (initializeMethod != null)
							initializeMethod.Invoke(null, null);
					}
				}
			}

			// Let's see if calling any Metadata class's Initialize() methods added our type...
			if (s_tables.ContainsKey(type))
				return s_tables[type];

			// Trying to find Metadata classes and calling their Initialize() methods didn't work.
			// This may be an unregistered type (no code-gen exists for it).
			// We'll create a new entry based on reflection of the type.
			return new TableMetadata(type, useHyperPropertyDescriptor);
		}


		public ColumnMetadata GetColumn(string colName)
		{
			return Columns
				.Where(col => String.Equals(col.Name, colName, StringComparison.InvariantCultureIgnoreCase))
				.FirstOrDefault();
		}


		public IList<PropertyDescriptor> GetNonColumnProperties()
		{
			var colNames = Columns.Select(col => col.Name);
			return Properties
				.Cast<PropertyDescriptor>()
				.Where(p => !colNames.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))
				.ToList();
		}


		/// <summary>
		/// This override will dump important info about the entries in the TableMetadata collection, for debugging
		/// </summary>
		/// <returns></returns>
		public static string GetInfoString()
		{
			StringBuilder sb = new StringBuilder();
			foreach(var tmeta in s_tables)
			{
				sb.AppendLine(tmeta.Value.Name);
				sb.AppendLine("\t" + tmeta.Key.AssemblyQualifiedName);
				sb.AppendLine("\t" + String.Join(",", tmeta.Value.Columns.Select(col => col.Name)));
			}
			return sb.ToString();
		}


		// ********************************************************************************
		// *** Get sql snippet helpers ****************************************************
		// ********************************************************************************

		/// <summary>
		/// Can accept a string input such as "Name, DateCreated desc". The terms are individually verified to be actual names in the table, and the order directions are verified to be
		/// either ASC or DESC.  All arguments are case-insensitive.  If a bad value is found, that term is skipped and ommitted from the returned string.
		/// This method is used to protect against sql injection attacks.
		/// </summary>
		/// <returns>A safe SQL string that can be used as an ORDER BY clause</returns>
		public static string GetSafeOrderByString<T>(string orderByInput)
		{
			List<string> verifiedColumns = new List<string>();

			string[] cols = orderByInput.Split(new[] {','});
			IList<string> safeColNames = GetColumnNames<T>();
			foreach(string col in cols.Select(col => col.Trim()))
			{
				Match match = RX_COL.Match(col);
				// Remember that group 0 is the whole regex match
				string colName = match.Groups[1].Value;
				string dir = "ASC";

				if (match.Groups.Count > 2)
				{
					string tempDir = match.Groups[2].Value.ToUpper();
					if (tempDir == "ASC" || tempDir == "DESC")
						dir = tempDir;
				}

				if (!safeColNames.Contains(colName, StringComparer.InvariantCultureIgnoreCase))
					throw new Exception("Invalid ORDER BY column");
				
				verifiedColumns.Add(colName + " " + dir);
			}

			return String.Join(",", verifiedColumns);
		}

		public static string GetTableName<T>()
		{
			return For<T>().Name;
		}


		// ********************************************************************************
		// *** Get column list helpers ****************************************************
		// ********************************************************************************

		public static IList<ColumnMetadata> GetColumns<T>()
		{
			return For<T>().Columns;
		}

		public static IList<string> GetColumnNames<T>()
		{
			return For<T>().Columns
				.Select(col => col.Name)
				.ToList();
		}

		/// <summary>
		/// Returns a comma separated list of all the columns in the table (as a single string) for use in a SELECT statement.
		/// Example: "ID, FName, LName, DateCreated"
		/// </summary>
		public static string GetColumnsString<T>()
		{
			return String.Join(",", GetColumns<T>().Select(col => col.Name));
		}

		/// <summary>
		/// Returns a comma separated list of all the columns in the table (as a single string) for use in a SELECT statement.
		/// Example ("p_"): "p_ID, p_FName, p_LName, p_DateCreated"
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="columnPrefix"></param>
		/// <returns></returns>
		public static string GetColumnsString<T>(string columnPrefix)
		{
			return String.Join(",", GetColumns<T>().Select(col => columnPrefix + col.Name));
		}

		/// <summary>
		/// Returns a comma separated list of all the columns in the table (as a single string) for use in a SELECT statement, but with an optional table alias and an optional AS prefix.
		/// Example ("p", "p_"): "p.ID AS p_ID, p.FName AS p_FName, p.LName AS p_LName, p.DateCreated AS p_DateCreated".
		/// Both the alias and AS prefix are optional.  If neither are provided, this will return the same string that <see cref="GetColumnsString{T}()" /> would.
		/// </summary>
		public static string GetColumnsString<T>(string tableAlias, string asPrefix)
		{
			if (!String.IsNullOrWhiteSpace(tableAlias))
				tableAlias += ".";
			if (String.IsNullOrWhiteSpace(asPrefix))
				return String.Join(",", GetColumns<T>().Select(col => tableAlias + col.Name));
			return String.Join(",", GetColumns<T>().Select(col => tableAlias + col.Name + " AS [" + asPrefix + col.Name + "]"));
		}

		/// <summary>
		/// Returns a comma separated list of all the columns in the table (as a single string) for use in a SELECT statement, but using an optional table alias, an optional AS prefix,
		/// and specifying an optional prefix for the source column names (as if an AS prefix were used in a subquery).
		/// Example ("temp", "person_", "p_"): "temp.person_ID AS p_ID, temp.person_FName AS p_FName, temp.person_LName AS p_LName", etc.
		/// Again, any of the options can be blank or null and they will not be used.
		/// </summary>
		public static string GetColumnsString<T>(string tableAlias, string sourceColumnPrefix, string asPrefix)
		{
			if (!String.IsNullOrWhiteSpace(tableAlias))
				tableAlias += ".";
			if (String.IsNullOrWhiteSpace(asPrefix))
				return String.Join(",", GetColumns<T>().Select(col => tableAlias + sourceColumnPrefix + col.Name));
			return String.Join(",", GetColumns<T>().Select(col => tableAlias + sourceColumnPrefix + col.Name + " AS [" + asPrefix + col.Name + "]"));
		}

	}


}

namespace Noef.UnitTests.Hyper
{
//
// ChainingPropertyDescriptor.cs
//
    public abstract class ChainingPropertyDescriptor : PropertyDescriptor {
        private readonly PropertyDescriptor _root;
        protected PropertyDescriptor Root { get { return _root; } }
        protected ChainingPropertyDescriptor(PropertyDescriptor root)
            : base(root) {
            _root = root;
        }
        public override void AddValueChanged(object component, EventHandler handler) {
            Root.AddValueChanged(component, handler);
        }
        public override AttributeCollection Attributes {
            get {
                return Root.Attributes;
            }
        }
        public override bool CanResetValue(object component) {
            return Root.CanResetValue(component);
        }
        public override string Category {
            get {
                return Root.Category;
            }
        }
        public override Type ComponentType {
            get { return Root.ComponentType; }
        }
        public override TypeConverter Converter {
            get {
                return Root.Converter;
            }
        }
        public override string Description {
            get {
                return Root.Description;
            }
        }
        public override bool DesignTimeOnly {
            get {
                return Root.DesignTimeOnly;
            }
        }
        public override string DisplayName {
            get {
                return Root.DisplayName;
            }
        }
        public override bool Equals(object obj) {
            return Root.Equals(obj);
        }
        public override PropertyDescriptorCollection GetChildProperties(object instance, Attribute[] filter) {
            return Root.GetChildProperties(instance, filter);
        }
        public override object GetEditor(Type editorBaseType) {
            return Root.GetEditor(editorBaseType);
        }
        public override int GetHashCode() {
            return Root.GetHashCode();
        }
        public override object GetValue(object component) {
            return Root.GetValue(component);
        }
        public override bool IsBrowsable {
            get {
                return Root.IsBrowsable;
            }
        }
        public override bool IsLocalizable {
            get {
                return Root.IsLocalizable;
            }
        }
        public override bool IsReadOnly {
            get { return Root.IsReadOnly; }
        }
        public override string Name {
            get {
                return Root.Name;
            }
        }
        public override Type PropertyType {
            get { return Root.PropertyType; }
        }
        public override void RemoveValueChanged(object component, EventHandler handler) {
            Root.RemoveValueChanged(component, handler);
        }
        public override void ResetValue(object component) {
            Root.ResetValue(component);
        }
        public override void SetValue(object component, object value) {
            Root.SetValue(component, value);
        }
        public override bool ShouldSerializeValue(object component) {
            return Root.ShouldSerializeValue(component);
        }
        public override bool SupportsChangeEvents {
            get {
                return Root.SupportsChangeEvents;
            }
        }
        public override string ToString() {
            return Root.ToString();
        }
    }

//
// HyperTypeDescriptionProvider.cs
//
    public sealed class HyperTypeDescriptionProvider : TypeDescriptionProvider {
        public static void Add(Type type) {
            TypeDescriptionProvider parent = TypeDescriptor.GetProvider(type);
            TypeDescriptor.AddProvider(new HyperTypeDescriptionProvider(parent), type);
        }
        public HyperTypeDescriptionProvider() : this(typeof(object)) { }
        public HyperTypeDescriptionProvider(Type type) : this(TypeDescriptor.GetProvider(type)) { }
        public HyperTypeDescriptionProvider(TypeDescriptionProvider parent) : base(parent) { }
        public static void Clear(Type type) {
            lock (descriptors) {
                descriptors.Remove(type);
            }
        }
        public static void Clear() {
            lock (descriptors) {
                descriptors.Clear();
            }
        }
        private static readonly Dictionary<Type, ICustomTypeDescriptor> descriptors = new Dictionary<Type, ICustomTypeDescriptor>();
        public sealed override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance) {
            ICustomTypeDescriptor descriptor;
            lock (descriptors) {
                if (!descriptors.TryGetValue(objectType, out descriptor)) {
                    try
                    {
                        descriptor = BuildDescriptor(objectType);
                    }
                    catch
                    {
                        return base.GetTypeDescriptor(objectType, instance);
                    }
                }
                return descriptor;
            }
        }

		[SecuritySafeCritical]
        [ReflectionPermission(SecurityAction.Assert, Unrestricted = true)]
        private ICustomTypeDescriptor BuildDescriptor(Type objectType)
        {
            // NOTE: "descriptors" already locked here

            // get the parent descriptor and add to the dictionary so that
            // building the new descriptor will use the base rather than recursing
            ICustomTypeDescriptor descriptor = base.GetTypeDescriptor(objectType, null);
            descriptors.Add(objectType, descriptor);
            try
            {
                // build a new descriptor from this, and replace the lookup
                descriptor = new HyperTypeDescriptor(descriptor);
                descriptors[objectType] = descriptor;
                return descriptor;
            }
            catch
            {   // rollback and throw
                // (perhaps because the specific caller lacked permissions;
                // another caller may be successful)
                descriptors.Remove(objectType);
                throw;
            }
        }
    }

//
// HyperTypeDescriptor.cs
//
    sealed class HyperTypeDescriptor : CustomTypeDescriptor {
        private readonly PropertyDescriptorCollection propertyCollections;
        static readonly Dictionary<PropertyInfo, PropertyDescriptor> properties = new Dictionary<PropertyInfo, PropertyDescriptor>();
        internal HyperTypeDescriptor(ICustomTypeDescriptor parent)
            : base(parent) {
            propertyCollections = WrapProperties(parent.GetProperties());
        }
        public sealed override PropertyDescriptorCollection GetProperties(Attribute[] attributes) {
            return propertyCollections;
        }
        public sealed override PropertyDescriptorCollection GetProperties() {
            return propertyCollections;
        }
        private static PropertyDescriptorCollection WrapProperties(PropertyDescriptorCollection oldProps) {
            PropertyDescriptor[] newProps = new PropertyDescriptor[oldProps.Count];
            int index = 0;
            bool changed = false;
            // HACK: how to identify reflection, given that the class is internal...
            Type wrapMe = Assembly.GetAssembly(typeof(PropertyDescriptor)).GetType("System.ComponentModel.ReflectPropertyDescriptor");
            foreach (PropertyDescriptor oldProp in oldProps) {
                PropertyDescriptor pd = oldProp;
                // if it looks like reflection, try to create a bespoke descriptor
                if (ReferenceEquals(wrapMe, pd.GetType()) && TryCreatePropertyDescriptor(ref pd)) {
                    changed = true;
                }
                newProps[index++] = pd;
            }

            return changed ? new PropertyDescriptorCollection(newProps, true) : oldProps;
        }

        static readonly ModuleBuilder moduleBuilder;
        static int counter;
        static HyperTypeDescriptor() {
            AssemblyName an = new AssemblyName("Hyper.ComponentModel.dynamic");
            AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            moduleBuilder = ab.DefineDynamicModule("Hyper.ComponentModel.dynamic.dll");

        }

        private static bool TryCreatePropertyDescriptor(ref PropertyDescriptor descriptor) {
            try {
                PropertyInfo property = descriptor.ComponentType.GetProperty(descriptor.Name);
                if (property == null) return false;

                lock (properties) {
                    PropertyDescriptor foundBuiltAlready;
                    if (properties.TryGetValue(property, out foundBuiltAlready)) {
                        descriptor = foundBuiltAlready;
                        return true;
                    }

                    string name = "_c" + Interlocked.Increment(ref counter).ToString();
                    TypeBuilder tb = moduleBuilder.DefineType(name, TypeAttributes.Sealed | TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoClass | TypeAttributes.Public, typeof(ChainingPropertyDescriptor));

                    // ctor calls base
                    ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard, new Type[] { typeof(PropertyDescriptor) });
                    ILGenerator il = cb.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, typeof(ChainingPropertyDescriptor).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(PropertyDescriptor) }, null));
                    il.Emit(OpCodes.Ret);

                    MethodBuilder mb;
                    MethodInfo baseMethod;
                    if (property.CanRead) {
                        // obtain the implementation that we want to override
                        baseMethod = typeof(ChainingPropertyDescriptor).GetMethod("GetValue");
                        // create a new method that accepts an object and returns an object (as per the base)
                        mb = tb.DefineMethod(baseMethod.Name,
                            MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
                            baseMethod.CallingConvention, baseMethod.ReturnType, new Type[] { typeof(object) });
                        // start writing IL into the method
                        il = mb.GetILGenerator();
                        if (property.DeclaringType.IsValueType) {
                            // upbox the object argument into our known (instance) struct type
                            LocalBuilder lb = il.DeclareLocal(property.DeclaringType);
                            il.Emit(OpCodes.Ldarg_1);
                            il.Emit(OpCodes.Unbox_Any, property.DeclaringType);
                            il.Emit(OpCodes.Stloc_0);
                            il.Emit(OpCodes.Ldloca_S, lb);
                        } else {
                            // cast the object argument into our known class type
                            il.Emit(OpCodes.Ldarg_1);
                            il.Emit(OpCodes.Castclass, property.DeclaringType);
                        }
                        // call the "get" method
                        il.Emit(OpCodes.Callvirt, property.GetGetMethod());

                        if (property.PropertyType.IsValueType) {
                            // box it from the known (value) struct type
                            il.Emit(OpCodes.Box, property.PropertyType);
                        }
                        // return the value
                        il.Emit(OpCodes.Ret);
                        // signal that this method should override the base
                        tb.DefineMethodOverride(mb, baseMethod);
                    }

                    bool supportsChangeEvents = descriptor.SupportsChangeEvents, isReadOnly = descriptor.IsReadOnly;

                    // override SupportsChangeEvents
                    baseMethod = typeof(ChainingPropertyDescriptor).GetProperty("SupportsChangeEvents").GetGetMethod();
                    mb = tb.DefineMethod(baseMethod.Name, MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName, baseMethod.CallingConvention, baseMethod.ReturnType, Type.EmptyTypes);
                    il = mb.GetILGenerator();
                    if (supportsChangeEvents) {
                        il.Emit(OpCodes.Ldc_I4_1);
                    } else {
                        il.Emit(OpCodes.Ldc_I4_0);
                    }
                    il.Emit(OpCodes.Ret);
                    tb.DefineMethodOverride(mb, baseMethod);

                    // override IsReadOnly
                    baseMethod = typeof(ChainingPropertyDescriptor).GetProperty("IsReadOnly").GetGetMethod();
                    mb = tb.DefineMethod(baseMethod.Name, MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName, baseMethod.CallingConvention, baseMethod.ReturnType, Type.EmptyTypes);
                    il = mb.GetILGenerator();
                    if (isReadOnly) {
                        il.Emit(OpCodes.Ldc_I4_1);
                    } else {
                        il.Emit(OpCodes.Ldc_I4_0);
                    }
                    il.Emit(OpCodes.Ret);
                    tb.DefineMethodOverride(mb, baseMethod);

                    /*  REMOVED: PropertyType, ComponentType; actually *adds* time overriding these
                    // override PropertyType
                    baseMethod = typeof(ChainingPropertyDescriptor).GetProperty("PropertyType").GetGetMethod();
                    mb = tb.DefineMethod(baseMethod.Name, MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName, baseMethod.CallingConvention, baseMethod.ReturnType, Type.EmptyTypes);
                    il = mb.GetILGenerator();
                    il.Emit(OpCodes.Ldtoken, descriptor.PropertyType);
                    il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
                    il.Emit(OpCodes.Ret);
                    tb.DefineMethodOverride(mb, baseMethod);

                    // override ComponentType
                    baseMethod = typeof(ChainingPropertyDescriptor).GetProperty("ComponentType").GetGetMethod();
                    mb = tb.DefineMethod(baseMethod.Name, MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName, baseMethod.CallingConvention, baseMethod.ReturnType, Type.EmptyTypes);
                    il = mb.GetILGenerator();
                    il.Emit(OpCodes.Ldtoken, descriptor.ComponentType);
                    il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
                    il.Emit(OpCodes.Ret);
                    tb.DefineMethodOverride(mb, baseMethod);
                    */

                    // for classes, implement write (would be lost in unbox for structs)
                    if (!property.DeclaringType.IsValueType) {
                        if (!isReadOnly && property.CanWrite) {
                            // override set method
                            baseMethod = typeof(ChainingPropertyDescriptor).GetMethod("SetValue");
                            mb = tb.DefineMethod(baseMethod.Name, MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, baseMethod.CallingConvention, baseMethod.ReturnType, new Type[] { typeof(object), typeof(object) });
                            il = mb.GetILGenerator();
                            il.Emit(OpCodes.Ldarg_1);
                            il.Emit(OpCodes.Castclass, property.DeclaringType);
                            il.Emit(OpCodes.Ldarg_2);
                            if (property.PropertyType.IsValueType) {
                                il.Emit(OpCodes.Unbox_Any, property.PropertyType);
                            } else {
                                il.Emit(OpCodes.Castclass, property.PropertyType);
                            }
                            il.Emit(OpCodes.Callvirt, property.GetSetMethod());
                            il.Emit(OpCodes.Ret);
                            tb.DefineMethodOverride(mb, baseMethod);
                        }

                        if (supportsChangeEvents) {
                            EventInfo ei = property.DeclaringType.GetEvent(property.Name + "Changed");
                            if (ei != null) {
                                baseMethod = typeof(ChainingPropertyDescriptor).GetMethod("AddValueChanged");
                                mb = tb.DefineMethod(baseMethod.Name, MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName, baseMethod.CallingConvention, baseMethod.ReturnType, new Type[] { typeof(object), typeof(EventHandler) });
                                il = mb.GetILGenerator();
                                il.Emit(OpCodes.Ldarg_1);
                                il.Emit(OpCodes.Castclass, property.DeclaringType);
                                il.Emit(OpCodes.Ldarg_2);
                                il.Emit(OpCodes.Callvirt, ei.GetAddMethod());
                                il.Emit(OpCodes.Ret);
                                tb.DefineMethodOverride(mb, baseMethod);

                                baseMethod = typeof(ChainingPropertyDescriptor).GetMethod("RemoveValueChanged");
                                mb = tb.DefineMethod(baseMethod.Name, MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName, baseMethod.CallingConvention, baseMethod.ReturnType, new Type[] { typeof(object), typeof(EventHandler) });
                                il = mb.GetILGenerator();
                                il.Emit(OpCodes.Ldarg_1);
                                il.Emit(OpCodes.Castclass, property.DeclaringType);
                                il.Emit(OpCodes.Ldarg_2);
                                il.Emit(OpCodes.Callvirt, ei.GetRemoveMethod());
                                il.Emit(OpCodes.Ret);
                                tb.DefineMethodOverride(mb, baseMethod);
                            }
                        }

                    }
                    PropertyDescriptor newDesc = tb.CreateType().GetConstructor(new Type[] { typeof(PropertyDescriptor) }).Invoke(new object[] { descriptor }) as PropertyDescriptor;
                    if (newDesc == null) {
                        return false;
                    }
                    descriptor = newDesc;
                    properties.Add(property, descriptor);
                    return true;
                }
            } catch {
                return false;
            }
        }
    }


}

