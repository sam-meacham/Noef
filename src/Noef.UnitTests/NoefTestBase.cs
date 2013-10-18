using NUnit.Framework;
using Noef.UnitTests.DataAccess;

namespace Noef.UnitTests
{
	[TestFixture]
	public abstract class NoefTestBase
	{
		protected Dal m_dal = new Dal();

		[SetUp]
		public void Setup()
		{
			m_dal.ResetDatabase();
		}

		[TearDown]
		public void TearDown()
		{
			
		}



	}
}
