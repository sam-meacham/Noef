using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Noef.CodeGen.Generators
{
	public class DtoGenerator : CodeGeneratorBase
	{
		public static readonly string CLASS_TEMPLATE = @"
namespace <#= DtoNamespace #>
{
	public partial class <#= ClassName #>
	{
		// Columns
<#= Properties #>
		// Related properties
<#= RelatedProperties #>
	}
}
";

		public static readonly string ENUM_TEMPLATE = @"
namespace <#= DtoNamespace #>
{
	public enum <#= EnumName #>
	{
<#= Fields #>
	}
}
";

		public static readonly string ENUM_DESC_TEMPLATE = @"
namespace <#= DtoNamespace #>
{
	public static class EnumDescriptions
	{
<#= Dictionaries #>
	}
}
";

		public DtoGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{ }

		public override void Run()
		{
			Output.WriteLine("using System;");
			Output.WriteLine("using System.Collections.Generic;");
			Output.WriteLine("using System.ComponentModel; // for the [Description] attribute on enum fields");
			Output.WriteLine();
			Output.WriteLine("// ReSharper disable PartialTypeWithSinglePart");
			Output.WriteLine();

			foreach (TableMapping table in Settings.TableMappings)
				writeClass(table);

			writeEnums();

			Output.WriteLine();
			Output.WriteLine("// ReSharper restore PartialTypeWithSinglePart");
			Output.WriteLine();

			Output.Flush();
		}


		private static string getEnumFieldName(string stringValue)
		{
			StringBuilder sb = new StringBuilder(stringValue);
			char[] badChars = @" `~!@#$%^&*()_+=-\|[]{},./<>?;:".ToArray();
			char[] digits = "1234567890".ToArray();

			// See if the string starts with a number (which isn't allowed...)
			if (digits.Contains(sb[0]))
				sb[0] = '_';

			// Now replace all other invalid chars with '_'
			foreach (char c in badChars)
				sb = sb.Replace(c, '_');

			return sb.ToString();
		}

		private void writeEnums()
		{
			// First write the enums
			foreach (TableMapping table in Settings.TableMappings.Where(t => t.IsEnum))
			{
				string enm = ENUM_TEMPLATE;
				enm = enm.Replace("<#= DtoNamespace #>", Settings.DtoNamespace);
				enm = enm.Replace("<#= EnumName #>", table.ClassName);
				enm = enm.Replace("<#= Fields #>", getEnumFiedDefs(table));
				Output.WriteLine(enm);
			}

			// Then create some Descriptions dictionaries (useful for populating UIs, etc)
			StringBuilder sbDicts = new StringBuilder();
			foreach (TableMapping table in Settings.TableMappings.Where(t => t.IsEnum))
			{
				sbDicts.AppendLine("\t\tpublic static readonly Dictionary<" + table.ClassName + ", string> " + table.ClassName + "_ = new Dictionary<" + table.ClassName + ", string>()")
					.AppendLine("\t\t{");
				for (int i = 0; i < table.EnumKeys.Length; i++)
				{
					string fieldName = getEnumFieldName(table.EnumLabels[i]);
					sbDicts.AppendLine("\t\t\t{ " + table.ClassName + "." + fieldName + ", \"" + table.EnumLabels[i] + "\" },");
				}
				sbDicts.AppendLine("\t\t};");
				sbDicts.AppendLine();
			}
			string dicts = ENUM_DESC_TEMPLATE
				.Replace("<#= DtoNamespace #>", Settings.DtoNamespace)
				.Replace("<#= Dictionaries #>", sbDicts.ToString());
			Output.WriteLine(dicts);
		}


		private string getEnumFiedDefs(TableMapping table)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < table.EnumKeys.Length; i++)
			{
				string fieldName = getEnumFieldName(table.EnumLabels[i]);
				sb.AppendLine("\t\t[Description(\"" + table.EnumLabels[i] + "\")]");
				sb.AppendLine("\t\t" + fieldName + " = " + table.EnumKeys[i] + ",");
			}
			return sb.ToString();
		}


		private void writeClass(TableMapping table)
		{
			if (!table.IsEnum)
			{
				string cls = CLASS_TEMPLATE;
				cls = cls.Replace("<#= DtoNamespace #>", Settings.DtoNamespace);
				cls = cls.Replace("<#= ClassName #>", table.ClassName);
				cls = cls.Replace("<#= Properties #>", getPropertyDefs(table));
				cls = cls.Replace("<#= RelatedProperties #>", getRelatedPropertyDefs(table));
				Output.WriteLine(cls);
			}
		}



		private string getRelatedPropertyDefs(TableMapping table)
		{
			StringBuilder sb = new StringBuilder();
			foreach(Relationship p in table.AsPrincipal)
			{
				sb.AppendLine("\t\t// (As principal)");
				sb.AppendLine("\t\t// (D) " + p.DependentTable.TableName + "." + p.DependentKey + " = (P) " + p.PrincipalTable.TableName + "." + p.PrincipalKey + (p.FkInfo == null ? "" : " (" + p.FkInfo.FkName + ")"));
				if (String.IsNullOrWhiteSpace(p.DependentPropertyName))
				{
					sb.AppendLine("\t\t// (prop name in the noef cfg file is blank or missing; property excluded)");
					sb.AppendLine("\t\t// " + p.GetDependentPropertyDeclaration());
					sb.AppendLine();
				}
				else
				{
					sb.AppendLine("\t\t" + p.GetDependentPropertyDeclaration());
					sb.AppendLine();
				}

				sb.AppendLine();
			}

			foreach(Relationship d in table.AsDependent)
			{
				sb.AppendLine("\t\t// (As dependent)");
				sb.AppendLine("\t\t// (D) " + d.DependentTable.TableName + "." + d.DependentKey + " = (P) " + d.PrincipalTable.TableName + "." + d.PrincipalKey + (d.FkInfo == null ? "" : " (" + d.FkInfo.FkName + ")"));
				if (String.IsNullOrWhiteSpace(d.PrincipalPropertyName))
				{
					sb.AppendLine("\t\t// (prop name in the noef cfg file is blank or missing; property excluded)");
					sb.AppendLine("\t\t// " + d.GetPrincipalPropertyDeclaration());
					sb.AppendLine();
				}
				else
				{
					sb.AppendLine("\t\t" + d.GetPrincipalPropertyDeclaration());
					sb.AppendLine();
				}
			}
			return sb.ToString();
		}


		public string getPropertyDefs(TableMapping table)
		{
			StringBuilder sb = new StringBuilder();
			foreach (Column col in table.Columns)
				if (!col.IsPropertyExcluded)
					sb.AppendLine(getPropertyDef(col));
			return sb.ToString();
		}

		public string getPropertyDef(Column column)
		{
			return String.Format("\t\tpublic {0} {1} {{ get; set; }}",
				column.GetClrTypeName(), column.PropertyName);
		}

	}
}
