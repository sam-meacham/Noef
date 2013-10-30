using System;
using System.Xml.Linq;

namespace Noef.CodeGen
{
	public static class ExtensionMethods
	{
		public static string ValueOrNull(this XElement xelement)
		{
			if (xelement == null)
				return null;
			if (String.IsNullOrEmpty(xelement.Value))
				return null;
			return xelement.Value;
		}
		
		// It's ok to call extension methods with null references, since it's just syntactic magic.
		public static string ValueOrNull(this XAttribute xattribute)
		{
			// So we check for null in here
			if (xattribute == null)
				return null;
			if (String.IsNullOrEmpty(xattribute.Value))
				return null;
			return xattribute.Value;
		}
	}
}
