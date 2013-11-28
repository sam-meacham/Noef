using System;
using System.Collections.Generic;
using System.ComponentModel; // for the [Description] attribute on enum fields

// ReSharper disable PartialTypeWithSinglePart


namespace Lam.Examples
{
	public partial class Parent
	{
		// Columns
		public int ID { get; set; }
		public string Name { get; set; }

		// Related properties
		// (As principal)
		// (D) Thing.ParentID = (P) Parent.ID (FK_Thing_Parent)
		public IList<Thing> Things {get; set; }



	}
}


namespace Lam.Examples
{
	public partial class Thing
	{
		// Columns
		public int ID { get; set; }
		public string Name { get; set; }
		public int ParentID { get; set; }

		// Related properties
		// (As principal)
		// (D) ChildThing.ParentID = (P) Thing.ID (FK_ChildThing_Thing)
		public IList<ChildThing> Children {get; set; }


		// (As dependent)
		// (D) Thing.ParentID = (P) Parent.ID (FK_Thing_Parent)
		public Parent Parent {get; set; }


	}
}


namespace Lam.Examples
{
	public partial class ChildThing
	{
		// Columns
		public int ID { get; set; }
		public int ParentID { get; set; }
		public string Name { get; set; }

		// Related properties
		// (As dependent)
		// (D) ChildThing.ParentID = (P) Thing.ID (FK_ChildThing_Thing)
		public Thing Thing {get; set; }


	}
}


namespace Lam.Examples
{
	public static class EnumDescriptions
	{

	}
}


// ReSharper restore PartialTypeWithSinglePart

