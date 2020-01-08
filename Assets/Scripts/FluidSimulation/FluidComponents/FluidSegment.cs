using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// An uninterrupted 1D segment of settled fluid voxels.
	/// </summary>
	public struct FluidSegment
	{
		public float ZMin;
		public float ZMax;

		public FluidSegment(float z)
		{
			ZMin = ZMax = z;
		}

		/// <summary>
		/// Traverse the segment in the given row from its z-min to its z-max voxel.
		/// </summary>
		public FluidSegmentEnumerator GetIndices(in Vector2 row, WorldApi worldApi)
		{
			return new FluidSegmentEnumerator(in this, in row, worldApi);
		}

		/// <summary>
		/// Returns an AABB of this segment.
		/// </summary>
		public Bounds GetBounds(in Vector2 row)
		{
			Vector3 segmentCenter = new Vector3(row.x, row.y, (ZMin + ZMax) * 0.5f);
			Vector3 segmentSize = WorldGridInfoHelper.GetVoxelSizeV3;

			segmentCenter += 0.5f * segmentSize;
			segmentSize.z *= Count;
			return new Bounds(segmentCenter, segmentSize);
		}

		/// <summary>
		/// How many voxels are contained in this segment.
		/// </summary>
		public int Count
		{
			get
			{
				return (int)((WorldGridInfo.kVoxelSize + ZMax - ZMin) * WorldGridInfo.kOneOverVoxelSize);
			}
		}

		/// <summary>
		/// Are these two segments intersecting or at most 1 voxel distance from each other.
		/// </summary>
		public bool Intersects(FluidSegment other)
		{
			return ZMax + WorldGridInfo.kVoxelSize >= other.ZMin && other.ZMax + WorldGridInfo.kVoxelSize >= ZMin;
		}

		public bool Contains(float z)
		{
			return ZMin <= z && z <= ZMax;
		}

		public void Encapsulate(float z)
		{
			if (ZMin > z)
				ZMin = z;

			if (ZMax < z)
				ZMax = z;
		}

		public void Encapsulate(FluidSegment other)
		{
			if (ZMin > other.ZMin)
				ZMin = other.ZMin;

			if (ZMax < other.ZMax)
				ZMax = other.ZMax;
		}

		public override string ToString()
		{
			return $"{ZMin} -> {ZMax}";
		}
	}

	/// <summary>
	/// Allows traversal of a segment voxel by voxel.
	/// </summary>
	public struct FluidSegmentEnumerator
	{
		private VectorI3 _current;
		private readonly VectorI3 _max;

		private VectorI3 _tmp;
		private readonly WorldApi _worldApi;

		public FluidSegmentEnumerator(in FluidSegment segment, in Vector2 row, WorldApi worldApi)
		{
			Vector3 zMinPos = new Vector3(row.x, row.y, segment.ZMin);
			Vector3 zMaxPos = new Vector3(row.x, row.y, segment.ZMax);
			worldApi.TryGetVoxel(in zMinPos, out _current);
			worldApi.TryGetVoxel(in zMaxPos, out _max);

			_tmp = VectorI3.negativeOne;	// so that the first MoveNext() returns true
			_worldApi = worldApi;
		}

		public FluidSegmentEnumerator GetEnumerator()
		{
			return this;
		}

		public VectorI3 Current
		{
			get
			{
				_tmp = _current;
				return _current;
			}
		}

		public bool MoveNext()
		{
			return _tmp.Equals(VectorI3.negativeOne) || (!_current.Equals(_max) && _worldApi.TryGetNeighbour(in _tmp, Neighbour.Forward, out _current).Valid);
		}
	}
}