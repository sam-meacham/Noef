using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public abstract partial class NoefDal
	{
		/// <summary>
		/// Constructs and executes an INSERT statement based on the table specified by T (using the column metadata).
		/// The values used for the insert will be taken from "obj".
		/// Since not all tables have an IDENTITY PK, you'll have to exclude the PK column if you don't want it in the column list or VALUES() values.
		/// Similiar to Update(), you can override column values (the exact string provided will be used instead of a param, populated with "obj"'s corresponding property value).
		/// Any params specified in the overrides can be populated using the beforeExecute Action.
		/// This returns the same "obj" that was passed in, but with all properties updated to reflect their current values in the database (so if you have an IDENTITY PK field, you'll have the value now).
		/// </summary>
		public void InsertWhitelist<T>(T obj,
			string pkColumn,
			IEnumerable<string> whitelistColumns,
			IDictionary<string, string> columnInsertOverrides = null,
			IDbConnection cn                                  = null,
			Action<IDbCommand> beforeExecute                  = null,
			IDbTransaction tx                                 = null,
			string newRecordIdSelector                        = "SCOPE_IDENTITY()",
			int timeout                                       = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();

			// automatically use the PK column name for record id seletor if it's NOT an IDENTITY column
			if (newRecordIdSelector == null)
				newRecordIdSelector = "SCOPE_IDENTITY()";
			if (!tmeta.Pk.IsIdentity && newRecordIdSelector == "SCOPE_IDENTITY()")
			{
				newRecordIdSelector = tmeta.Pk.Name;
			}

			// validate the whitelisted columns -- throw an exception if one specified doesn't exist
			List<string> whitelist = whitelistColumns == null ? new List<string>() : whitelistColumns.ToList();
			EnsureColumnsExist<T>(whitelist);
			if (whitelist.Count == 0)
				throw new Exception("You're calling InsertWhitelist with no whitelist columns specified");

			StringBuilder sb = new StringBuilder();

			// Set up the sql statement
			sb.AppendLine("INSERT INTO " + tmeta.Name);

			IEnumerable<string> insertCols = tmeta.Columns
				.Where(col => whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
				.Select(c => c.Name);

			sb.AppendLine("(" + String.Join(",", insertCols.ToArray()) + ")");
			sb.Append("VALUES(");

			List<string> insertVals = new List<string>();
			foreach(ColumnMetadata col in tmeta.Columns)
			{
				if (!whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
					continue;
				if (columnInsertOverrides != null && columnInsertOverrides.Keys.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
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
					if (!whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
						continue;
					if (columnInsertOverrides != null && columnInsertOverrides.Keys.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase))
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
		/// Constructs and executes an INSERT statement based on the table specified by T (using the column metadata).
		/// The values used for the insert will be taken from "obj".
		/// Since not all tables have an IDENTITY PK, you'll have to exclude the PK column if you don't want it in the column list or VALUES() values.
		/// Similiar to Update(), you can override column values (the exact string provided will be used instead of a param, populated with "obj"'s corresponding property value).
		/// Any params specified in the overrides can be populated using the beforeExecute Action.
		/// This returns the same "obj" that was passed in, but with all properties updated to reflect their current values in the database (so if you have an IDENTITY PK field, you'll have the value now).
		/// </summary>
		public void InsertBlacklist<T>(T obj,
			string pkColumn,
			IEnumerable<string> excludedColumns               = null,
			IDictionary<string, string> columnInsertOverrides = null,
			IDbConnection cn                                  = null,
			Action<IDbCommand> beforeExecute                  = null,
			IDbTransaction tx                                 = null,
			string newRecordIdSelector                        = "SCOPE_IDENTITY()",
			int timeout                                       = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();

			// automatically use the PK column name for record id seletor if it's NOT an IDENTITY column
			if (newRecordIdSelector == null)
				newRecordIdSelector = "SCOPE_IDENTITY()";
			if (!tmeta.Pk.IsIdentity && newRecordIdSelector == "SCOPE_IDENTITY()")
			{
				newRecordIdSelector = tmeta.Pk.Name;
			}

			StringBuilder sb = new StringBuilder();
			List<string> excluded = excludedColumns == null ? new List<string>() : excludedColumns.ToList();

			// Force the PK to be in the excludedColumns if it isn't already and is an IDENTITY field
			if (tmeta.Pk.IsIdentity && !excluded.Contains(tmeta.Pk.Name))
			{
				excluded.Add(tmeta.Pk.Name);
			}

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
	}
}
