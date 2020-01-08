using System.Collections.Generic;
using System;

namespace System.Linq
{
	public static class LinqExtra
	{
		public static IEnumerable<Tuple<T, int>> ZipWithIndex<T>(this IEnumerable<T> self)
		{
			int i = 0;
			foreach (T t in self)
			{
				yield return Tuple.Create(t, i++);
			}
		}

		public static IEnumerable<T> Interlace<T>(this IEnumerable<T> self, IEnumerable<T> other)
		{
			IEnumerator<T> it1 = self.GetEnumerator();
			IEnumerator<T> it2 = other.GetEnumerator();

			bool bMore1 = false;
			bool bMore2 = false;
			while ((bMore1 = it1.MoveNext()) && (bMore2 = it2.MoveNext()))
			{
				yield return it1.Current;
				yield return it2.Current;
			}

			if (bMore1)
			{
				do
				{
					yield return it1.Current;
				} while (bMore1 = it1.MoveNext());
			}
			it1.Dispose();

			if (bMore2)
			{
				do
				{
					yield return it2.Current;
				} while (bMore1 = it2.MoveNext());
			}
			it2.Dispose();
		}
	}
}

namespace System.Collections.Generic
{
	public static class CollectionExts
	{
		public static IEnumerable<T> AsOption<T>(this T x)
		{
			yield return x;
		}

		static public V GetOr<K, V>(this Dictionary<K, V> dict, K key, V def = default(V))
		{
			V rv;
			if (dict.TryGetValue(key, out rv))
			{
				return rv;
			}
			
			return def;
		}

		static public V GetOrSet<K, V>(this Dictionary<K, V> dict, K key, V def = default(V))
		{
			V rv;
			if (dict.TryGetValue(key, out rv))
			{
				return rv;
			}
			else
			{
				dict[key] = def;
			}
			
			return def;
		}

		public static void Swap<T>(this List<T> obj, int i, int j)
		{
			T tmp = obj[i];
			obj[i] = obj[j];
			obj[j] = tmp;
		}

		public static void Swap<T>(this T[] obj, int i, int j)
		{
			T tmp = obj[i];
			obj[i] = obj[j];
			obj[j] = tmp;
		}

		public static void MoveLast<T>(this List<T> list, int i)
		{
			T v = list[i];
			list.RemoveAt(i);
			list.Add(v);
		}

		public static void MoveFirst<T>(this List<T> list, int i)
		{
			T v = list[i];
			list.RemoveAt(i);
			list.Insert(0, v);
		}

		public static void MoveBack<T>(this List<T> list, int i)
		{
			list.Swap(i, i+1);
		}

		public static void MoveForward<T>(this List<T> list, int i)
		{
			list.Swap(i, i-1);
		}

		public static void PopBack<T>(this List<T> obj)
		{
			obj.RemoveAt(obj.Count - 1);
		}

		public static void PushBack<T>(this List<T> obj, T value)
		{
			obj.Add(value);
		}

		public static T Front<T>(this List<T> obj)
		{
			return obj[0];
		}

		public static void PopFront<T>(this List<T> obj)
		{
			obj.RemoveAt(0);
		}

		public static T Back<T>(this List<T> obj)
		{
			return obj[obj.Count - 1];
		}

		/// <summary>
		/// Add all entries in dictionary to list.
		/// </summary>
		/// <typeparam name="K"></typeparam>
		/// <typeparam name="V"></typeparam>
		/// <param name="dictionary"></param>
		/// <param name="outBuffer"></param>
		public static void CollectEntries<K, V>(this Dictionary<K, V> dictionary, List<KeyValuePair<K,V>> outBuffer)
		{
			for (var e = dictionary.GetEnumerator(); e.MoveNext();)
			{
				outBuffer.Add(e.Current);
			}
		}

		public static void RemoveAtViaEndSwap<T>(this List<T> obj, int idx)
		{
			int lastidx = obj.Count - 1;
			obj[idx] = obj[lastidx];
			obj.RemoveAt(lastidx);
		}

		public static Dictionary<K, V> GroupwiseAggregate<T, K, V>(this List<T> src,
			Func<T, K> getkey, Func<T, V, V> aggregator, V seed = default(V))
		{
			Dictionary<K, V> rv = new Dictionary<K, V>();

			T element;
			K key;
			V accum;
			for (int i = 0; i < src.Count; ++i)
			{
				element = src[i];
				key = getkey(element);
				if (!rv.TryGetValue(key, out accum))
				{
					accum = seed;
				}
				
				rv[key] = aggregator(element, accum);
			}
			return rv;
		}

		public static void SortBy<T, V>(this T[] array, Func<T, V> map) where V : IComparable<V>
		{
			System.Array.Sort(array, (x, y) => map(x).CompareTo(map(y)));
		}

		public static void SortBy<T, V, U>(this T[] array, Func<T, V> ord1, Func<T, U> ord2)
			where V : IComparable<V>
			where U : IComparable<U>
		{
			System.Array.Sort(array, (x, y) =>
			{
				int val = ord1(x).CompareTo(ord1(y));
				if (val != 0)
				{
					return val;
				}
				
				return ord2(x).CompareTo(ord2(y));
			});
		}

		public static void SortBy<T, V, U, W>(this T[] array, Func<T, V> ord1, Func<T, U> ord2, Func<T, W> ord3)
			where V : IComparable<V>
			where U : IComparable<U>
			where W : IComparable<W>
		{
			System.Array.Sort(array, (x, y) =>
			{
				int val = ord1(x).CompareTo(ord1(y));
				if (val != 0)
				{
					return val;
				}
				
				val = ord2(x).CompareTo(ord2(y));
				if (val != 0)
				{
					return val;
				}
				
				return ord3(x).CompareTo(ord3(y));
			});
		}

		public static void SortBy<T, V>(this List<T> list, Func<T, V> ord) where V : IComparable<V>
		{
			list.Sort((x, y) => ord(x).CompareTo(ord(y)));
		}

		public static void SortBy<T, V, U>(this List<T> list, Func<T, V> ord1, Func<T, U> ord2)
			where V : IComparable<V>
			where U : IComparable<U>
		{
			list.Sort((x, y) =>
			{
				int val = ord1(x).CompareTo(ord1(y));
				if (val != 0)
				{
					return val;
				}
				
				return ord2(x).CompareTo(ord2(y));
			});
		}

		public static void SortBy<T, V, U, W>(
				this List<T> list, Func<T, V> ord1, Func<T, U> ord2, Func<T, W> ord3)
			where V : IComparable<V>
			where U : IComparable<U>
			where W : IComparable<W>
		{
			list.Sort((x, y) =>
			{
				int val = ord1(x).CompareTo(ord1(y));
				if (val != 0)
				{
					return val;
				}
				
				val = ord2(x).CompareTo(ord2(y));
				if (val != 0)
				{
					return val;
				}
				
				return ord3(x).CompareTo(ord3(y));
			});
		}


		public static V Aggregate<T, V>(this List<T> src, Func<T, V, V> aggregateor, V seed = default(V))
		{
			V rv = seed;
			for (int i = 0; i < src.Count; ++i)
			{
				rv = aggregateor(src[i], rv);
			}
			
			return rv;
		}

		public static Dictionary<K, List<T>> GroupBy<T, K>(IEnumerable<T> src, Func<T, K> getgroup)
		{
			var rv = new Dictionary<K, List<T>>();
			K group;
			List<T> grouplist;
			foreach (T element in src)
			{
				group = getgroup(element);
				if (!rv.TryGetValue(group, out grouplist))
				{
					rv[group] = grouplist = new List<T>();
				}
				
				grouplist.Add(element);
			}
			return rv;
		}

		public static int SafeCount<T>(this IList<T> list)
		{
			if (list == null)
				return 0;

			return list.Count;
		}
	}
}
