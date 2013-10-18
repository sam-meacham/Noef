using System.Collections.Generic;
using Noef.Benchmarks.DataAccess.Noef;
using System.ComponentModel;
using Noef.Benchmarks.DataAccess.Noef.Hyper;
using Noef.Benchmarks.DataAccess;

namespace Noef.Benchmarks.DataAccess
{
	[MetadataClass]
	public static class Metadata
	{
		public static TableMetadata Posts { get; private set; }

		
		public static bool s_isInit = false;
		public static IList<TableMetadata> _AllTables { get; private set; }

		public static void Initialize()
		{
			if (s_isInit)
				return;

			// Set up HyperPropertyDescriptor for all of the dto classes
			// (We do this here since we don't want to pollute the dto types themselves with unnecessary attributes that create additional dependencies.
			// We want the DTOs to be POCOs in the truest sense).
			HyperTypeDescriptionProvider.Add(typeof(Post));


			// Initialize the TableMetadata properties for each table/dto

			// Posts => Posts
			PropertyDescriptorCollection Posts_props = TypeDescriptor.GetProperties(typeof(Post));
			Posts = new TableMetadata(typeof(Post), "Posts", new List<ColumnMetadata>()
			{
				new ColumnMetadata(Posts_props["Id"]),
				new ColumnMetadata(Posts_props["Text"]),
				new ColumnMetadata(Posts_props["CreationDate"]),
				new ColumnMetadata(Posts_props["LastChangeDate"]),
				new ColumnMetadata(Posts_props["Counter1"]),
				new ColumnMetadata(Posts_props["Counter2"]),
				new ColumnMetadata(Posts_props["Counter3"]),
				new ColumnMetadata(Posts_props["Counter4"]),
				new ColumnMetadata(Posts_props["Counter5"]),
				new ColumnMetadata(Posts_props["Counter6"]),
				new ColumnMetadata(Posts_props["Counter7"]),
				new ColumnMetadata(Posts_props["Counter8"]),
				new ColumnMetadata(Posts_props["Counter9"]),

			});


			// The list of ALL tables
			_AllTables = new List<TableMetadata>()
			{
				Posts,

			};

			// Add all of these tables to Noef's table metadata
			TableMetadata.AddTables(_AllTables);
			s_isInit = true;
		}
	}
}


