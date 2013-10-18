using System.IO;
using System.Linq;
using System.Text;

namespace Noef.CodeGen.Generators
{
	public class RelationshipsConfigGenerator : CodeGeneratorBase
	{
		public RelationshipsConfigGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{ }

		public override void Run()
		{
			var fks = Settings.Fks
				.OrderBy(fk => fk.DependentTable)
				.ThenBy(fk => fk.DependentColumn);

			StringBuilder sb = new StringBuilder();

			string curTable = "";
			foreach(var fk in fks)
			{
				if (fk.DependentTable != curTable)
				{
					curTable = fk.DependentTable;
					sb.AppendLine();
					sb.AppendLine("<!-- FKs for " + curTable + " -->");
				}
				string principalPropertyName = fk.PrincipalTable;
				string dependentPropertyname = fk.DependentTable + (fk.IsFkPk ? "" : "_s");

				if (Settings.PropertyNameHints.ContainsKey(fk.FkName))
				{
					principalPropertyName = Settings.PropertyNameHints[fk.FkName].Item1;
					dependentPropertyname = Settings.PropertyNameHints[fk.FkName].Item2;
				}

				sb.AppendFormat("<relationship fk=\"{0}.{1}\" pk=\"{2}.{3}\" principalPropertyName=\"{4}\" dependentPropertyName=\"{5}\" />\r\n",
					fk.DependentTable,
					fk.DependentColumn,
					fk.PrincipalTable,
					fk.PrincipalColumn,
					principalPropertyName,
					dependentPropertyname);
			}
			Output.WriteLine(sb.ToString());
			Output.Flush();
		}

	}
}