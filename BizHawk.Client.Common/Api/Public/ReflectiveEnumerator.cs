using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BizHawk.Client.Common.Api.Public
{
	//https://stackoverflow.com/a/6944605
	public static class ReflectiveEnumerator
	{
		public static IEnumerable<T> GetEnumerableOfType<T>(params object[] constructorArgs) where T : class
		{
			List<T> objects = new List<T>();
			foreach (Type type in
				Assembly.GetAssembly(typeof(T)).GetTypes()
				.Concat(Assembly.GetExecutingAssembly().GetTypes())
				.Concat(Assembly.GetCallingAssembly().GetTypes())
				.Concat(Assembly.GetEntryAssembly().GetTypes())
				.Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T)))
				.Distinct()
			)
			{
				objects.Add((T)Activator.CreateInstance(type, constructorArgs));
			}
			return objects;
		}
	}
}
