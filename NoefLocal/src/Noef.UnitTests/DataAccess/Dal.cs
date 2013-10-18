using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Noef.UnitTests.DataAccess
{
	public class Dal : NoefDal
	{
		public override string ConnectionStringName
		{
			get { return "NoefUnitTests"; }
		}

		public override DbType DbType
		{
			get { return DbType.SqlCE; }
		}

		/// <summary>
		/// Resets all data in the database to its initial state
		/// </summary>
		public void ResetDatabase()
		{
			SqlCeConnection cn = new SqlCeConnection(GetConnectionString());
			if (!File.Exists(cn.Database))
				createDatabase();
			string[] sqls = getEmbeddedFile("Sql.ResetDatabase.sql").Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			executeCommands(sqls);
		}

		private void createDatabase()
		{
			SqlCeEngine engine = new SqlCeEngine(GetConnectionString());
			engine.CreateDatabase();
			string[] sqls = getEmbeddedFile("Sql.CreateDatabase.sql").Split(new [] { "GO" }, StringSplitOptions.RemoveEmptyEntries);
			executeCommands(sqls);
		}

		private string getEmbeddedFile(string filename)
		{
			Assembly asm = Assembly.GetExecutingAssembly();
			Stream stream = asm.GetManifestResourceStream("Noef.UnitTests." + filename);
			if (stream == null)
				throw new Exception("Could not get a stream for the embedded resource: " + filename);
			StreamReader streamReader = new StreamReader(stream);
			string text = streamReader.ReadToEnd();
			streamReader.Close();
			return text;
		}

		private void executeCommands(IEnumerable<string> commands)
		{
			string[] sqls =
				commands
				.Select(s => s.Trim())
				.Where(s => s != String.Empty)
				.ToArray();

			SqlCeConnection cn = new SqlCeConnection(GetConnectionString());
			cn.Open();
			foreach (string sql in sqls)
			{
				SqlCeCommand cmd = new SqlCeCommand(sql, cn);
				try
				{
					cmd.ExecuteNonQuery();
				}
				catch (Exception)
				{
					Console.WriteLine("Error executing sql:");
					Console.WriteLine(sql);
					throw;
				}
			}
			cn.Close();
		}

	}
}
