using System;
using System.Collections;
#if !NET2
using System.Collections.Concurrent;
#endif
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Noef
{
	/// <summary>
	/// An instance of this class contains the metadata for a table in SQL Server (the name of the table, and all of the column metadata).
	/// This class also has many static members: s_tables, which is the metadata for all tables, and the bulk of the static helper methods that
	/// are used to help construct and execute SQL statements.
	/// </summary>
	public class TableMetadata
	{
		// http://stackoverflow.com/questions/686630/static-generic-class-as-dictionary
		// Each combination of type parameters used will result in separate static values.
		public static class Hydrators<T>
		{
			public static Func<object[], int, T> Hydrator { get; set; }
		}

		// ********************************************************************************
		// *** Static properties and methods **********************************************
		// ********************************************************************************

		/// <summary>
		/// The metadata on all of the classes registered via <see cref="AddTables" />.
		/// </summary>
#if !NET2
		private static readonly IDictionary<Type, TableMetadata> s_tables = new ConcurrentDictionary<Type, TableMetadata>();
#else
		private static readonly IDictionary<Type, TableMetadata> s_tables = new Dictionary<Type, TableMetadata>();
#endif

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
				AddTable(forType(type));
			return s_tables[type];
		}

		public static TableMetadata For(Type type)
		{
			if (!s_tables.ContainsKey(type))
				AddTable(forType(type));
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
		/// The name of the table as it is in the database.  If this table is not in the database that's the default connection
		/// for the main DAL, this will be fully qualified (DBName.SchemaName.TableName). Otherwise it's just the plain table.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Sometimes the Name is fully qualified.  This will always be JUST the plain table name.
		/// </summary>
		public string TableNameOnly { get; set; }

		/// <summary>
		/// The name of the database this table is in.
		/// </summary>
		public string Database { get; set; }

		/// <summary>
		/// The schema name of the table.
		/// </summary>
		public string Schema { get; set; }

		/// <summary>
		/// The name of the connection string to use for this particular table
		/// (If this table is in a database OTHER than your main DB, this might be different than your default connection name).
		/// </summary>
		public string ConnectionName { get; set; }

		/// <summary>
		/// The columns in this table. Each <see cref="ColumnMetadata" /> object has a PropertyDescriptor object to tell you about the corresponding property in the class.
		/// If you need to find a column name name instead of ordinal position, use <see cref="GetColumn" />.
		/// The reason we store this in addition to the PropertyDescriptors, is a class might have some extra properties that AREN'T in the SQL table.
		/// At times we need to be able to identify only the properties that represent a table's columns.
		/// </summary>
		public IList<ColumnMetadata> Columns { get; set; }

		/// <summary>
		/// The ColumnMetadata for the column that is the PK in the database.
		/// Noef does NOT support composite primary keys!
		/// </summary>
		public ColumnMetadata[] Pks { get; set; }

		/// <summary>
		/// If you only have one PK (as you should...), this is a convenience getter for Pks[0]
		/// </summary>
		public ColumnMetadata Pk
		{
			get { return Pks[0]; }
		}


		/// <summary>
		/// There may be additional properties that are not mapped directly to columns, and you still want to be able to dynamically access them for some reason
		/// (such as ReflectionHelper.ApplyValues(), to populate a collection coming in from a service call).
		/// </summary>
		public PropertyDescriptorCollection Properties { get; set; }


		/// <summary>
		/// Private constructor, called from the static forType factory method.
		/// This is only used for unregistered types, not in the TableMetadata collection.
		/// </summary>
		private TableMetadata (Type type)
		{
			// Create a new entry based on reflection of the type.
			Type = type;
			Name = Type.Name;
			Properties = TypeDescriptor.GetProperties(type);
			Columns = new List<ColumnMetadata>();
			foreach (PropertyDescriptor property in Properties)
			{
				// TODO: NOTE: FIX: CAREFUL: For "unregistered" types, I think the smartest thing to do is SKIP over user types and collection types,
				// since those properties CAN'T be hydrated from a single field in the database, and if there WERE a custom TableMetadata entry, those
				// would be skipped over anyway.

				// Noef DOES use a byte[] to represent certain sql types. By convention, I'm going to allow byte[] types to be included in the TableMetadata entries.
				// This means if you have an additional property in some dto type that you DON'T want Hydrate() to try and populate, you'll have to create a custom
				// TableMetadata entry for that type.

				if (property.PropertyType == typeof(string)
						|| property.PropertyType.IsEnum
						|| property.PropertyType == typeof(byte[])
						|| (property.PropertyType.Module.Name == "mscorlib.dll" && !typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
						|| property.PropertyType.FullName == "Microsoft.SqlServer.Types.SqlGeometry"
						|| property.PropertyType.FullName == "Microsoft.SqlServer.Types.SqlGeography"
						|| property.PropertyType.FullName == "Microsoft.SqlServer.Types.SqlHierarchyId"
				)
					Columns.Add(new ColumnMetadata(property));
			}
		}

		/// <summary>
		/// This is the constructor that's called from the generated _Metadata.cs file.
		/// </summary>
		public TableMetadata(Type type, string tableName, string database, string schema, string connectionName, IList<ColumnMetadata> columns)
		{
			Type = type;
			Name = tableName;
			TableNameOnly = getPlainTableName(tableName);
			Database = database;
			Schema = schema;
			ConnectionName = connectionName;
			Columns = columns;
			Pks = Columns.Where(c => c.IsPk).ToArray();
			Properties = TypeDescriptor.GetProperties(type);
		}

		private static string getPlainTableName(string tableName)
		{
			int ixDot = tableName.LastIndexOf(".", StringComparison.Ordinal);
			if (ixDot > -1)
				return tableName.Substring(ixDot + 1);
			return tableName;
		}

		private static TableMetadata forType(Type type)
		{
			// We need to see if there's a "Metadata" class that has info for this type.
			// If there is, we just need to call that class's Initialize() method.
			// There are 2 assemblies to search for the Metadata class.  The one that contains our type, and the current one (since Noef is used as a single file,
			// the current executing assembly will be our DataAccess or DAL assembly).  The current assembly is more likely.
			// NOTE: If Noef is being used as an assembly reference and NOT a single file distribution, and the Metadata is NOT in the same assembly as the type
			// (could be the case if the type is in a separate Dtos project), this won't be able to find the Metadata class!

			List<Type> typesInCurrent = ReflectionHelper.GetTypesWithAttribute(type.Assembly, typeof(MetadataClassAttribute)).ToList();

			if (typesInCurrent.Count > 0)
			{
				foreach(Type t in typesInCurrent)
				{
					var initializeMethod = t.GetMethods().FirstOrDefault(m => m.Name == "Initialize");
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
						var initializeMethod = t.GetMethods().FirstOrDefault(m => m.Name == "Initialize");
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
			// TODO: BIG TODO! This isn't cached!!!
			return new TableMetadata(type);
		}


		public ColumnMetadata GetColumn(string colName)
		{
			return Columns.FirstOrDefault(col => String.Equals(col.Name, colName, StringComparison.OrdinalIgnoreCase));
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
				sb.AppendLine("\t" + String.Join(",", tmeta.Value.Columns.Select(col => col.Name).ToArray()));
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

			return String.Join(",", verifiedColumns.ToArray());
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
			return String.Join(",", GetColumns<T>().Select(col => col.Name).ToArray());
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
			return String.Join(",", GetColumns<T>().Select(col => columnPrefix + col.Name).ToArray());
		}

		/// <summary>
		/// Returns a comma separated list of all the columns in the table (as a single string) for use in a SELECT statement, but with an optional table alias and an optional AS prefix.
		/// Example ("p", "p_"): "p.ID AS p_ID, p.FName AS p_FName, p.LName AS p_LName, p.DateCreated AS p_DateCreated".
		/// Both the alias and AS prefix are optional.  If neither are provided, this will return the same string that <see cref="GetColumnsString{T}()" /> would.
		/// </summary>
		public static string GetColumnsString<T>(string tableAlias, string asPrefix)
		{
			if (!String.IsNullOrEmpty(tableAlias))
				tableAlias += ".";
			if (String.IsNullOrEmpty(asPrefix))
				return String.Join(",", GetColumns<T>().Select(col => tableAlias + col.Name).ToArray());
			return String.Join(",", GetColumns<T>().Select(col => tableAlias + col.Name + " AS [" + asPrefix + col.Name + "]").ToArray());
		}

		/// <summary>
		/// Returns a comma separated list of all the columns in the table (as a single string) for use in a SELECT statement, but using an optional table alias, an optional AS prefix,
		/// and specifying an optional prefix for the source column names (as if an AS prefix were used in a subquery).
		/// Example ("temp", "person_", "p_"): "temp.person_ID AS p_ID, temp.person_FName AS p_FName, temp.person_LName AS p_LName", etc.
		/// Again, any of the options can be blank or null and they will not be used.
		/// </summary>
		public static string GetColumnsString<T>(string tableAlias, string sourceColumnPrefix, string asPrefix)
		{
			if (!String.IsNullOrEmpty(tableAlias))
				tableAlias += ".";
			if (String.IsNullOrEmpty(asPrefix))
				return String.Join(",", GetColumns<T>().Select(col => tableAlias + sourceColumnPrefix + col.Name).ToArray());
			return String.Join(",", GetColumns<T>().Select(col => tableAlias + sourceColumnPrefix + col.Name + " AS [" + asPrefix + col.Name + "]").ToArray());
		}


		public static string GetTablevarDeclaration<T>(string tableVarName_includingAtSymbol)
		{
			TableMetadata t = For<T>();
			string[] cols = t.Columns.Select(c => c.Name + " " + c.SqlTypeDeclaration).ToArray();
			string s = "DECLARE " + tableVarName_includingAtSymbol + " TABLE (" + String.Join(",", cols) + ");";
			return s;
		}

		/// <summary>
		/// This gets a comma separated list of column defs that can be used for a table declaration.
		/// Example return value:
		///		"Name varchar(20), Lat Decimal(19,4), Lng Decimal (19,4), Val float"
		/// etc.
		/// </summary>
		public static string GetColumnDefs<T>()
		{
			TableMetadata t = For<T>();
			string[] cols = t.Columns.Select(c => c.Name + " " + c.SqlTypeDeclaration).ToArray();
			string s = String.Join(",", cols);
			return s;
		}
	}
}
