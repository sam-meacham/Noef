using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Noef.CodeGen.Generators;

namespace Noef.CodeGen
{
	public class ImportSettings
	{
		private static readonly string DEFAULT_SCHEMA = "dbo";

		// The type of output to generate
		private readonly IList<OutputType> m_outputTypes;
		
		// connection strings parsed from the <connectionStrings> section (which is exactly like one in an app.config or web.config)
		private IDictionary<string, string> m_cnStrings;

		// Code generation settings
		public string OutputFolder { get; set; }
		public string DalConnectionName { get; set; }
		public string NoefNamespace { get; set; }
		public string DtoNamespace { get; set; }
		public string MetadataNamespace { get; set; }
		public string DalNamespace { get; set; }
		public string DalClassName { get; set; }
		public string DalBaseClassName { get; set; }

		// Database mapping info
		/// <summary>
		/// Key = name of table in database.
		/// Value = class name (will never be null, even if the tablename and classname are the same)
		/// </summary>
		public IList<TableMapping> TableMappings { get; set; }
		public List<Relationship> Relationships { get; set; }
		
		// Post processed (not part of the settings file)
		public IList<FkInfo> Fks { get; set; }

		// The keys here are the names of FKs in the database.  The string tuple is the name of the pricipal and dependent property names that should be used, instead of just defaulting to the table name.
		public Dictionary<string, Tuple<string, string>> PropertyNameHints = new Dictionary<string, Tuple<string, string>>();

		/// <summary>
		/// Default constructor (sets default values)
		/// </summary>
		public ImportSettings()
		{
			OutputFolder = Environment.CurrentDirectory;
			m_outputTypes = new List<OutputType>(); // if empty, will process all types
			DalNamespace = "YourNamespace";
			NoefNamespace = null; // will default to the DAL's namespace
			DalClassName = "YourDal";
			DalBaseClassName = "NoefDal";
			DtoNamespace = null; // will default to the DAL's namespace
			MetadataNamespace = null;  // will default to the DAL's namespace
			TableMappings = new List<TableMapping>();
			Relationships = new List<Relationship>();
		}

		public void AddOutputType(OutputType type)
		{
			if (!m_outputTypes.Contains(type))
				m_outputTypes.Add(type);
		}

		public void ApplyFromFile(string file)
		{
			// wherever this noef-config.xml is, that's where we want to output the files as well.
			OutputFolder = Path.GetDirectoryName(file);

			// read the config file and apply the settings
			string configXml = File.ReadAllText(file);
			Apply(configXml);
		}

		private void getConnectionStrings(string appConfigFile)
		{
			m_cnStrings = new Dictionary<string, string>();
			if (!File.Exists(appConfigFile))
				throw new Exception("Could not find config file: " + appConfigFile);
			XDocument xdocCnStrings = XDocument.Parse(File.ReadAllText(appConfigFile));
			if (xdocCnStrings.Root == null)
				throw new Exception("That config file has no root element: " + appConfigFile);
			XElement connectionStrings = xdocCnStrings.Root.Element("connectionStrings");
			if (connectionStrings != null)
			{
				//foreach (XElement cnString in connectionStrings.Elements(ns + "add"))
				foreach (XElement cnString in connectionStrings.Elements("add"))
				{
					string cnName = cnString.Attribute("name").ValueOrNull();
					string cnVal = cnString.Attribute("connectionString").ValueOrNull();
					if (String.IsNullOrWhiteSpace(cnName) || String.IsNullOrWhiteSpace(cnVal))
						throw new Exception("connection strings must have a name and connectionString attribute");
					m_cnStrings.Add(cnName, cnVal);
				}
			}
		}
		
		public void Apply(string configXml)
		{
			XDocument xdoc = XDocument.Parse(configXml);
			XNamespace ns = XNamespace.Get("urn:noef-config-1.1");

			XElement root = xdoc.Root;
			if (root == null)
				throw new Exception("You must have a root <noefConfig> node.");
			if (root.Name.LocalName != "noefConfig")
				throw new Exception("You must have a root <noefConfig> node.");

			// TODO: This could be a handy extension point. If people wanted to create their own extensions for codegen, based on settings in the xml config.
			// For example, custom generators for creating UI controls based on the Noef.CodeGen model.

			// TODO: Warn about unrecognized elements.

			// sections in the xml
			string appConfigFile = root.Element(ns + "appConfig").ValueOrNull();
			XElement noef = root.Element(ns + "noef");
			XElement dal = root.Element(ns + "dal");
			XElement dtos = root.Element(ns + "dtos");
			XElement metadata = root.Element(ns + "metadata");
			XElement tables = root.Element(ns + "tables");
			XElement relationships = root.Element(ns + "relationships");

			// get connection strings from the app config file
			getConnectionStrings(appConfigFile);

			// <dal>
			if (dal == null)
				throw new Exception("The <dal> section is required!");
			var classElement = dal.Element(ns + "class");
			DalClassName = classElement.ValueOrNull() ?? DalClassName;
			
			// see if they need a specific base class that's already subclassing NoefDal
			// This will also signal that a Noef distribution should not be generated.
			if (classElement != null)
				DalBaseClassName = classElement.Attribute("base").ValueOrNull() ?? DalBaseClassName;

			DalNamespace = dal.Element(ns + "namespace").ValueOrNull() ?? DalNamespace;
			DalConnectionName = dal.Element(ns + "connection").ValueOrNull() ?? DalConnectionName;

			// <noef>
			if (noef != null)
				NoefNamespace = noef.Element(ns + "namespace").ValueOrNull();
			NoefNamespace = NoefNamespace ?? DalNamespace;

			// <dtos>
			if (dtos != null)
				DtoNamespace = dtos.Element(ns + "namespace").ValueOrNull();
			DtoNamespace = DtoNamespace ?? DalNamespace;

			// <metadata>
			if (metadata != null)
				MetadataNamespace = metadata.Element(ns + "namespace").ValueOrNull();
			MetadataNamespace = MetadataNamespace ?? DalNamespace;

			// <tables>
			if (tables == null)
				throw new Exception("<tables> is required");

			using (SqlConnection cn = GetNewOpenConnection(DalConnectionName))
			{
				foreach (XElement import in tables.Elements(ns + "import"))
				{
					string tableName = import.Attribute("name").ValueOrNull();
					if (String.IsNullOrWhiteSpace(tableName))
						throw new Exception("name attribute is required (the name of the table)");

					string className = import.Attribute("class").ValueOrNull() ?? tableName;
					string baseClass = import.Attribute("baseClass").ValueOrNull();
					string connection = import.Attribute("connection").ValueOrNull() ?? DalConnectionName;
					string database = import.Attribute("database").ValueOrNull() ?? cn.Database;
					string schema = import.Attribute("schema").ValueOrNull() ?? DEFAULT_SCHEMA;

					string strExcludedColumns = import.Attribute("excludedColumns").ValueOrNull();
					string[] excludedColumns = new string[0];
					if (strExcludedColumns != null)
						excludedColumns = strExcludedColumns.Split(',');

					string strExcludedProperties = import.Attribute("excludedProperties").ValueOrNull();
					string[] excludedProperties = new string[0];
					if (strExcludedProperties != null)
						excludedProperties = strExcludedProperties.Split(',');

					string strEnum = import.Attribute("enum").ValueOrNull();
					bool isEnum = false;
					if (strEnum != null)
						isEnum = strEnum.ToLower() == "true";

					string enumKey = import.Attribute("enumKey").ValueOrNull() ?? "ID";
					string enumLabel = import.Attribute("enumLabel").ValueOrNull() ?? "Name";

					TableMapping mapping = isEnum
						? new TableMapping(tableName, database, schema, className, enumKey, enumLabel)
						: new TableMapping(tableName, database, schema, className, baseClass, excludedColumns, excludedProperties);

					// Get the metadata from sql server and populate the Columns list
					if (!String.Equals(connection, DalConnectionName, StringComparison.OrdinalIgnoreCase))
					{
						// this table uses a different connection
						mapping.ConnectionName = connection;
						if (String.IsNullOrWhiteSpace(mapping.ConnectionName))
							mapping.ConnectionName = null;

						using (SqlConnection cn2 = new SqlConnection(m_cnStrings[connection]))
						{
							// since this table uses a different connection, it's TableName will be the fully qualified name (always)
							mapping.TableName = cn2.Database + "." + mapping.Schema + "." + tableName;

							cn2.Open();
							TableMapping.PopulateColumns(cn2, mapping);
							cn2.Close();
						}
					}
					else if (!String.Equals(database, cn.Database, StringComparison.OrdinalIgnoreCase))
					{
						// The databases are different, but the current table can still use the main connection
						mapping.TableName = database + "." + mapping.Schema + "." + tableName;
						string mainDB = cn.Database;
						cn.ChangeDatabase(database);
						TableMapping.PopulateColumns(cn, mapping);
						cn.ChangeDatabase(mainDB);
					}
					else
					{
						// this table uses the same connection as the main DAL connection
						TableMapping.PopulateColumns(cn, mapping);
					}

					// Now we need to make some changes based on any additional settings in the config file
					IEnumerable<XElement> columns = import.Elements(ns + "column");
					foreach (XElement elCol in columns)
					{
						string colName = elCol.Attribute("name").ValueOrNull();
						if (colName == null)
							throw new Exception("If you use a <column> element under your <import> element, the \"name\" attribute is required");
						Column columnMeta = mapping.Columns.SingleOrDefault(c => String.Equals(c.ColumnName, colName, StringComparison.OrdinalIgnoreCase));
						if (columnMeta == null)
							throw new Exception("Column not found: " + tableName + "." + colName);
						string propName = elCol.Attribute("propName").ValueOrNull();
						string type = elCol.Attribute("type").ValueOrNull();
						if (propName != null)
							columnMeta.PropertyName = propName;
						if (type != null)
						{
							columnMeta.IsUserType = true;
							columnMeta.Type = type;
						}
					}

					// duplicates are simply overwritten
					TableMappings.Add(mapping);
				}
				cn.Close();
			}

			// Now that we have the list of tables to import, let's populate our Tables and FkInfo collections
			// (They are required for processing the relationship correctly, which we do next)
			PopulateFkData();

			// process the <relationships> section
			if (relationships != null)
			{
				foreach(XElement relationship in relationships.Elements(ns + "relationship"))
				{
					// All values are required, so we don't use ValueOrNull()
					XAttribute pk = relationship.Attribute("pk");
					if (pk == null)
						throw new Exception("The \"pk\" attribute is required in the <relationship> element");
					XAttribute fk = relationship.Attribute("fk");
					if (fk == null)
						throw new Exception("The \"fk\" attribute is required in the <relationship> element");
					// If one is missing, it simply means you don't create that relationship.
					XAttribute principalPropertyName = relationship.Attribute("principalPropertyName");
					XAttribute dependentPropertyName = relationship.Attribute("dependentPropertyName");
					XAttribute sequenceType = relationship.Attribute("dependentSequenceType");

					Relationship rel = new Relationship(TableMappings, pk.Value, fk.Value, principalPropertyName.ValueOrNull(), dependentPropertyName.ValueOrNull());
					if (sequenceType != null)
						rel.SequenceType = (SequenceType)Enum.Parse(typeof(SequenceType), sequenceType.Value, true);

					rel.SetFk(Fks);

					// the rel.IsMany will already be set based on the database design.
					// If a value was provided in the config, override it.
					string cardinality = relationship.Attribute("cardinality").ValueOrNull();
					if (cardinality != null)
						rel.IsMany = cardinality == "many";

					Relationships.Add(rel);
				}
			}
		}

		/// <summary>
		/// This is called from Noef.CodeGen to get the code generators specified by the command line.
		/// </summary>
		/// <returns></returns>
		public List<CodeGeneratorBase> GetCodeGenerators()
		{
			Func<string, TextWriter> fn_getWriter = outputFile =>
				{
					string path = Path.Combine(OutputFolder, outputFile);
					return new StreamWriter(path);
				};

			List<CodeGeneratorBase> generators = new List<CodeGeneratorBase>();
			if (m_outputTypes.Contains(OutputType.Dtos))
				generators.Add(new DtoGenerator(fn_getWriter("_Dtos.cs"), this));
			if (m_outputTypes.Contains(OutputType.RelatedProperties))
				generators.Add(new RelatedPropertiesGenerator(fn_getWriter("_relatedProperties.txt"), this));
			if (m_outputTypes.Contains(OutputType.RelationshipsConfig))
				generators.Add(new RelationshipsConfigGenerator(fn_getWriter("_fks.txt"), this));
			if (m_outputTypes.Contains(OutputType.NoefDistro))
				generators.Add(new NoefDistroGenerator(fn_getWriter("_Noef.cs"), this, NoefNamespace));
			if (m_outputTypes.Contains(OutputType.Dal))
				generators.Add(new DalGenerator(fn_getWriter("_Dal.cs"), this));
			if (m_outputTypes.Contains(OutputType.Metadata))
				generators.Add(new MetadataGenerator(fn_getWriter("_Metadata.cs"), this));
			if (m_outputTypes.Contains(OutputType.ConfigFile))
				generators.Add(new NoefConfigGenerator(fn_getWriter("noef-config.xml"), this));
			if (m_outputTypes.Contains(OutputType.HttpModule))
				generators.Add(new HttpModuleGenerator(fn_getWriter("_DalHttpModule.cs"), this));
			if (m_outputTypes.Contains(OutputType.Ui))
				generators.Add(new JqUiGenerator(fn_getWriter("_ui.js"), this));
			
			return generators;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			string types = "(all: " + String.Join(",", Enum.GetNames(typeof (OutputType))) + ")";
			if (m_outputTypes.Count > 0)
				types = String.Join(",", m_outputTypes.Select(t => t.ToString()));
			sb.AppendLine("Output types: " + types);
			sb.AppendLine("Connection string: " + DalConnectionName);
			sb.AppendLine("Dto Namespace: " + DtoNamespace);
			sb.AppendLine("Database objects to import:");
			foreach(var mapping in TableMappings)
			{
				sb.Append("\t" + mapping.TableName); // the table name
				sb.AppendLine(" => " + mapping.ClassName); // the class name
			}
			return sb.ToString();
		}


		public void PopulateFkData()
		{
			using (SqlConnection cn = GetNewOpenConnection(DalConnectionName))
			{
				Fks = FkInfo.GetAll(cn, TableMappings.Select(mapping => mapping.TableName));
				cn.Close();	
			}
		}


		public SqlConnection GetNewOpenConnection(string cnStringName)
		{
			SqlConnection cn = null;
			try
			{
				string cnString = m_cnStrings[cnStringName];
				cn = new SqlConnection(cnString);
				cn.Open();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Could not open the SqlConnection");
				Console.WriteLine(ex.Message);
				Environment.Exit(-1);
			}
			return cn;
		}


		public string GetDalConnectionString()
		{
			return m_cnStrings[DalConnectionName];
		}

	}
}
