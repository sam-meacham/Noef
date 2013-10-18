using System;
using System.Collections.Generic;

namespace Noef
{
	public class PagedData<T>
	{
		public int PageSize { get; set; }
		public int PageIndex { get; set; }
		public int TotalPages { get; set; }
		public int TotalRecords { get; set; }
		public IEnumerable<T> Data;

		/// <summary>
		/// In case you want to pass something else along with your page
		/// </summary>
		public object Context { get; set; }

		public PagedData(IEnumerable<T> data, int pageSize, int pageIndex, int totalRecords)
		{
			Data = data;
			PageSize = pageSize;
			PageIndex = pageIndex;
			TotalRecords = totalRecords;
			TotalPages = (int) Math.Ceiling((double) TotalRecords/PageSize);
		}

		// With serialization, it's a good idea to have a parameterless constructor
		public PagedData()
		{ }
	}

	/// <summary>
	/// A non-generic version that uses a RawTable instead of IEnumerable{T} for storing the data.
	/// </summary>
	public class PagedData
	{
		public int PageSize { get; set; }
		public int PageIndex { get; set; }
		public int TotalPages { get; set; }
		public int TotalRecords { get; set; }
		public RawTable Table;

		/// <summary>
		/// In case you want to pass something else along with your page
		/// </summary>
		public object Context { get; set; }

		public PagedData(RawTable table, int pageSize, int pageIndex, int totalRecords)
		{
			Table = table;
			PageSize = pageSize;
			PageIndex = pageIndex;
			TotalRecords = totalRecords;
			TotalPages = (int) Math.Ceiling((double) TotalRecords/PageSize);
		}

		// With serialization, it's a good idea to have a parameterless constructor
		public PagedData()
		{ }
	}
}
