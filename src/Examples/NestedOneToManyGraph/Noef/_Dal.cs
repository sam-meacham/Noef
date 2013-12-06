using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using Noef;
using Lam.Examples;


// ReSharper disable CheckNamespace
namespace Lam.Examples
// ReSharper restore CheckNamespace
{
	/// <summary>
	/// Generated DAL class (inherits from NoefDal).
	/// </summary>
	public partial class MyDal : NoefDal
	{
		// Required overrides
		public override string ConnectionStringName
		{
			get { return "g_NoefTest"; }
		}

		public override NoefDbType DbType
		{
			get { return NoefDbType.SqlServer; }
		}

		// NOTE: This file was auto generated, and can/will be overwritten.
		// The MyDal class is partial. If you want to augment it, do it in a different file (YourDal.cs, etc)

		// You can optionally override GetConnectionString() or GetConnection() (which calls GetConnectionString(), which uses the ConnectionStringName property),
		// if you want custom behavior for getting the default connection string or connection.
	}
}

