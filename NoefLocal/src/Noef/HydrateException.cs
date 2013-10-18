using System;

namespace Noef
{
	[Serializable]
	public class HydrateException : Exception
	{
		public HydrateException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		public HydrateException(string message) : base(message)
		{
		}
	}
}
