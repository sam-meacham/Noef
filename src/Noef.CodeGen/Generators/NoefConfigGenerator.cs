using System.IO;
using System.Text;

namespace Noef.CodeGen.Generators
{
	public class NoefConfigGenerator : CodeGeneratorBase
	{
		public static readonly string TEMPLATE = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<noefConfig xmlns=""urn:noef-config-1.1"">
	<appConfig>c:/inetpub/wwwroot/web.config</appConfig>

	<!-- Settings for your DAL (data access layer) class, which will be a single .cs file -->
	<!-- When running NoefGen.exe, include the ""Dal"" type to generate this -->
	<dal>
		<class><%= DalClassName %></class>
		<namespace><%= DalNamespace %></namespace>
		<connection>ConnectionStringName</connection>
	</dal>

	<!-- Settings for your dto class generation, which puts all of your dto classes into a single .cs file -->
	<!-- When running NoefGen.exe, include the ""Dtos"" type to generate this -->
	<dtos>
		<namespace><%= DtoNamespace %></namespace>
	</dtos>

	<!-- Settings for the metadata class generation, which will be a single .cs file -->
	<!-- When running NoefGen.exe, include the ""Dtos"" or ""Metadata"" type to generate this -->
	<metadata>
		<namespace><%= MetaNamespace %></namespace>
	</metadata>

	<!-- Settings for the individual tables, views, and table-valued-functions you want to create DTO types for -->
	<tables>
		<import name=""YourTable"" />
		<import name=""ExternalUserTable"" connection=""SomeOtherConnectionStringName"" />

		<!-- an example using more options -->
		<import name=""FullOptions"" class=""DifferentClassName"" database=""SomeOtherDBSameConnection"" excludedColumns=""Password,BinaryField,BigField"">
			<column name=""USER_ID"" propName=""UserID"" type=""int"" />
		</import>
	</tables>

	<!-- Settings for the FKs that relate your various imported table types.  These will create the related properties in your DTO types -->
	<relationships>
	</relationships>
</noefConfig>
";

		public NoefConfigGenerator(TextWriter output, ImportSettings settings)
			: base(output, settings)
		{ }


		public override void Run()
		{
			StringBuilder sb = new StringBuilder(TEMPLATE);
			sb
				.Replace("<%= DalClassName %>", Settings.DalClassName)
				.Replace("<%= DalNamespace %>", Settings.DalNamespace)
				.Replace("<%= DtoNamespace %>", Settings.DtoNamespace)
				.Replace("<%= MetaNamespace %>", Settings.MetadataNamespace);

			Output.Write(sb.ToString());
			Output.Flush();
		}
	}
}
