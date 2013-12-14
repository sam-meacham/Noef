using System;
using System.Collections.Generic;
using System.Linq;

namespace Noef.CodeGen
{
	public class Relationship
	{
		public string Pk { get; set; }
		public string Fk { get; set; }

		// The results of parsing the "Def"
		public TableMapping PrincipalTable { get; set; }
		public string PrincipalKey { get; set; }
		public TableMapping DependentTable { get; set; }
		public string DependentKey { get; set; }

		/// <summary>
		/// What you want the property to be named that represents the principal object in the dependent class (child.parent).
		/// </summary>
		public string PrincipalPropertyName { get; set; }

		/// <summary>
		/// What you want the property to be named that represents the dependent object in the principal class (parent.child, or .children in the case of a list)
		/// </summary>
		public string DependentPropertyName { get; set; }

		public FkInfo FkInfo { get; set; }

		/// <summary>
		/// Whether or not the dependent property is a single object or a sequence
		/// </summary>
		public bool IsMany { get; set; }

		/// <summary>
		/// If IsMany is true (meaning the dependent property is a sequence), this determines what .NET type to use (List, IList, array, etc)
		/// </summary>
		public SequenceType SequenceType { get; set; }

		/// <summary>
		/// Constructor
		/// </summary>
		public Relationship(IList<TableMapping> mappings, string pk, string fk, string principalPropertyName, string dependentPropertyName)
		{
			Pk = pk;
			Fk = fk;
			PrincipalPropertyName = principalPropertyName;
			DependentPropertyName = dependentPropertyName;

			string[] pkParts = Pk.Trim().Split(new [] {'.'});
			string[] fkParts = Fk.Trim().Split(new [] {'.'});

			string pTableName = pkParts[0].Trim();
			PrincipalTable = mappings.Single(m => String.Equals(m.TableName, pTableName, StringComparison.OrdinalIgnoreCase));
			PrincipalTable.AsPrincipal.Add(this);
			PrincipalKey = pkParts[1].Trim();

			string dTableName = fkParts[0].Trim();
			DependentTable = mappings.SingleOrDefault(m => String.Equals(m.TableName, dTableName, StringComparison.OrdinalIgnoreCase));

			if (DependentTable == null)
				throw new Exception("No table named \"" + dTableName + "\" found in your mappings");

			DependentTable.AsDependent.Add(this);
			DependentKey = fkParts[1].Trim();

			SequenceType = SequenceType.IList;
		}


		public string GetDependentPropertyTypeName()
		{
			if (!IsMany)
				return DependentTable.ClassName;
			switch(SequenceType)
			{
				case SequenceType.List:  return "List<" + DependentTable.ClassName + ">";
				case SequenceType.IList: return "IList<" + DependentTable.ClassName + ">";
				case SequenceType.Array: return DependentTable.ClassName + "[]";
				default: throw new ArgumentOutOfRangeException();
			}
		}

		public string GetDependentPropertyDeclaration()
		{
			return "public " + GetDependentPropertyTypeName() + " " + DependentPropertyName + " {get; set; }";
		}

		public string GetPrincipalPropertyDeclaration()
		{
			return "public " + PrincipalTable.ClassName + " " + PrincipalPropertyName + " {get; set; }";
		}

		public void SetFk(IList<FkInfo> fks)
		{
			// Determine which FK they specified with their relationship definition
			FkInfo = fks.FirstOrDefault(fk =>
				(
					// format: PrincipalTable.PK = DependentTable.FK
					String.Equals(fk.PrincipalTable, PrincipalTable.TableName, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(fk.PrincipalColumn, PrincipalKey, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(fk.DependentTable, DependentTable.TableName, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(fk.DependentColumn, DependentKey, StringComparison.OrdinalIgnoreCase)
				)
				||
				(
					// format: DependentTable.FK = PrincipalTable.PK 
					String.Equals(fk.PrincipalTable, DependentTable.TableName, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(fk.PrincipalColumn, DependentKey, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(fk.DependentTable, PrincipalTable.TableName, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(fk.DependentColumn, PrincipalKey, StringComparison.OrdinalIgnoreCase)
				)
			);

			if (FkInfo != null)
				IsMany = !FkInfo.IsFkPk;
		}

	}
}
