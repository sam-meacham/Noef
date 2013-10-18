using System.Collections.Generic;
using Noef;
using System.ComponentModel;
using Noef.UnitTests.DataAccess;
using Noef.UnitTests.Hyper;

namespace Noef.UnitTests.DataAccess
{
	[MetadataClass]
	public static class Metadata
	{
		public static TableMetadata Project { get; private set; }
		public static TableMetadata ProjectComment { get; private set; }
		public static TableMetadata User { get; private set; }

		
		public static bool s_isInit = false;
		public static IList<TableMetadata> _AllTables { get; private set; }

		public static void Initialize()
		{
			if (s_isInit)
				return;

			// Set up HyperPropertyDescriptor for all of the dto classes
			// (We do this here since we don't want to pollute the dto types themselves with unnecessary attributes that create additional dependencies.
			// We want the DTOs to be POCOs in the truest sense).
			HyperTypeDescriptionProvider.Add(typeof(Project));
			HyperTypeDescriptionProvider.Add(typeof(ProjectComment));
			HyperTypeDescriptionProvider.Add(typeof(User));


			// Initialize the TableMetadata properties for each table/dto

			// Project => Project
			PropertyDescriptorCollection Project_props = TypeDescriptor.GetProperties(typeof(Project));
			Project = new TableMetadata(typeof(Project), new List<ColumnMetadata>()
			{
				new ColumnMetadata(Project_props["ID"]),
				new ColumnMetadata(Project_props["Name"]),
				new ColumnMetadata(Project_props["Description"]),
				new ColumnMetadata(Project_props["OwnerID"]),

			});

			// ProjectComment => ProjectComment
			PropertyDescriptorCollection ProjectComment_props = TypeDescriptor.GetProperties(typeof(ProjectComment));
			ProjectComment = new TableMetadata(typeof(ProjectComment), new List<ColumnMetadata>()
			{
				new ColumnMetadata(ProjectComment_props["ID"]),
				new ColumnMetadata(ProjectComment_props["ProjectID"]),
				new ColumnMetadata(ProjectComment_props["UserID"]),
				new ColumnMetadata(ProjectComment_props["DateCreated"]),
				new ColumnMetadata(ProjectComment_props["Comment"]),

			});

			// User => User
			PropertyDescriptorCollection User_props = TypeDescriptor.GetProperties(typeof(User));
			User = new TableMetadata(typeof(User), new List<ColumnMetadata>()
			{
				new ColumnMetadata(User_props["ID"]),
				new ColumnMetadata(User_props["Username"]),
				new ColumnMetadata(User_props["Email"]),
				new ColumnMetadata(User_props["DateCreated"]),

			});


			// The list of ALL tables
			_AllTables = new List<TableMetadata>()
			{
				Project,
				ProjectComment,
				User,

			};

			// Add all of these tables to Noef's table metadata
			TableMetadata.AddTables(_AllTables);
			s_isInit = true;
		}
	}
}


