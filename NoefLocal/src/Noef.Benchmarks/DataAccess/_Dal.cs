using System;
using Noef.Benchmarks.DataAccess.Noef;

namespace Noef.Benchmarks.DataAccess
{
	/// <summary>
	/// The dal implementation class (inherits from NoefDal).
	/// If you want to access the Dal singleton instance, use Dal.Instance.
	/// </summary>
	public partial class DalImpl : NoefDal
	{
		// Required overrides
		public override string ConnectionStringName
		{
			get { return "NoefTests"; }
		}

		public override DbType DbType
		{
			get { return DbType.SqlServer; }
		}

		// NOTE: This file was auto generated, and can be overwritten. The DalImpl class is partial. If you want to add anything to it, do it in a different file with a partial class.

		// You can optionally override GetConnectionString() or GetConnection() (which calls GetConnectionString(), which uses the ConnectionStringName property),
		// if you want custom behavior for getting the default connection string or connection.

		// These "GetByKey" methods correspond to each of the generated DTO types. You can leave them in this Dal class, or move them wherever you'd like
		// (a generic repository or query class, a partial class on the actual dto type, etc)




		// Add any additional DAL methods in a separate partial class file. You can make them static if you want, in which case you'd call them like: DalImpl.StaticMethodName().
		// If they are non-static, you call them like: Dal.Instance.MethodName() (preferred).
	}

	/// <summary>
	/// The singleton instance of DalImpl. Use Dal.Instance.
	/// Rarely should code be added to this class.  Add it to the DalImpl class instead, and access the singleton instance of DalImpl via Dal.Instance.
	/// Note that that covers ASP.NET scenarios (the singleton instance is "per request"). If you have some other application, you can just create a new instance of DalImpl
	/// and use it however you'd like.
	/// </summary>
	public class Dal : PerRequestSingleton<DalImpl>
	{
	}
}


