using System.Collections.Generic;
using System.Linq;

namespace GameplayAbilities
{
	public static class ListExtensions
	{
		public static int AddUnique<T>(this List<T> list, T item)
		{
			int index = list.IndexOf(item);
			if (index != -1)
			{
				return index;
			}

			list.Add(item);
			return list.Count - 1;
		}

		public static void AppendUnique<T>(this List<T> destination, IEnumerable<T> source)
		{
			// 预分配空间以避免多次扩容
			int sourceCount = source.Count();
			if (destination.Capacity < destination.Count + sourceCount)
			{
				destination.Capacity = destination.Count + sourceCount;
			}

			var originalView = destination.AsReadOnly();

			foreach (var item in source)
			{
				if (!originalView.Contains(item))
				{
					destination.Add(item);
				}
			}
		}

		public static int RemoveSingle<T>(this List<T> list, T item)
		{
			var index = list.FindIndex(x => x.Equals(item));
			if (index != -1)
			{
				list.RemoveAt(index);
				return 1;
			}
			return 0;
		}

		public static void Reset<T>(this List<T> list, int new_size = 0)
		{
			if (new_size > list.Capacity)
			{
				list.Capacity = new_size;
			}
			list.Clear();
		}

		public static void Reserve<T>(this List<T> list, int new_size)
		{
			list.Capacity = new_size;
		}

		public static bool IsValidIndex<T>(this List<T> list, int index)
		{
			return index >= 0 && index < list.Count;
		}

		public static bool IsEmpty<T>(this List<T> list)
		{
			return list.Count == 0;
		}
	}
}