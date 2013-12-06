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
// future plans for data annotations that help craft jquery/knockout/bootstrap ui controls
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
			//cls = cls.Replace("<#= DtoNamespace #>", Settings.DtoNamespace);
			Output.WriteLine(cls);
			return sb.ToString();
		}

	}
}
