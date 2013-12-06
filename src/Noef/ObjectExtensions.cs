using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Noef
{
	public static class ObjectExtensions
	{
		/// <summary>
		/// Convert an anonymous object into a Dictionary{string, object}.
		/// Ex: new { id = 1, name = "Sam" } would be turned into a dictionary: { {"id", 1}, {"name", "Sam"} }
		/// </summary>
		public static IDictionary<string, object> ToDictionary(this object data)
		{
			IDictionary<string, object> dictionary = data as IDictionary<string, object>;
			if (dictionary != null)
				return dictionary;
			return data
				.GetType()
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(property => property.CanRead)
				.ToDictionary(property => property.Name, property => property.GetValue(data, null));
		}

		/// <summary>
		/// If val is null, returns DBNull.Value, otherwise it just returns the val.
		/// </summary>
		public static object NullOrValue(this object val)
		{
			return (val == DBNull.Value) ? null : val;
		}


		/// <summary>
		/// This is provided as a convenience, since some databases have blank values that semantically mean NULL.
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static string NullIfBlank(this string s)
		{
			return String.IsNullOrEmpty(s) ? null : s;
		}


		public static string GetCompleteStackTrace(this Exception ex)
		{
			if (ex == null)
				return null;

			StringBuilder sb = new StringBuilder();
			while (ex != null)
			{
				sb.AppendLine("Type: " + ex.GetType().Name);
				sb.AppendLine("Message: " + ex.Message);
				sb.AppendLine("Stack Trace:");
				sb.AppendLine(ex.StackTrace).AppendLine();
				ex = ex.InnerException;
				if (ex != null)
					sb.AppendLine("Inner exception:");
			}
			return sb.ToString();
		}

	}
}
