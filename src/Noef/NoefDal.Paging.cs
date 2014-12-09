using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public abstract partial class NoefDal
	{
		// ********************************************************************************
		// *** Page queries ***************************************************************
		// *** NOTE: These are taken and modified from PetaPoco, adapted to work in Noef **
		// *** http://www.toptensoftware.com/petapoco/ ************************************
		// ********************************************************************************

// ReSharper disable StaticFieldInGenericType
		private static readonly Regex rxColumns = new Regex(@"\A\s*SELECT\s+((?:\((?>\((?<depth>)|\)(?<-depth>)|.?)*(?(depth)(?!))\)|.)*?)(?<!,\s+)\bFROM\b", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
		private static readonly Regex rxOrderBy = new Regex(@"\bORDER\s+BY\s+(?:\((?>\((?<depth>)|\)(?<-depth>)|.?)*(?(depth)(?!))\)|[\w\(\)\.])+(?:\s+(?:ASC|DESC))?(?:\s*,\s*(?:\((?>\((?<depth>)|\)(?<-depth>)|.?)*(?(depth)(?!))\)|[\w\(\)\.])+(?:\s+(?:ASC|DESC))?)*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
		private static readonly Regex rxDistinct = new Regex(@"\ADISTINCT\s", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
// ReSharper restore StaticFieldInGenericType

		private static bool splitSqlForPaging(string originalQuery, out string sqlCount, out string columnsList, out string sqlOrderBy)
		{
			columnsList = null;
			sqlCount = null;
			sqlOrderBy = null;

			// Extract the columns from "SELECT <whatever> FROM"
			Match m = rxColumns.Match(originalQuery);
			if (!m.Success)
				return false;

			// Save column list and replace with COUNT(*)
			Group g = m.Groups[1];
			columnsList = originalQuery.Substring(g.Index);

			if (rxDistinct.IsMatch(columnsList))
				sqlCount = originalQuery.Substring(0, g.Index) + "COUNT(" + m.Groups[1].ToString().Trim() + ") " + originalQuery.Substring(g.Index + g.Length);
			else
				sqlCount = originalQuery.Substring(0, g.Index) + "COUNT(*) " + originalQuery.Substring(g.Index + g.Length);


			// Look for an "ORDER BY <whatever>" clause
			m = rxOrderBy.Match(sqlCount);
			if (m.Success)
			{
				g = m.Groups[0];
				sqlOrderBy = g.ToString();
				sqlCount = sqlCount.Substring(0, g.Index) + sqlCount.Substring(g.Index + g.Length);
			}

			return true;
		}

		private static void buildPageQueries(int skip, int take, string originalSql, out string sqlCount, out string sqlPage) 
		{
			// Split the SQL into the bits we need
			string columnsList, sqlOrderBy;
			if (!splitSqlForPaging(originalSql, out sqlCount, out columnsList, out sqlOrderBy))
				throw new Exception("Unable to parse SQL statement for paged query");

			// Build the SQL for the actual final result
			columnsList = rxOrderBy.Replace(columnsList, "");
			if (rxDistinct.IsMatch(columnsList))
			{
				columnsList = "__noef_inner.* FROM (SELECT " + columnsList + ") __noef_inner";
			}
			sqlPage = string.Format("SELECT * FROM (SELECT ROW_NUMBER() OVER ({0}) __noef_rownum, {1}) peta_paged WHERE __noef_rownum > " + skip + " AND __noef_rownum <= " + (skip + take),
										sqlOrderBy ?? "ORDER BY (SELECT NULL)", columnsList);
		}

		// Fetch a page
		public PagedData<T> Page<T>(int pageIndex, int pageSize, string sql, object sqlParams, IDbConnection cn = null, IDbTransaction tx = null, Expression<Func<T, object[]>> propsToHydrate = null, Action<T, object[]> hydrateRowCallback = null, int timeout = -1)
		{
			string sqlCount, sqlPage;
			buildPageQueries(pageIndex * pageSize, pageSize, sql, out sqlCount, out sqlPage);

			// Get the records and the total count (we do startingColum=1 to skip the row number column)
			IList<T> data = Query(sqlPage, sqlParams, 1, propsToHydrate: propsToHydrate, hydrateRowCallback: hydrateRowCallback, cn: cn, tx: tx, timeout: timeout);
			int totalRecords = (int)ExecuteScalar(sqlCount, sqlParams);

			// Create the PagedData object and return it
			PagedData<T> result = new PagedData<T>(data, pageSize, pageIndex, totalRecords);
			return result;
		}


		// Fetch a page (non-generic version)
		public PagedData Page(int pageIndex, int pageSize, string sql, object sqlParams, IDbConnection cn = null, IDbTransaction tx = null, int timeout = -1)
		{
			string sqlCount, sqlPage;
			buildPageQueries(pageIndex * pageSize, pageSize, sql, out sqlCount, out sqlPage);

			// Get the records and the total count (we do startingColum=1 to skip the row number column)
			RawTable rawTable = Query(sqlPage, sqlParams, 1, cn, tx: tx, timeout: timeout);
			int totalRecords = (int)ExecuteScalar(sqlCount, sqlParams);

			// Create the PagedData object and return it
			PagedData result = new PagedData(rawTable, pageSize, pageIndex, totalRecords);
			return result;
		}


	}
}
