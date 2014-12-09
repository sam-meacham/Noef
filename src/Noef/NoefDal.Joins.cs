using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
#if SQL_CE
using System.Data.SqlServerCe;
#endif

namespace Noef
{
	public abstract partial class NoefDal
	{
		public IList<TPrimary> InnerJoin<TPrimary, TJoined>(string where, string orderBy, object sqlParams,
				Expression<Func<TPrimary, object>> joinOnPrimary,
				Expression<Func<TJoined, object>> joinOn,
				Expression<Func<TPrimary, object>> propToHydrate,
				IDbConnection cn = null,
				IDbTransaction tx = null,
				int timeout = -1)
			where TPrimary : class
			where TJoined : class
		{
			return join("INNER", where, orderBy, sqlParams, joinOnPrimary, joinOn, propToHydrate, cn, tx, timeout);
		}

		public IList<TPrimary> LeftJoin<TPrimary, TJoined>(string where, string orderBy, object sqlParams,
				Expression<Func<TPrimary, object>> joinOnPrimary,
				Expression<Func<TJoined, object>> joinOn,
				Expression<Func<TPrimary, object>> propToHydrate,
				IDbConnection cn = null,
				IDbTransaction tx = null,
				int timeout = -1)
			where TPrimary : class
			where TJoined : class
		{
			return join("LEFT", where, orderBy, sqlParams, joinOnPrimary, joinOn, propToHydrate, cn, tx, timeout);
		}


		/// <summary>
		/// NOTE: This is for a one-to-many currently. Might want to change the name to reflect that, if I ever do one that is one-to-one.
		/// TODO: Currently only supports a single primary key (Pks[0])
		/// </summary>
		private IList<TPrimary> join<TPrimary, TJoined>(string joinType, string where, string orderBy, object sqlParams,
				Expression<Func<TPrimary, object>> joinOnPrimary,
				Expression<Func<TJoined, object>> joinOn,
				Expression<Func<TPrimary, object>> propToHydrate,
				IDbConnection cn = null,
				IDbTransaction tx = null,
				int timeout = -1)
			where TPrimary : class
			where TJoined : class
		{
			TableMetadata t1 = TableMetadata.For<TPrimary>();
			TableMetadata t2 = TableMetadata.For<TJoined>();

			string col1 = joinOnPrimary == null ? t1.Pks[0].Name : ReflectionHelper.GetPropertyName(joinOnPrimary);
			string col2 = ReflectionHelper.GetPropertyName(joinOn);
			string propName = ReflectionHelper.GetPropertyName(propToHydrate);

			// We know the column names we're joining on between the 2 tables, now get refs to the appropriate ColumnMetadata objects
			//ColumnMetadata colMeta1 = t1.Columns.Single(c => String.Equals(c.Name, col1, StringComparison.OrdinalIgnoreCase));
			//ColumnMetadata colMeta2 = t2.Columns.Single(c => String.Equals(c.Name, col2, StringComparison.OrdinalIgnoreCase));

			// Default values for where and orderBy if none were provided
			if (String.IsNullOrEmpty(where))
				where = "1 = 1";
			if (String.IsNullOrEmpty(orderBy))
				orderBy = t1.Name + "." + t1.Pks[0].Name + " DESC, " + t2.Name + "." + t2.Pks[0].Name + " DESC";

			// Set up the query
			string sql = String.Format(
@"SELECT
	{0},
	{1}
FROM
	{2} {2}
	{3} JOIN {4} {4}
	ON {2}.{5} = {4}.{6}
WHERE
	{7}
ORDER BY
	{8}",
		TableMetadata.GetColumnsString<TPrimary>(t1.Name, ""),
		TableMetadata.GetColumnsString<TJoined>(t2.Name, ""),
		t1.Name, joinType, t2.Name, col1, col2, where, orderBy);

			// Get the raw rows
			IList<object[]> rows = GetRows(sql, sqlParams, cn, CommandType.Text, tx, timeout);
			// If there were no results, return an empty list
			if (rows.Count == 0)
				return new List<TPrimary>();

			// Get a unique list of the "primary" objects (the left table in the join)
			IList<TPrimary> primaries = rows.HydrateUniqueList<TPrimary>(0, t1.Columns.IndexOf(t1.Pks[0]));

			// Is the property we're hydrating in the primary class a list or a single property?
			bool isList = typeof(IEnumerable).IsAssignableFrom(t1.Properties[propName].PropertyType);
			int joinedOffset = t1.Columns.Count;
			if (isList)
			{
				// (one-to-many)
				// Get a "list of lists" for the related objects (each list corresponds to 1 of the 
				int splitIndex = t1.Columns.IndexOf(t1.Pks[0]);
				IDictionary<object, IList<TJoined>> joinedLists = rows.HydrateNonNullListPerKey<TJoined>(splitIndex, joinedOffset, joinedOffset + t2.Columns.IndexOf(t2.Pks[0]));
				foreach(TPrimary primary in primaries)
				{
					object pk = t1.Pks[0].Property.GetValue(primary);
					if (pk == null)
						throw new Exception("Null PK value");
					IList<TJoined> list = joinedLists[pk];
					ReflectionHelper.SetValue(t1.Properties[propName], primary, list);
				}
			}
			else
			{
				// (one-to-one)
				// We're hydrating a single property in the primary class
				IList<TJoined> joined = rows.HydrateList<TJoined>(joinedOffset, joinedOffset + t2.Columns.IndexOf(t2.Pks[0]));
				for (int i = 0; i < primaries.Count; i++)
					ReflectionHelper.SetValue(t1.Properties[propName], primaries[i], joined[i]);
			}

			return primaries;
		}

	}
}
