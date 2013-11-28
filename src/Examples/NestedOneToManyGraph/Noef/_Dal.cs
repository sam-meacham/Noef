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
	/// The dal implementation class (inherits from NoefDal).
	/// If you want to access the MyDal singleton instance, use MyDal.Instance.
	/// </summary>
	public partial class MyDalImpl : NoefDal
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

		// NOTE: This file was auto generated, and can be overwritten. The MyDalImpl class is partial. If you want to add anything to it, do it in a different file with a partial class.

		// You can optionally override GetConnectionString() or GetConnection() (which calls GetConnectionString(), which uses the ConnectionStringName property),
		// if you want custom behavior for getting the default connection string or connection.

		// Add any additional DAL methods in a separate partial class file. You can make them static if you want, in which case you'd call them like: MyDalImpl.StaticMethodName().
		// If they are non-static, you call them like: MyDal.Instance.MethodName() (preferred).
	}


	/// <summary>
	/// The singleton instance of MyDalImpl. Use MyDal.Instance.
	/// Rarely should code be added to this class.  Add it to the MyDalImpl class instead, and access the singleton instance of MyDalImpl via MyDal.Instance.
	/// Note that that covers ASP.NET scenarios (the singleton instance is "per request"). If you have some other application, you can just create a new instance of MyDalImpl
	/// and use it however you'd like.
	/// </summary>
	public partial class MyDal : PerRequestSingleton<MyDalImpl>
	{
		private static string s_versionString;
		public static string VersionString
		{
			get
			{
				if (s_versionString != null)
					return s_versionString;
				Assembly asm = Assembly.GetExecutingAssembly();
				FileVersionInfo info = FileVersionInfo.GetVersionInfo(asm.Location);
				s_versionString = info.ProductVersion;
				return s_versionString;	
			}
		}
	}
}


