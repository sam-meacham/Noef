using System;
using System.Collections.Generic;


namespace Noef.UnitTests.DataAccess
{
	public partial class Project
	{
		// Columns
		public int ID { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public int OwnerID { get; set; }

		// Related properties
		// ProjectComment.ProjectID = Project.ID (FK_ProjectComment_Project)
		public IList<ProjectComment> Comments {get; set; }

		// Project.OwnerID = User.ID (FK_Project_User)
		public User User { get; set; }


	}
}


namespace Noef.UnitTests.DataAccess
{
	public partial class ProjectComment
	{
		// Columns
		public int ID { get; set; }
		public int ProjectID { get; set; }
		public int UserID { get; set; }
		public DateTime DateCreated { get; set; }
		public string Comment { get; set; }

		// Related properties
		// ProjectComment.ProjectID = Project.ID (FK_ProjectComment_Project)
		public Project Project { get; set; }

		// ProjectComment.UserID = User.ID (FK_ProjectComment_User)
		public User User { get; set; }


	}
}


namespace Noef.UnitTests.DataAccess
{
	public partial class User
	{
		// Columns
		public int ID { get; set; }
		public string Username { get; set; }
		public string Email { get; set; }
		public DateTime DateCreated { get; set; }

		// Related properties
		// Project.OwnerID = User.ID (FK_Project_User)
		public IList<Project> Project_s {get; set; }

		// ProjectComment.UserID = User.ID (FK_ProjectComment_User)
		public IList<ProjectComment> Comments {get; set; }


	}
}

