using System.Collections.Generic;
using System;
using System.Threading;
using Noef;
using System.ComponentModel;
using Lam.Examples;

// ReSharper disable CheckNamespace
namespace Lam.Examples
// ReSharper restore CheckNamespace
{
	[MetadataClass]
	public static class Metadata
	{
		public static TableMetadata Parent { get; private set; }
		public static TableMetadata Thing { get; private set; }
		public static TableMetadata ChildThing { get; private set; }

		
		public static readonly object s_objSync = new object();
		public static bool s_isInit = false;
		public static IList<TableMetadata> _AllTables { get; private set; }

		public static void Initialize()
		{
			// dirty check (not thread safe)
			if (s_isInit)
				return;

			lock (s_objSync)
			{
				// thread safe check
				if (s_isInit)
					return;

				// Initialize the TableMetadata properties for each table/dto

				// Parent => Parent
				PropertyDescriptorCollection Parent_props = TypeDescriptor.GetProperties(typeof(Parent));
				Parent = new TableMetadata(typeof(Parent), "Parent", "NoefTest", "dbo", null, new List<ColumnMetadata>()
				{
					new ColumnMetadata("ID", Parent_props["ID"], false, true, true, false, 4, "int", "int"),
					new ColumnMetadata("Name", Parent_props["Name"], false, false, false, false, 20, "nchar", "nchar(20)"),

				});

				// Thing => Thing
				PropertyDescriptorCollection Thing_props = TypeDescriptor.GetProperties(typeof(Thing));
				Thing = new TableMetadata(typeof(Thing), "Thing", "NoefTest", "dbo", null, new List<ColumnMetadata>()
				{
					new ColumnMetadata("ID", Thing_props["ID"], false, true, true, false, 4, "int", "int"),
					new ColumnMetadata("Name", Thing_props["Name"], false, false, false, false, 20, "nchar", "nchar(20)"),
					new ColumnMetadata("ParentID", Thing_props["ParentID"], false, false, false, true, 4, "int", "int"),

				});

				// ChildThing => ChildThing
				PropertyDescriptorCollection ChildThing_props = TypeDescriptor.GetProperties(typeof(ChildThing));
				ChildThing = new TableMetadata(typeof(ChildThing), "ChildThing", "NoefTest", "dbo", null, new List<ColumnMetadata>()
				{
					new ColumnMetadata("ID", ChildThing_props["ID"], false, true, true, false, 4, "int", "int"),
					new ColumnMetadata("ParentID", ChildThing_props["ParentID"], false, false, false, true, 4, "int", "int"),
					new ColumnMetadata("Name", ChildThing_props["Name"], false, false, false, false, 20, "nchar", "nchar(20)"),

				});


				// The list of ALL tables
				_AllTables = new List<TableMetadata>()
				{
					Parent,
					Thing,
					ChildThing,

				};

				// Add all of these tables to Noef's table metadata
				TableMetadata.AddTables(_AllTables);

				// Create hydrators for all mapped types

				TableMetadata.Hydrators<Parent>.Hydrator = (row, startingColumn) =>
				{
					Parent obj = new Parent();
					obj.ID = (int) row[0 + startingColumn];
					obj.Name = (string) row[1 + startingColumn];

					return obj;
				};

				TableMetadata.Hydrators<Thing>.Hydrator = (row, startingColumn) =>
				{
					Thing obj = new Thing();
					obj.ID = (int) row[0 + startingColumn];
					obj.Name = (string) row[1 + startingColumn];
					obj.ParentID = (int) row[2 + startingColumn];

					return obj;
				};

				TableMetadata.Hydrators<ChildThing>.Hydrator = (row, startingColumn) =>
				{
					ChildThing obj = new ChildThing();
					obj.ID = (int) row[0 + startingColumn];
					obj.ParentID = (int) row[1 + startingColumn];
					obj.Name = (string) row[2 + startingColumn];

					return obj;
				};


				s_isInit = true;
			}
		}
	}
}


