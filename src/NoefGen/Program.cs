using System;
using System.Collections.Generic;
using System.IO;
using Noef.CodeGen;
using Noef.CodeGen.Generators;

namespace NoefGen
{
	class Program
	{
		static void Main(string[] args)
		{
			string settingsFile = null;
			string outputTypes = null;

			// NDesk_Options is included in Noef.CodeGen.
			// Remember that an arg with "=" means "required" and one with ":" means "optional".
			var options = new OptionSet()
			{
				// The output type (required)
				{ "type=", @"The output type(s) you want to execute, comma separated.  Defaults to ALL types.
Output types are:
- Dtos: create the DTO classes that model your SQL tables.
- RelatedProperties: create a text file that describes the FK relationships of your tables, as modeled in your DTOs.
- RelationshipsConfig: create a text file with the xml markup you can put in your noef-config.xml to set up your FK relationships in the DTOs.
- Metadata: create the _metadata.cs file that will contain the metadata for your sql tables (and how they map to your DTOs), and the hydrator functions that will transform raw SQL results into your .NET DTO types.
- NoefDistro: generate the entire Noef codebase into a single redistributable .cs file (_Noef.cs)
- Dal: Generate _Dal.cs, which is your singleton entry point for all your data access.
- HttpModule: Generate an IHttpModule class you can put in your web.config for easy use from an ASP.NET application.

Example:
	// generate all types
	NoefGen.exe --type=Dtos,RelatedProperties,RelationshipsConfig,Metadata,NoefDistro,Dal,HttpModule
", v => outputTypes = v },

				// A settings file (see noef-config.xsd for the xml schema)
				{ "s|settings=", @"Specify the settings file you want to use.  Defaults to noef-gen.xml.
Example:
	NoefGen.exe --settings=noef-gen.xml
", v => settingsFile = v },
			};

			// Parse the options the first time (we need to get the output types, which can ONLY be specified on the command line, not from the settings file)
			options.Parse(args);

			// are they running with NO args?  Just ALL defaults?  Our processing behavior will use defaults and be slightly modified (interactive)
			bool noArgs = outputTypes == null && settingsFile == null;

			// if they don't specify an output type, just run ALL output types
			if (String.IsNullOrEmpty(outputTypes))
				outputTypes = "Dtos,RelatedProperties,RelationshipsConfig,Metadata,NoefDistro,Dal,HttpModule,Ui";
			if (String.IsNullOrEmpty(settingsFile))
				settingsFile = "noef-config.xml";

			// Create the ImportSettings from the file they specified
			ImportSettings settings = new ImportSettings();
			if (!File.Exists(settingsFile))
				// only create the config file, since there isn't one
				outputTypes = "ConfigFile";
			else
				settings.ApplyFromFile(settingsFile);

			// if they didn't specify any args, show them the usage, and then run with defaults
			if (noArgs)
			{
				printUsage(options);
				Console.WriteLine();
				Console.WriteLine(@"No settings file or output types specified.  Defaults will be used.");
				Console.WriteLine(@"Settings file: " + settingsFile);
				Console.WriteLine(@"Output types: " + outputTypes);
				Console.WriteLine();
			}

			// Process the output types string
			string[] outputTypesParts = outputTypes.Split(new [] {','});
			foreach (string strType in outputTypesParts)
			{
				OutputType enumType;
				if (!Enum.TryParse(strType, out enumType))
				{
					Console.WriteLine("Invalid output type: " + strType);
					Environment.Exit(-1);
				}
				settings.AddOutputType(enumType);
			}

			// Print the calculated settings to stderr, for the user to see
			Console.WriteLine(settings.ToString());

			// run the code generators
			List<CodeGeneratorBase> codeGenerators = settings.GetCodeGenerators();
			foreach(CodeGeneratorBase gen in codeGenerators)
				gen.Run();
		}


		private static void printUsage(OptionSet options)
		{
			Console.WriteLine(@"
NoefGen.exe uses an xml config file (noef-config.xml by default),
connects to your database, gets data about the tables you're going to import,
and generates various .cs code files as output.

");
			options.WriteOptionDescriptions(Console.Out);
		}

	}
}
