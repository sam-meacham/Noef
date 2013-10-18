using System.IO;

namespace Noef.CodeGen.Generators
{
	public abstract class CodeGeneratorBase
	{
		public TextWriter Output { get; set; }
		public ImportSettings Settings { get; set; }

		protected CodeGeneratorBase(TextWriter output, ImportSettings settings)
		{
			Output = output;
			Settings = settings;
		}

		public abstract void Run();

	}
}
