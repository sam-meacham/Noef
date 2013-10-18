using NUnit.Framework;
using Noef.UnitTests.DataAccess;

namespace Noef.UnitTests
{
	/// <summary>
	/// These tests ONLY test methods in NoefDal, even though many of the methods in NoefDal in turn call methods in HydrateExtensions, the
	/// tests for HydrateExtensions (that call its methods directs) are found in in the HydrateTests.cs file.
	/// </summary>
	public class DalTests : NoefTestBase
	{
		[Test]
		public void Dal_SingleOrDefault()
		{
			// Project
			string[] sqls = {
				// Using SELECT *
				"SELECT * FROM Project WHERE ID = @id",
				// Using TableMetadata to get the columns list (and all overloads)
				"SELECT " + TableMetadata.GetColumnsString<Project>() + " FROM Project WHERE ID = @id",
				"SELECT " + TableMetadata.GetColumnsString<Project>("") + " FROM Project WHERE ID = @id",
				"SELECT " + TableMetadata.GetColumnsString<Project>("p", "p_") + " FROM Project p WHERE ID = @id",
				"SELECT " + TableMetadata.GetColumnsString<Project>("p", "", "p_") + " FROM Project p WHERE ID = @id",
				// The columns out of order (so the PK isn't first)
				"SELECT 1 AS _tmp1, 'asdf' AS _tmp2, ID, Name, Description, OwnerID FROM Project WHERE ID = @id"
			};

			for (int i = 0; i < sqls.Length; i++)
			{
				Project project;
				if (i == 5)
					project = m_dal.SingleOrDefault<Project>(sqls[i], new { id = 1 }, null, 2, 2);
				else
					project = m_dal.SingleOrDefault<Project>(sqls[i], new { id = 1 });
				Assert.IsNotNull(project);
				Assert.AreEqual("Noef", project.Name.Trim());
			}

			// User
			sqls = new[] {
				// Using SELECT *
				"SELECT * FROM [User] WHERE ID = @id",
				// Using TableMetadata to get the columns list (and all overloads)
				"SELECT " + TableMetadata.GetColumnsString<User>() + " FROM [User] WHERE ID = @id",
				"SELECT " + TableMetadata.GetColumnsString<User>("") + " FROM [User] WHERE ID = @id",
				"SELECT " + TableMetadata.GetColumnsString<User>("u", "u_") + " FROM [User] u WHERE ID = @id",
				"SELECT " + TableMetadata.GetColumnsString<User>("u", "", "u_") + " FROM [User] u WHERE ID = @id",
				// The columns out of order (so the PK isn't first)
				"SELECT 1 AS _tmp1, 'asdf' AS _tmp2, ID, Username, Email, DateCreated FROM [User] WHERE ID = @id"
			};

			for (int i = 0; i < sqls.Length; i++)
			{
				User user;
				if (i == 5)
					user = m_dal.SingleOrDefault<User>(sqls[i], new { id = 1 }, null, 2, 2);
				else
					user = m_dal.SingleOrDefault<User>(sqls[i], new { id = 1 });
				Assert.IsNotNull(user);
				Assert.AreEqual("sam", user.Username.Trim());
			}

		}

		[Test]
		public void Dal_Select_Single()
		{
			Project p = m_dal.Select<Project>("ID", 1);
			Assert.IsNotNull(p);
			Assert.AreEqual("Noef", p.Name.Trim());
		}

	}
}
