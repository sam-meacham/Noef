using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Noef
{
	public static class ReflectionHelper
	{
		public static bool HasAttribute<T>(this Type type) where T : Attribute
		{
			return type.GetCustomAttributes(typeof(T), inherit: true).Length > 0;
		}

		public static string GetPropertyName<T>(Expression<Func<T, object>> exp)
		{
			MemberExpression body = exp.Body as MemberExpression;
			if (body == null)
			{
				UnaryExpression ubody = (UnaryExpression)exp.Body;
				body = ubody.Operand as MemberExpression;
			}
			if (body == null)
				throw new Exception("Could not parse expression");
			return body.Member.Name;
		}

		public static string[] GetPropertyNames<T>(Expression<Func<T, object[]>> exp)
		{
			List<String> propNames = new List<string>();
			NewArrayExpression arrayBody = exp.Body as NewArrayExpression;
			if (arrayBody == null)
				throw new Exception("Needs to be a new object[] {} expression");
			foreach(var e in arrayBody.Expressions)
			{
				MemberExpression body = e as MemberExpression;
				if (body == null)
				{
					UnaryExpression ubody = (UnaryExpression)e;
					body = ubody.Operand as MemberExpression;
				}
				if (body == null)
					throw new Exception("Could not parse expression in GetPropertyNames() (one of the expressions in the NewArrayExpression body)");
				propNames.Add(body.Member.Name);
			}
			return propNames.ToArray();
		}

		public static IEnumerable<Type> GetTypesWithAttribute(Assembly assembly, Type attributeType)
		{
			return assembly.GetTypes().Where(type => type.GetCustomAttributes(attributeType, true).Length > 0);
		}

		public static IEnumerable<MethodInfo> GetMethodsWithAttribute(Type classType, Type attributeType)
		{
			return classType.GetMethods().Where(methodInfo => methodInfo.GetCustomAttributes(attributeType, true).Length > 0);
		}

		public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute(Type classType, Type attributeType)
		{
			return classType.GetProperties().Where(propertyInfo => propertyInfo.GetCustomAttributes(attributeType, true).Length > 0);
		}


		public static T GetAttribute<T>(MethodInfo method)
			where T : Attribute
		{
			T[] attribs = (T[])method.GetCustomAttributes(typeof(T), true);
			return attribs.SingleOrDefault();
		}


		public static T GetAttribute<T>(Type @class)
			where T : Attribute
		{
			T[] attribs = (T[])@class.GetCustomAttributes(typeof(T), true);
			return attribs.SingleOrDefault();
		}


		public static T GetAttribute<T>(MemberInfo member)
			where T : Attribute
		{
			T[] attribs = (T[])member.GetCustomAttributes(typeof(T), true);
			return attribs.SingleOrDefault();
		}

		public static bool IsNullable(Type type)
		{
			bool isNullable = type.IsGenericType && type.FullName != null && type.FullName.StartsWith("System.Nullable");
			return isNullable;
		}

		public static Type GetActualType(Type nullableType)
		{
			return Nullable.GetUnderlyingType(nullableType);
		}

		public static void SetValue(PropertyDescriptor prop, object obj, object value)
		{
			Type underlyingType = null;
			bool isNullable = IsNullable(prop.PropertyType);
			if (isNullable)
				underlyingType = GetActualType(prop.PropertyType);

			if (value == null)
				prop.SetValue(obj, null);
			else if (prop.PropertyType.IsEnum)
				prop.SetValue(obj, Enum.ToObject(prop.PropertyType, value));
			else if (isNullable && underlyingType.IsEnum)
				prop.SetValue(obj, Enum.ToObject(underlyingType, value));
			else if (prop.PropertyType.IsInstanceOfType(value) || (isNullable && underlyingType.IsInstanceOfType(value)))
				prop.SetValue(obj, value);
			else if (isNullable)
				prop.SetValue(obj, Convert.ChangeType(value, underlyingType));
			else
				prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
		}

		public static void ApplyValues(object obj, IEnumerable<KeyValuePair<string, object>> values, bool throwOnBadProp = true)
		{
			Type type = obj.GetType();
			TableMetadata tmeta = TableMetadata.For(type);
			foreach(KeyValuePair<string, object> pair in values)
			{
				try
				{
					PropertyDescriptor prop = tmeta.Properties[pair.Key];
					if (prop == null && throwOnBadProp)
						throw new Exception("Invalid property name " + pair.Key + " for type + " + type.Name);
					if (prop == null)
						// Bad property, but the user specified to not throw an exception, so just skip this one
						continue;
					SetValue(prop, obj, pair.Value);
				}
				catch (Exception)
				{
					if (throwOnBadProp)
						throw;
				}
			}
		}

	}
}
