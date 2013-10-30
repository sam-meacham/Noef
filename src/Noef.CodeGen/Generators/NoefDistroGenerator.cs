using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Ionic.Zip;


namespace Noef.CodeGen.Generators
{
	public class NoefDistroGenerator : CodeGeneratorBase
	{
		public string NoefNamespace { get; set; }
		public bool SupportSqlCE { get; set; }
		public bool SupportSqlTableValuedParams { get; set; }

		// Constructor
		public NoefDistroGenerator(TextWriter output, ImportSettings settings, string noefNamespace = "Noef")
			: base(output, settings)
		{
			NoefNamespace = noefNamespace;
			SupportSqlCE = false;
			SupportSqlTableValuedParams = false;
		}


		public string[] GetConditionalIncludeSymbols()
		{
			List<string> symbols = new List<string>();
			if (SupportSqlCE)
				symbols.Add("SQL_CE");
			if (SupportSqlTableValuedParams)
				symbols.Add("SQL_TABLE_VALUED_PARAMS");
			return symbols.ToArray();
		}


		public Dictionary<string, string> GetNoefSourceFiles()
		{
			Assembly asm = Assembly.GetExecutingAssembly();
			Stream zipstream = asm.GetManifestResourceStream("Noef.CodeGen.NoefSrc.zip");
			if (zipstream == null)
				throw new Exception("NoefSrc.zip was not properly embedded in the Noef.CodeGen assembly");
			ZipFile zip = ZipFile.Read(zipstream);
			Dictionary<string, string> srcFiles = new Dictionary<string, string>();

			foreach (ZipEntry entry in zip.Entries)
			{
				if (entry.FileName.EndsWith("AssemblyInfo.cs"))
					continue;
				MemoryStream stream = new MemoryStream();
				entry.Extract(stream);
				stream.Seek(0, 0);
				StreamReader reader = new StreamReader(stream);
				srcFiles[entry.FileName] = reader.ReadToEnd();
				reader.Close();
			}
			return srcFiles;
		}


		public string[] GetDistinctUsingDirectives()
		{
			// This will contain all of the using directives, with no duplicates.
			// Note that some using directives are wrapped in conditional includes (#if, #endif).  For those usings, the entries
			// will be multiline, and the conditional directives are included.
			List<string> usings = new List<string>();
			string[] conditionalIncludes = GetConditionalIncludeSymbols();
			Dictionary<string, string> srcFiles = GetNoefSourceFiles();
			foreach(var srcFile in srcFiles)
			{
				// It's easier to process each file if we have the soure in individual lines
				string[] lines = srcFile.Value.Split(new [] { Environment.NewLine }, StringSplitOptions.None);

				bool inIf = false;				// whether or not we're currently in an "#if" conditional
				bool includeIf = false;			// if we ARE in an "#if" conditional, should we be including the entires?
				string multiLineTemp = "";		// "using" statements inside the "#if" conditionals are single entries (multi-line). This is the one currently being built up.
				foreach(string line in lines)
				{
					// Are we currently inside an "#if" conditional?
					if (inIf)
					{
						if(includeIf)
						{
							if (line.StartsWith("#endif"))
							{
								inIf = false;
								multiLineTemp += line.Trim();
								usings.Add(multiLineTemp);
							}
							else
							{
								multiLineTemp += line.Trim() + "\r\n";
							}
						}
						else if (line.StartsWith("#endif"))
						{
							inIf = false;
						}
					}
					else if (line.StartsWith("#if"))
					{
						inIf = true;
						string symbol = line.Split(' ')[1];
						if (conditionalIncludes.Contains(symbol))
						{
							includeIf = true;
							multiLineTemp = "\r\n" + line.Trim() + "\r\n";
						}
						else
						{
							includeIf = false;
						}
					}
					else if (line.StartsWith("using"))
					{
						usings.Add(line.Trim());
					}
					else if (line.StartsWith("namespace"))
					{
						// we're past the using directives in this file. Move on.
						break;
					}
				}
			}

			return usings
				.Distinct()
				.OrderByDescending(u => u)
				.ToArray();
		}


		public Dictionary<string, StringBuilder> ParseNoefSource()
		{
			// Get all .cs files in the Noef directory
			Dictionary<string, string> srcFiles = GetNoefSourceFiles();

			// We'll collect the source for each unique namespace
			Dictionary<string, StringBuilder> dict = new Dictionary<string, StringBuilder>();

			// These won't work with whitespace (leading OR trailing), so make sure the source is formatted pretty and doesn't do anything stupid.
			Regex rxNamespace = new Regex(@"
^namespace\ (?<ns>(\w+\.?)+)
\r$\n
	{
\r$\n
		(?<inner>.*)
\r$\n
	}
\r$\n", RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Multiline);

			foreach(var srcFile in srcFiles)
			{
				// Extract the namespace for the code in this file, and then pull just the inner code
				Match match = rxNamespace.Match(srcFile.Value);
				string ns = match.Groups["ns"].Value;
				string innerCode = match.Groups["inner"].Value;

				if (!dict.ContainsKey(ns))
					dict.Add(ns, new StringBuilder());

				dict[ns].AppendLine("//");
				dict[ns].AppendLine("// " + Path.GetFileName(srcFile.Key));
				dict[ns].AppendLine("//");
				dict[ns].AppendLine(innerCode);
				dict[ns].AppendLine();
			}
			return dict;
		}


		public override void Run()
		{
			if (Settings.DalBaseClassName != "NoefDal" && !Settings.ForceGenNoefDistro)
			{
				Output.WriteLine("// No Noef distribution generated, because your DAL class specified a base class in your noef-config.xml");
				Output.WriteLine("// The intended usage for that is that class already subclasses NoefDal");
				Output.Flush();
				return;
			}

			Assembly asm = Assembly.GetExecutingAssembly();
			FileVersionInfo info = FileVersionInfo.GetVersionInfo(asm.Location);
			string versionString = info.ProductVersion; // for noefgen, ProductVersion and FileVersion are always sync'd

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("// Noef single cs file distribution");
			// Sam Meacham - 10/30/2013 - removing timestamp, it causes svn/git conflicts.
			//sb.AppendLine("// Generated: " + DateTime.Now);
			sb.AppendLine("// NoefGen version: " + versionString);
			sb.AppendLine("// Root namespace: " + NoefNamespace);
			sb.AppendLine("// SQL CE support: " + SupportSqlCE);
			sb.AppendLine("// SQL Server Table Valued Params support: " + SupportSqlTableValuedParams);
			sb.AppendLine();

			// conditional symbols
			string[] symbols = GetConditionalIncludeSymbols();
			foreach(string symbol in symbols)
				sb.AppendLine("#define " + symbol);
			sb.AppendLine();

			// "using" statements
			string[] usings = GetDistinctUsingDirectives();
			foreach (string u in usings)
				sb.AppendLine(u);
			sb.AppendLine();

			// individual namespaces and all their contained classes
			var sources = ParseNoefSource();
			foreach(var pair in sources)
			{
				sb.AppendLine("namespace " + pair.Key);
				sb.AppendLine("{");
				sb.AppendLine(pair.Value.ToString()); // this is all of the classes in namespace X, in a single string
				sb.AppendLine("}");
				sb.AppendLine();
			}

			// Fix the "Noef" namespace if they've provided a different one
			if (NoefNamespace != "Noef")
			{
				sb.Replace("using Noef", "using " + NoefNamespace);
				sb.Replace("namespace Noef", "namespace " + NoefNamespace);
			}

			Output.WriteLine(sb.ToString());
			Output.Flush();
		}

	}
}
