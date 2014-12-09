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
		/// <param name="keyColumn">The name of the column that is the primary key. No support for composite keys.</param>
		/// <param name="columnUpdateOverrides">Literal string values to use for specific column names (instead of default param names and "obj"'s values)</param>
		/// <param name="excludedColumns">Columns to exclude from the SET column list</param>
		/// <param name="beforeExecute">An Action to be executed (on the IDbCommand) before ExecuteReader() is called (can be used to add params, etc)</param>
		/// <param name="tx"> </param>
		/// <param name="timeout"></param>
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database (fields only, no collections. This is not an ORM, duh)</returns>
		public void UpdateBlacklist<T>(T obj,
			string keyColumn                                  = null,
			IEnumerable<string> excludedColumns               = null,
			IDictionary<string, string> columnUpdateOverrides = null,
			IDbConnection cn                                  = null,
			Action<IDbCommand> beforeExecute                  = null,
			IDbTransaction tx                                 = null,
			int timeout                                       = -1)
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
				// Never try to update the PK column
				if (String.Equals(col.Name, keyColumn, StringComparison.OrdinalIgnoreCase))
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
			sb.AppendLine("WHERE " + keyColumn + " = @" + keyColumn);
			sb.AppendLine();
			sb.AppendLine("-- Return the updated record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + keyColumn + " = @" + keyColumn);

			// Set up the command
			T updatedObj;
			using (IDbCommand cmd = CreateCommand<T>(sb.ToString(), null, cn, CommandType.Text, tx, timeout))
			{
				// Add the pk param
				cmd.AddParam("@" + keyColumn, tmeta.GetColumn(keyColumn).Property.GetValue(obj));

				// Add the remaining params
				foreach (ColumnMetadata col in tmeta.Columns)
				{
					// Don't add the pk column, it's already been added.
					if (String.Equals(col.Name, keyColumn, StringComparison.OrdinalIgnoreCase))
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


		/// <summary>
		/// Constructs and executes an UPDATE statement based on the table specified by T (using the column metadata).
		/// A single row will be updated using a WHERE clause based on the pkColumn name (taken from the TableMetadata.Pk entry)
		/// ONLY columns specified in the whitelist will be updated.
		/// Each column in the table will be updated with the corresponding property in "obj". No overrides in this method, this is for very simple updates.
		/// </summary>
		/// <typeparam name="T">The class that represents the table in the database that you want to update a record for</typeparam>
		/// <param name="obj">The object representing the record you want to update</param>
		/// <param name="columnUpdateOverrides"></param>
		/// <param name="cn">The connection to use</param>
		/// <param name="keyColumn">The name of the column that is the primary key. No support for composite keys.</param>
		/// <param name="whitelistColumns">The columns you want to be updated (will be in the SET columns list)</param>
		/// <param name="beforeExecute"></param>
		/// <param name="tx"> </param>
		/// <param name="timeout"></param>
		/// <returns>The same "obj" that was passed in, but with all its properties updated to reflect current values in the database, including columns that were
		/// not in the update statement (triggers, who knows)</returns>
		public void UpdateWhitelist<T>(T obj,
			string keyColumn                                  = null,
			IEnumerable<string> whitelistColumns              = null,
			IDictionary<string, string> columnUpdateOverrides = null,
			IDbConnection cn                                  = null,
			Action<IDbCommand> beforeExecute                  = null,
			IDbTransaction tx                                 = null,
			int timeout                                       = -1)
		{
			TableMetadata tmeta = TableMetadata.For<T>();
			StringBuilder sb = new StringBuilder();

			// validate the whitelisted columns -- throw an exception if one specified doesn't exist
			List<string> whitelist = whitelistColumns == null ? new List<string>() : whitelistColumns.ToList();
			EnsureColumnsExist<T>(whitelist);

			// Set up the sql statement
			sb.AppendLine("UPDATE " + tmeta.Name);
			sb.AppendLine("SET");
			string[] sets = (from col in tmeta.Columns
				where whitelist.Contains(col.Name, StringComparer.InvariantCultureIgnoreCase)
				select col.Name + " = @" + col.Name).ToArray();
			sb.AppendLine(String.Join(",", sets));
			sb.AppendLine("WHERE " + keyColumn + " = @" + keyColumn);
			sb.AppendLine();
			sb.AppendLine("-- Return the updated record");
			sb.AppendLine("SELECT " + TableMetadata.GetColumnsString<T>());
			sb.AppendLine("FROM " + tmeta.Name);
			sb.AppendLine("WHERE " + keyColumn + " = @" + keyColumn);

			// Set up the command
			T updatedObj;
			using (IDbCommand cmd = CreateCommand<T>(sb.ToString(), null, cn, CommandType.Text, tx, timeout))
			{
				// Add the pk param
				cmd.AddParam("@" + keyColumn, tmeta.GetColumn(keyColumn).Property.GetValue(obj));

				// Add the remaining params
				foreach (ColumnMetadata col in tmeta.Columns)
				{
					// Don't add the pk column, it's already been added
					if (String.Equals(col.Name, keyColumn, StringComparison.OrdinalIgnoreCase))
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

	}
}
