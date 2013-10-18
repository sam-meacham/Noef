using System;
using System.Collections.Generic;
using System.Data;
using NUnit.Framework;
using Noef.UnitTests.DataAccess;
using System.Linq;

namespace Noef.UnitTests
{
	/// <summary>
	/// These tests ONLY test methods in HydrateExtensions.cs.
	/// Check DalTests for tests that test methods in NoefDal.cs (which in turn call many of the methods in HydrateExtensions, but I want separate tests all the same).
	/// </summary>
	public class HydrateTests : NoefTestBase
	{
		public enum Colors
		{
			White = 0,
			Black,
			Red,
			Green
		}

		public class Foo
		{
			public bool Bool { get; set; }
			public bool? NullableBool { get; set; }
			public Colors Color { get; set; }
			public Colors? NullableColor { get; set; }
		}

		[Test]
		public void ConverstionTests_Temp()
		{
			IList<object[]> rows = new List<object[]>()
				{
					new object[] { true, null, Colors.Red, null },
					new object[] { true, true, Colors.Red, Colors.Red },
					new object[] { true, 0, Colors.Red, 0 },
				};

			foreach (var row in rows)
				row.Hydrate<Foo>();
		}

		[Test]
		public void HydrateListOfString_FromReader_1()
		{
			string sql = "SELECT LOWER(name) as name FROM Project";
			IDataReader reader = m_dal.CreateCommand(sql).ExecuteReader();
			IList<string> names = reader.HydrateList<string>();
			Assert.IsNotNull(names);
			Assert.Greater(names.Count, 0);
			foreach(string name in names)
				Console.WriteLine(name);
		}

		[Test]
		public void HydrateListOfString_FromReader_2()
		{
			string sql = "SELECT 'asdf' as DummyColumn, LOWER(name) as name FROM Project";
			IDataReader reader = m_dal.CreateCommand(sql).ExecuteReader();
			IList<string> names = reader.HydrateList<string>(1, 1);
			Assert.IsNotNull(names);
			Assert.Greater(names.Count, 0);
			foreach(string name in names)
				Console.WriteLine(name);
		}

		[Test]
		public void HydrateListOfString_FromReader_3()
		{
			string sql = "SELECT 'asdf' as DummyColumn, NULL as NullColumn, LOWER(name) as name FROM Project";
			IDbCommand cmd = m_dal.CreateCommand(sql);
			IDataReader reader = cmd.ExecuteReader();
			IList<string> names = reader.HydrateList<string>(2, 1);
			Assert.IsNotNull(names);
			foreach(string name in names)
				// each string should be null, since we gave a pk column index that is always a null value
				Assert.IsNull(name);
		}

		[Test]
		public void HydrateListOfObject_FromReader_1()
		{
			string sql = "SELECT ID, Name, [Description], OwnerID FROM Project";
			IDataReader reader = m_dal.CreateCommand(sql).ExecuteReader();
			IList<Project> projects = reader.HydrateList<Project>();
			Assert.IsNotNull(projects);
			Assert.Greater(projects.Count, 0);
			foreach(Project p in projects)
				Console.WriteLine(p.Name);
		}

		[Test]
		public void HydrateListOfObject_FromReader_2()
		{
			string sql = "SELECT 'asdf' as DummyColumn, ID, Name, [Description], OwnerID FROM Project";
			IDataReader reader = m_dal.CreateCommand(sql).ExecuteReader();
			IList<Project> projects = reader.HydrateList<Project>(1, 1);
			Assert.IsNotNull(projects);
			Assert.Greater(projects.Count, 0);
			foreach(Project p in projects)
				Console.WriteLine(p.Name);
		}

		[Test]
		public void HydrateListOfObject_FromReader_3()
		{
			string sql = "SELECT 'asdf' as DummyColumn, NULL as NullColumn, ID, Name, [Description], OwnerID FROM Project";
			IDataReader reader = m_dal.CreateCommand(sql).ExecuteReader();
			IList<Project> projects = reader.HydrateList<Project>(2, 1);
			Assert.IsNotNull(projects);
			Assert.Greater(projects.Count, 0);
			foreach (Project p in projects)
				Assert.IsNull(p);
		}

		[Test]
		public void HydrateSingleString_FromRow_1()
		{
			string sql = "SELECT LOWER(name) as name FROM Project";
			IDbCommand cmd = m_dal.CreateCommand(sql);
			IList<object[]> rows = cmd.ExecuteReader().GetRows();
			string name = rows[0].Hydrate<string>();
			Assert.IsNotNull(name);
			Console.WriteLine(name);
		}

		[Test]
		public void HydrateSingleString_FromRow_2()
		{
			string sql = "SELECT 'asdf' as DummyColumn, LOWER(name) as name FROM Project";
			IDbCommand cmd = m_dal.CreateCommand(sql);
			IList<object[]> rows = cmd.ExecuteReader().GetRows();
			string name = rows[0].Hydrate<string>(1, 1);
			Assert.IsNotNull(name);
			Console.WriteLine(name);
		}

		[Test]
		public void HydrateSingleString_FromRow_3()
		{
			string sql = "SELECT 'asdf' as DummyColumn, NULL as NullColumn, LOWER(name) as name FROM Project";
			IDbCommand cmd = m_dal.CreateCommand(sql);
			IList<object[]> rows = cmd.ExecuteReader().GetRows();
			// I specify column index 1 to be the PK column, which is NULL in the result set, so the hydrate method should see that and immediatly return null (default(T)).
			string name = rows[0].Hydrate<string>(2, 1);
			Assert.IsNull(name);
		}


		[Test]
		public void HydrateSingleObject_FromRow_1()
		{
			string sql = "SELECT ID, Name, [Description], OwnerID FROM Project";
			IDbCommand cmd = m_dal.CreateCommand(sql);
			IList<object[]> rows = cmd.ExecuteReader().GetRows();
			Project project = rows[0].Hydrate<Project>();
			Assert.IsNotNull(project);
			Console.WriteLine(project.Name);
		}

		[Test]
		public void HydrateSingleObject_FromRow_2()
		{
			string sql = @"SELECT 'asdf' AS DummyColumn, ID, Name, [Description], OwnerID FROM Project";
			IDbCommand cmd = m_dal.CreateCommand(sql);
			IList<object[]> rows = cmd.ExecuteReader().GetRows();
			Project project = rows[0].Hydrate<Project>(1, 1);
			Assert.IsNotNull(project);
			Console.WriteLine(project.Name);
		}

		[Test]
		public void HydrateSingleObject_FromRow_3()
		{
			string sql = @"SELECT 'asdf' AS DummyColumn, NULL as NullColumn, ID, Name, [Description], OwnerID FROM Project";
			IDbCommand cmd = m_dal.CreateCommand(sql);
			IList<object[]> rows = cmd.ExecuteReader().GetRows();
			Project project = rows[0].Hydrate<Project>(2, 1);
			Assert.IsNull(project);
		}

		[Test]
		public void HydrateListPerKey_1()
		{
			string sql = @"
SELECT
	p.ID,
	p.Name,
	p.[Description],
	p.OwnerID,
	c.ID,
	c.ProjectID,
	c.UserID,
	c.DateCreated,
	c.Comment
FROM
	Project p
	LEFT JOIN ProjectComment c
	ON p.ID = c.ProjectID
ORDER BY
	p.ID
";
			IList<object[]> rows = m_dal.CreateCommand(sql)
				.ExecuteReader()
				.GetRows();
			// First let's get the Project objects out
			IList<Project> projects = rows.HydrateUniqueList<Project>(0);
			int commentsOffset = TableMetadata.For<Project>().Columns.Count;
			// Now the comments for each project
			IDictionary<object, IList<ProjectComment>> projComments = rows.HydrateListPerKey<ProjectComment>(0, commentsOffset, commentsOffset);
			// Now wire the graph up
			foreach(Project proj in projects)
				proj.Comments = projComments[proj.ID];

			Assert.AreEqual("Noef", projects[0].Name.Trim());
			Assert.IsNotNull(projects[0].Comments);
			Assert.AreEqual(3, projects[0].Comments.Count);
			Assert.AreEqual("This is a test comment on Noef from Sam", projects[0].Comments[0].Comment.Trim());

			Assert.AreEqual("RestCake", projects[1].Name.Trim());
			Assert.IsNull(projects[1].Comments[0]);

			Assert.AreEqual("Project 3", projects[2].Name.Trim());
			Assert.IsNull(projects[2].Comments[0]);

			// Just an example of how to "flatten" the result of HydrateListPerKey into a single list.
			List<ProjectComment> list = projComments.SelectMany(kv => kv.Value).ToList();
			Assert.AreEqual(5, list.Count);

			// non-null tests
			IDictionary<object, IList<ProjectComment>> projComments2 = rows.HydrateNonNullListPerKey<ProjectComment>(0, commentsOffset, commentsOffset);
			Assert.AreEqual(0, projComments2[projects[1].ID].Count);
			Assert.AreEqual(0, projComments2[projects[2].ID].Count);
		}

	}
}
