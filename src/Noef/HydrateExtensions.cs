using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace Noef
{
	public static class HydrateExtensions
	{
		// ********************************************************************************
		// *** object hydrators ***********************************************************
		// *** (these are extension methods for object[] and IList<object[]>) *************
		// ********************************************************************************

		public static IList<object[]> GetRows(this IDbCommand cmd)
		{
			using (IDataReader reader = cmd.ExecuteReader())
			{
				IList<object[]> rows = reader.GetRows();
				reader.Close();
				return rows;
			}
		}

		public static RawTable GetRawTable(this IDbCommand cmd, int startingColumn = 0)
		{
			using (IDataReader reader = cmd.ExecuteReader())
			{
				RawTable table = reader.GetRawTable(startingColumn);
				reader.Close();
				return table;
			}
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
		/// <param name="propsToHydrate"></param>
		/// <param name="hydrateRowCallback"> </param>
		/// <returns></returns>
		public static T Hydrate<T>(this object[] row, int startingColumn = 0, int pkColumn = -1, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			Type type = typeof(T);
			if (row == null)
				throw new HydrateException("Error hydrating to type " + type.Name + ". row cannot be null");

			T obj;

			// If the PK column is null, return null (this should only be the case with LEFT/RIGHT joins, where there was no corresponding record in the join)
			// Note that we're operating on an assumption here, which becomes a de-facto convention of Noef.  YOUR DATA MUST HAVE PKS.
			if (row[pkColumn] == DBNull.Value)
			{
				obj = default(T);
				if (hydrateRowCallback != null)
					hydrateRowCallback(obj, row);
				return obj;
			}

			// See if we have a registered hydrator for this type
			Func<object[], int, T> hydrator = TableMetadata.Hydrators<T>.Hydrator;
			if (hydrator != null && propsToHydrate == null)
			{
				obj = hydrator(row, startingColumn);
			}
			else if (type.Module.Name == "mscorlib.dll")
			{
				// A built in system type.
				if (type == typeof(string) || type.IsPrimitive)
					obj = (T)row[startingColumn];
				else
					throw new Exception("Unsupported type for hydration");
				if (hydrateRowCallback != null)
					hydrateRowCallback(obj, row);
			}
			else
			{
				// A custom user type.
				// Note that we don't use the ColumnMetadata entries. We are just accessing the fields ordinally, so the order of the result set MUST match
				// the order of the properties in the user or dto type.

				string[] propNames = null;
				if (propsToHydrate != null)
				{
					propNames = ReflectionHelper.GetPropertyNames(propsToHydrate);
				}

				try
				{
					obj = (T)Activator.CreateInstance(typeof(T), true);
				}
				catch (Exception ex)
				{
					throw new HydrateException("An object of type " + type.Name + " could not be created. Check the inner exception for details.", ex);
				}

				TableMetadata tmeta = TableMetadata.For<T>();
				if (tmeta == null)
					throw new HydrateException("The TableMetadata entry for type " + type.Name + " is null.");

				// i is declared out of the try block so I can reference it's value in the catch block.
				int i = 0;

				// hydrating using provided property names
				if (propNames != null)
				{
					PropertyDescriptor prop = null;
					try
					{
						PropertyDescriptor[] props = tmeta.Properties.Cast<PropertyDescriptor>().ToArray();
						for (i = 0; i < propNames.Length; i++)
						{
							prop = props.SingleOrDefault(p => p.Name == propNames[i]);
							if (prop == null)
								throw new Exception("Could not find a property named " + propNames[i] + " in type " + type.Name);
							ReflectionHelper.SetValue(prop, obj, row[i + startingColumn].NullOrValue());
						}
					}
					catch (Exception ex)
					{
						if (prop == null)
							throw new HydrateException("Error setting a property for type " + type.Name + " (using provided property names). i = " + i
								+ ", startingColumn = " + startingColumn , ex);

						throw new HydrateException("Error setting property " + type.Name + "." + prop.Name + " (using provided property names). i = " + i
							+ ", startingColumn = " + startingColumn, ex);
					}
				}
				// hydrating all properties, ordinally (but Noef will NOT exceed the number of columns in the object[] row)
				else
				{
					try
					{
						// The query results need to match the order of the properties in the class, but not ALL the properties have to be hydrated.
						// If your row has only the first 5, on those will be hydrated.
						int numProps = Math.Min(tmeta.Columns.Count, row.Length - startingColumn);

						for (i = 0; i < numProps; i++)
						{
							PropertyDescriptor prop = tmeta.Columns[i].Property;
							ReflectionHelper.SetValue(prop, obj, row[i + startingColumn].NullOrValue());
						}
					}
					catch (Exception ex)
					{
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
			}
			if (hydrateRowCallback != null)
				hydrateRowCallback(obj, row);
			return obj;
		}


		
		public static IList<T> HydrateList<T>(this IList<object[]> rows, int startingColumn = 0, int pkColumn = -1, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			return rows
				.Select(row => row.Hydrate(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback))
				.ToList();
		}



		public static IList<T> HydrateNonNullList<T>(this IList<object[]> rows, int startingColumn = 0, int pkColumn = -1, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

// ReSharper disable CompareNonConstrainedGenericWithNull
			return rows
				.Select(row => row.Hydrate(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback))
				.Where(obj => obj != null)
				.ToList();
// ReSharper restore CompareNonConstrainedGenericWithNull
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
		/// <param name="propsToHydrate"> </param>
		/// <param name="hydrateRowCallback"> </param>
		/// <returns></returns>
		public static IList<T> HydrateUniqueList<T>(this IList<object[]> rows, int keyIndex, int startingColumn = 0, int pkColumn = -1, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			List<T> list = new List<T>();
			List<object> keys = new List<object>();
			foreach(object[] row in rows)
			{
				object curKey = row[keyIndex];
				// Make sure we're encountering a new key
				if (!keys.Any(k => Equals(k, curKey)))
				{
					// Previously unencountered key.  Hydrate the object, add it to the list, and add the key to the list of encountered keys.
					list.Add(row.Hydrate(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback));
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
		public static IDictionary<object, IList<T>> HydrateNonNullListPerKey<T>(this IList<object[]> rows, int keyIndex, int startingColumn = 0, int pkColumn = -1)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

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
		public static IDictionary<object, IList<T>> HydrateListPerKey<T>(this IList<object[]> rows, int keyIndex, int startingColumn = 0, int pkColumn = -1)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

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
		/// <param name="propsToHydrate"> </param>
		/// <param name="hydrateRowCallback"> </param>
		/// <returns>A list of lists, each list containing all objects that had the same value in the "keyIndex" column.</returns>
		public static IDictionary<object, IList<T>> HydrateListPerKey<T>(this IList<object[]> rows, int keyIndex, bool includeNulls, int startingColumn = 0, int pkColumn = -1, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			// The final list of lists that we'll return to the caller
			IDictionary<object, IList<T>> dict = new Dictionary<object, IList<T>>();

			foreach(object[] row in rows)
			{
				object curKey = row[keyIndex];
				// If we encounter a new "key" value, we need to create a new list in our dictionary
				if (!dict.ContainsKey(curKey))
					dict.Add(curKey, new List<T>());
				T obj = row.Hydrate(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback);
// ReSharper disable CompareNonConstrainedGenericWithNull
				if (includeNulls || obj != null)
					dict[curKey].Add(obj);
// ReSharper restore CompareNonConstrainedGenericWithNull
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
				int numProps = Math.Min(reader.FieldCount, tmeta.Columns.Count); // don't try to hydrate more properties than we have columns!
				for (i = 0; i < tmeta.Columns.Count; i++)
				{
					ColumnMetadata col = tmeta.Columns[i];
					ReflectionHelper.SetValue(col.Property, obj, reader[columnNamePrefix + col.Name].NullOrValue());
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



		public static T Hydrate<T>(this IDataReader reader, int startingColumn = 0, int pkColumn = -1, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			// Make sure we can advance to the first (or next) row in the reader
			if (!reader.Read())
				return default(T);
			// Get a single row's values
			object[] values = new object[reader.FieldCount];
			reader.GetValues(values);
			// Hydrate that rows values into type T
			return values.Hydrate(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback);
		}




		/// <summary>
		/// (Unit tests: HydrateTests.HydrateListOfString_FromReader_1, 2 and 3 - tests overloads as well)
		/// (Unit tests: HydrateTests.HydrateListOfObject_FromReader_1, 2 and 3 - tests overloads as well)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reader"></param>
		/// <param name="startingColumn"></param>
		/// <param name="pkColumn"></param>
		/// <param name="propsToHydrate"> </param>
		/// <param name="hydrateRowCallback"> </param>
		/// <returns></returns>
		public static IList<T> HydrateList<T>(this IDataReader reader, int startingColumn = 0, int pkColumn = -1, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null)
		{
			if (pkColumn == -1)
				pkColumn = startingColumn;

			IList<object[]> rows = reader.GetRows();
			return rows.HydrateList(startingColumn, pkColumn, propsToHydrate, hydrateRowCallback);
		}


		public static List<string> GetFieldNames(this IDataRecord reader)
		{
			if (reader == null)
				throw new ArgumentNullException("reader", "reader cannot be null");
			List<string> names = new List<string>();
			for(int i=0; i < reader.FieldCount; i++)
				names.Add(reader.GetName(i));
			return names;
		}

	}
}
