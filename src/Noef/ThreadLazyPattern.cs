using System;
using System.Threading;
using System.Web;

namespace Noef
{
	public static class ThreadLazyPattern
	{
#if !NET2 // Lazy<T> is .NET 4+
		/// <summary>
		/// setup() will be called right away, on a separate thread.
		/// A Lazy{T} object is created, and fnValue() will be called when that lazy is evaluated.
		/// But before fnValue() will be called, the separate thread that called setup() will be joined to the current (main) thread.
		/// This guarantees that setup() FINISHES before fnValue() is called.
		/// This is a convenience method for filling a specific kind of gap that occurs commonly enough that bundling it this way is nice.
		/// </summary>
		public static Lazy<T> NewLazy<T>(Action setup, Func<T> fnValue)
		{
			// create a separate thread, where setup() is called right away
			HttpContext mainThreadContext = HttpContext.Current;
			Thread t = new Thread(() =>
			{
				// Need access to the DAL.  Note Noef hasn't been thread-safety tested or evaluated yet.
				HttpContext.Current = mainThreadContext;
				setup();
			}) { Name = "ThreadLazyPattern-setup-" + typeof (T).Name };
			t.Start();

			// set up our wrapper value factory, that will join the "setup" thread in
			Func<T> fnValueWrapper = () =>
			                         {
				                         t.Join();
										 // call the actual fnValue function
				                         T value = fnValue();
				                         return value;
			                         };

			// set up the lazy object
			// TODO: there is another constructor that takes a bool, "isThreadSafe" as a 2nd arg. Need to look that up.
			Lazy<T> lazy = new Lazy<T>(fnValueWrapper);
			return lazy;
		}


		/// <summary>
		/// setup() will be called right away, on a separate thread.
		/// A Lazy{T} object is created, and fnValue() will be called when that lazy is evaluated.
		/// But before fnValue() will be called, the separate thread that called setup() will be joined to the current (main) thread.
		/// This guarantees that setup() FINISHES before fnValue() is called.
		/// This is a convenience method for filling a specific kind of gap that occurs commonly enough that bundling it this way is nice.
		/// </summary>
		public static Lazy<T> NewLazy<T>(Action setup, ref T obj)
		{
			// create a separate thread, where setup() is called right away
			HttpContext mainThreadContext = HttpContext.Current;
			Thread t = new Thread(() =>
			{
				// Need access to the DAL.  Note Noef hasn't been thread-safety tested or evaluated yet.
				HttpContext.Current = mainThreadContext;
				setup();
			}) { Name = "ThreadLazyPattern-setup-" + typeof (T).Name };
			t.Start();

			// TODO: Does this do what I want?
			T obj2 = obj;

			// set up our wrapper value factory, that will join the "setup" thread in
			Func<T> fnValueWrapper = () =>
			{
				// make sure setup() has FINISHED (it has the responsibility of assigning obj)
				t.Join();

				// call the actual fnValue function
				return obj2;
			};

			// set up the lazy object
			// TODO: there is another constructor that takes a bool, "isThreadSafe" as a 2nd arg. Need to look that up.
			Lazy<T> lazy = new Lazy<T>(fnValueWrapper);
			return lazy;
		}
#endif
	}
}
