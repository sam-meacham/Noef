using System;
using System.Collections;
using System.Collections.Generic;
using Lam.Examples;
using NUnit.Framework;

namespace Noef.Examples.NestedOneToManyGraph
{
	[TestFixture]
    public class Tests
    {
		private MyDal m_dal;

		[SetUp]
		public void Setup()
		{
			m_dal = new MyDal();
		}

		[TearDown]
		public void TearDown()
		{
			m_dal.Dispose();
		}

		[Test]
		public void Test1()
		{
			string sql = @"SELECT
" + TableMetadata.GetColumnsString<Parent>("p", "") + @",
" + TableMetadata.GetColumnsString<Thing>("t", "") + @",
" + TableMetadata.GetColumnsString<ChildThing>("ch", "") + @"
FROM
	dbo.Parent p
	LEFT JOIN dbo.Thing t
	ON p.ID = t.ParentID

	LEFT JOIN dbo.ChildThing ch
	ON t.ID = ch.ParentID
ORDER BY p.ID, t.ID, ch.ID
";
	
			var rows = m_dal.GetRows(sql);

			// get some offsets...
			int thingOffset = TableMetadata.GetColumns<Parent>().Count;
			int childrenOffset = thingOffset + TableMetadata.GetColumns<Thing>().Count;

			IList<Parent> parents = rows.HydrateUniqueList<Parent>(0, 0);

			//var things = rows.HydrateNonNullListPerKey<Thing>(0, thingOffset, thingOffset);
			var things = rows.HydrateUniqueListPerKey<Thing>(0, thingOffset, thingOffset);
			var childThings = rows.HydrateNonNullListPerKey<ChildThing>(thingOffset, childrenOffset, childrenOffset);

			Console.WriteLine(parents.Count + " parent(s)\r\n");

			foreach (var parent in parents)
			{
				parent.Things = things[parent.ID];

				Console.WriteLine("Parent: " + parent.Name + " has " + parent.Things.Count + " thing(s)");

				foreach (var thing in parent.Things)
				{
					thing.Children = childThings[thing.ID];
					Console.WriteLine("\tThing: " + thing.Name + " (" + thing.Children.Count + " children)");
					foreach (var child in thing.Children)
					{
						Console.WriteLine("\t\tChild thing: " + child.Name);
					}
				}
				Console.WriteLine();
				Console.WriteLine();
			}

			//sql = "INSERT INTO Thing(Name,ParentID) values('1st thing', 1),('2nd thing', 1),('3rd thing', 1);INSERT INTO ChildThing(Name,ParentID) values('child 1', 1),('child 2', 2),('child 3',3)";
			//m_dal.ExecuteNonQuery(sql);
		}

    }
}
