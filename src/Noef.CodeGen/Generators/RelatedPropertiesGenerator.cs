using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Noef.CodeGen.Generators
{
	public class RelatedPropertiesGenerator : CodeGeneratorBase
	{
		public static readonly string CLASS_TEMPLATE = @"
namespace {0}
{{
	using System;
	using System.Collections.Generic;

	public partial class {1}
	{{
{2}
	}}
}}
";

		public RelatedPropertiesGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{ }

		public override void Run()
		{
			foreach(TableMapping table in Settings.TableMappings)
				WriteClass(table);
			Output.Flush();
		}

		public void WriteClass(TableMapping table)
		{
			string relatedPropertyDefs = getRelatedPropertyDefs(table);
			Output.WriteLine(CLASS_TEMPLATE,
				Settings.DtoNamespace, table.ClassName, relatedPropertyDefs);
		}

		private string getRelatedPropertyDefs(TableMapping table)
		{
			StringBuilder sb = new StringBuilder();

			// There are 2 ways we set up related data objects:

			// 1. If this table has a FK to another table, this object has an instance of that object
			var asDependent = Settings.Fks.Where(fk => String.Equals(fk.DependentTable, table.TableName, StringComparison.CurrentCultureIgnoreCase));

			// 2. If another table has a FK back to this table, this object either has an instance of that object (1:0-1, if the FK is a PK as well),
			// or this object has a list of those objects (1:*)
			var asPrincipal = Settings.Fks.Where(fk => String.Equals(fk.PrincipalTable, table.TableName, StringComparison.CurrentCultureIgnoreCase));

			foreach(var fk in asDependent)
			{
				TableMapping relatedTable = Settings.TableMappings.First(t => String.Equals(t.TableName, fk.PrincipalTable, StringComparison.CurrentCultureIgnoreCase));
				sb.AppendLine("\t\t// " + fk.FkName + " (" + fk.DependentTable + "." + fk.DependentColumn + " = " + fk.PrincipalTable + "." + fk.PrincipalColumn + ")");
				sb.AppendFormat("\t\tpublic {0} {0} {{ get; set; }}\r\n", relatedTable.ClassName);
			}

			foreach(var fk in asPrincipal)
			{
				TableMapping relatedTable = Settings.TableMappings.First(t => String.Equals(t.TableName, fk.DependentTable, StringComparison.CurrentCultureIgnoreCase));

				if (fk.IsFkPk)
				{
					sb.AppendLine("\t\t// " + fk.FkName + " (" + fk.DependentTable + "." + fk.DependentColumn + " = " + fk.PrincipalTable + "." + fk.PrincipalColumn + ")");
					sb.AppendFormat("\t\tpublic {0} {0} {{ get; set; }}\r\n", relatedTable.ClassName);
				}
				else
				{
					// TODO: Pluralize the 2nd arg
					sb.AppendLine("\t\t// " + fk.FkName);
					sb.AppendFormat("\t\tpublic IList<{0}> {1} {{ get; set; }}\r\n", relatedTable.ClassName, relatedTable.ClassName);
				}
			}
			return sb.ToString();

		}

	}
}
