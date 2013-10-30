using System;
using System.IO;
using System.Text;

namespace Noef.CodeGen.Generators
{
	/// <summary>
	/// This is experimental, and CB/SamStack specific (but I want to use the Noef.CodeGen data model. TODO: Expose that separately? MEF? T4?)
	/// </summary>
	public class JqUiGenerator : CodeGeneratorBase
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

		public JqUiGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{ }


		public override void Run()
		{
			Output.WriteLine();

			foreach (TableMapping table in Settings.TableMappings)
			{
				Output.WriteLine(getUiDef(table));
				Output.WriteLine();
				Output.WriteLine();
			}

			Output.WriteLine();
			Output.Flush();
		}


		private string getUiDef(TableMapping table)
		{
			StringBuilder sb = new StringBuilder();
			// TODO
			string cls = CLASS_TEMPLATE;
			cls = cls.Replace("<#= DtoNamespace #>", Settings.DtoNamespace);
			cls = cls.Replace("<#= ClassName #>", table.ClassName);
			cls = cls.Replace("<#= Properties #>", getPropertyDefs(table));
			cls = cls.Replace("<#= RelatedProperties #>", getRelatedPropertyDefs(table));
			Output.WriteLine(cls);
			return sb.ToString();
		}


		private string getRelatedPropertyDefs(TableMapping table)
		{
			StringBuilder sb = new StringBuilder();
			foreach(Relationship p in table.AsPrincipal)
			{
				sb.AppendLine("\t\t// (As principal)");
				sb.AppendLine("\t\t// (D) " + p.DependentTable.TableName + "." + p.DependentKey + " = (P) " + p.PrincipalTable.TableName + "." + p.PrincipalKey + (p.FkInfo == null ? "" : " (" + p.FkInfo.FkName + ")"));
				if (String.IsNullOrEmpty(p.DependentPropertyName))
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
				if (String.IsNullOrEmpty(d.PrincipalPropertyName))
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
