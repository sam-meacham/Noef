using System;
using System.Collections.Generic;

namespace Noef
{
	/// <summary>
	/// Sometimes you had adhoc queries that you don't want to create specific user/dto types to represent, because they are seldomly used.
	/// You can use the non-generic methods in NoefDal (such as Query()) and the results of your query will simply be dumped into a RawTable.
	/// You get a simple IList{object[]} to represent your rows and columns, and some metadata that tells you the field names and types.
	/// </summary>
	public class RawTable
	{
		public IList<object[]> Rows { get; set; }
		public string[] FieldNames { get; set; }
		public Type[] FieldTypes { get; set; }
	}
}
