﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
﻿using Noef.Benchmarks.DataAccess;
﻿using Noef.Benchmarks.DataAccess.Noef;


namespace Noef.Benchmarks
{
	public class PerformanceTests
	{
		public class Test
		{
			public static Test Create(Action<int> iteration, string name)
			{
				return new Test { Iteration = iteration, Name = name };
			}

			public Action<int> Iteration { get; set; }
			public string Name { get; set; }
			public Stopwatch Watch { get; set; }
		}

		public class Tests : List<Test>
		{
			public void Add(Action<int> iteration, string name)
			{
				Add(Test.Create(iteration, name));
			}

			public void Run(int iterations)
			{
				// warmup 
				foreach (var test in this)
				{
					test.Iteration(iterations + 1);
					test.Watch = new Stopwatch();
					test.Watch.Reset();
				}

				var rand = new Random();
				for (int i = 1; i <= iterations; i++)
				{
					foreach (var test in this.OrderBy(ignore => rand.Next()))
					{
						test.Watch.Start();
						test.Iteration(i);
						test.Watch.Stop();
					}
				}

				foreach (var test in this.OrderBy(t => t.Watch.ElapsedMilliseconds))
				{
					Console.WriteLine(test.Name + " took " + test.Watch.ElapsedMilliseconds + "ms");
				}
			}
		}


		public void Run(int iterations)
		{
			var tests = new Tests();

			// HAND CODED 

			var connection = Program.GetOpenConnection();

			var postCommand = new SqlCommand();
			postCommand.Connection = connection;
			postCommand.CommandText = @"select Id, [Text], [CreationDate], LastChangeDate, 
                Counter1,Counter2,Counter3,Counter4,Counter5,Counter6,Counter7,Counter8,Counter9 from Posts where Id = @Id";
			var idParam = postCommand.Parameters.Add("@Id", System.Data.SqlDbType.Int);

			tests.Add(id =>
			{
				idParam.Value = id;

				using (var reader = postCommand.ExecuteReader())
				{
					reader.Read();
					var post = new Post();
					post.Id = reader.GetInt32(0);
					post.Text = reader.GetNullableString(1);
					post.CreationDate = reader.GetDateTime(2);
					post.LastChangeDate = reader.GetDateTime(3);

					post.Counter1 = reader.GetNullableValue<int>(4);
					post.Counter2 = reader.GetNullableValue<int>(5);
					post.Counter3 = reader.GetNullableValue<int>(6);
					post.Counter4 = reader.GetNullableValue<int>(7);
					post.Counter5 = reader.GetNullableValue<int>(8);
					post.Counter6 = reader.GetNullableValue<int>(9);
					post.Counter7 = reader.GetNullableValue<int>(10);
					post.Counter8 = reader.GetNullableValue<int>(11);
					post.Counter9 = reader.GetNullableValue<int>(12);
				}
			}, "hand coded");


			// Noef

			string sql = "SELECT " + TableMetadata.GetColumnsString<Post>() + " FROM dbo.Posts WHERE Id = @id";
			tests.Add(id =>
				{
					Post post = Dal.Instance.SingleOrDefault<Post>(sql, new { id });
				}, "Noef SingleOrDefault<T>");


			tests.Add(id =>
			{
				Post post = Dal.Instance.Select<Post>("Id", id);

			}, "Noef SelecT<T>");


			// Run the tests

			tests.Run(iterations);


		}

	}

	static class SqlDataReaderHelper
	{
		public static string GetNullableString(this SqlDataReader reader, int index)
		{
			object tmp = reader.GetValue(index);
			if (tmp != DBNull.Value)
			{
				return (string)tmp;
			}
			return null;
		}

		public static T? GetNullableValue<T>(this SqlDataReader reader, int index) where T : struct
		{
			object tmp = reader.GetValue(index);
			if (tmp != DBNull.Value)
			{
				return (T)tmp;
			}
			return null;
		}
	}

}
