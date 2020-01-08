using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Extension methods for a more efficient or GC friendly work with Unity / System collections.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Returns a readonly reference (or just a copy) of a single voxel in a linearized array of chunks of voxels.
		/// </summary>
		public static unsafe ref readonly Voxel Get(this NativeArray<Voxel> array, int chunkId, int voxelId)
		{
			return ref ((Voxel*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array))[chunkId * WorldGridInfo.kTotalVoxelsInChunk + voxelId];
		}

		/// <summary>
		/// Returns a writable reference (or just a copy) of a single voxel in a linearized array of chunks of voxels.
		/// </summary>
		public static unsafe ref Voxel GetWritable(this NativeArray<Voxel> array, int chunkId, int voxelId)
		{
			return ref ((Voxel*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array))[chunkId * WorldGridInfo.kTotalVoxelsInChunk + voxelId];
		}

		/// <summary>
		/// Add items in the helperList to set.
		/// Adding to a set while iterating through it would invalidate it.
		/// Therefore we can add items to a helperList and transfer them to the set after the iteration.
		/// </summary>
		public static void AddRange<T>(this ISet<T> set, IList<T> helperList)
		{
			for (int i = 0; i < helperList.Count; i++)
				set.Add(helperList[i]);
		}

		/// <summary>
		/// Remove items in the helperList from set.
		/// To be used as a GC friendly version of RemoveWhere or after iterating through the set to avoid invalidating it.
		/// </summary>
		public static void RemoveRange<T>(this ISet<T> set, IList<T> helperList)
		{
			for (int i = 0; i < helperList.Count; i++)
				set.Remove(helperList[i]);
		}

		public static void AddRange<T>(this ISet<T> set, in NativeList<T> helperList) where T : struct
		{
			for (int i = 0; i < helperList.Length; i++)
				set.Add(helperList[i]);
		}

		public static void RemoveRange<T>(this ISet<T> set, in NativeList<T> helperList) where T : struct
		{
			for (int i = 0; i < helperList.Length; i++)
				set.Remove(helperList[i]);
		}

		/// <summary>
		/// Remove items in the helperList from dictionary.
		/// To be used after iterating through the dictionary to avoid invalidating it.
		/// </summary>
		public static void RemoveRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, in NativeList<TKey> helperList) where TKey : struct
		{
			for (int i = 0; i < helperList.Length; i++)
				dictionary.Remove(helperList[i]);
		}
	}
}
