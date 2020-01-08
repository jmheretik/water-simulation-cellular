using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Connected component of settled fluid voxels - represents a single body of water.
	/// </summary>
	public class FluidComponent
	{
		/// <summary>
		/// Settled voxels which make up this component.
		/// Stored as a list of FluidSegments for each unique row (pair x,y in world coordinates).
		/// </summary>
		public Dictionary<Vector2, List<FluidSegment>> AllSegments;

		/// <summary>
		/// Outlets are the voxels just above or on the water surface.
		/// Stored as a set of unique indices VectorI3(blockId, chunkId, voxelId).
		/// </summary>
		public HashSet<VectorI3> Outlets;

		public FluidComponentUpdateJob UpdateJob;

		/// <summary>
		/// Approximate AABB of this component.
		/// </summary>
		public Bounds Bounds;

		/// <summary>
		/// Number of voxels this component consists of.
		/// </summary>
		public int Count;

		/// <summary>
		/// Current water level of this component. Its the Y coord of the lowest outlet (or the Y coord of the highest valid voxel if there are no outlets).
		/// </summary>
		public float WaterLevel;
		public float Lifetime;
		public byte Viscosity;
		public bool ToRebuild;
		public bool Rebuilding;
		public bool Settled;

		public Color DebugColor;

		public bool ToUpdate
		{
			get
			{
				return !Settled && Lifetime > FluidComponentManager.kMinComponentLifetime && Count >= FluidComponentManager.kMinComponentSize;
			}
		}

		public bool ToRemove
		{
			get
			{
				return Lifetime > FluidComponentManager.kMinComponentLifetime && ((!Rebuilding && Count < FluidComponentManager.kMinComponentSize) || AllSegments.Count == 0);
			}
		}

		private float _creationTime;
		private ushort _settleCounter;

		/// <summary>
		/// Initialize fluid component from the given segment.
		/// </summary>
		public FluidComponent(byte viscosity, float realtimeSinceStartup, in FluidSegment segment, in Vector2 row, in Color debugColor)
		{
			Viscosity = viscosity;

			DebugColor = debugColor;

			// TODO use pool of objects, also for outlets
			AllSegments = new Dictionary<Vector2, List<FluidSegment>>();

			Lifetime = 0;
			_creationTime = realtimeSinceStartup;

			Initialize(in segment, in row);
		}

		/// <summary>
		/// Reinitialize component to the initial values.
		/// </summary>
		private void Initialize(in FluidSegment segment, in Vector2 row)
		{
			Count = 1;
			WaterLevel = row.y;

			AllSegments.Add(row, new List<FluidSegment>() { segment });
			Bounds = segment.GetBounds(in row);

			if (ToRebuild)
			{
				ToRebuild = false;
				Rebuilding = true;
			}
		}

		/// <summary>
		/// Unsettle component so it gets updated in the next iteration.
		/// </summary>
		public void Unsettle(int diff)
		{
			_settleCounter = (ushort)Mathf.Min(_settleCounter + Mathf.Abs(diff), ushort.MaxValue);
			Settled = false;
		}

		/// <summary>
		/// Decreases the settle counter by component's viscosity in order for it to reach a settled state.
		/// </summary>
		public void DecreaseSettle()
		{
			if (_settleCounter == 0)
			{
				Settled = true;
				ToRebuild = false;
				Rebuilding = false;
				UpdateBounds();
			}
			else
			{
				int value = Viscosity != 0 ? Viscosity : byte.MaxValue;

				_settleCounter = (ushort)Mathf.Max(_settleCounter - value, 0);
			}
		}

		/// <summary>
		/// Component will get rebuilt in following updates.
		/// This needs to be done if there is a suspicion the component might have got split or connected to some other.
		/// </summary>
		public void MarkForRebuild()
		{
			ToRebuild = true;
			Unsettle(Viscosity);
		}

		/// <summary>
		/// Unsettles all voxels and prepares the component for removal unless its flagged for rebuild.
		/// </summary>
		public void Cleanup(WorldApi worldApi, int unsettleValue = 0)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Cleanup");

			bool startVoxelSet = false;
			Vector3 startWorldPos = default;

			if (ToRebuild)
				unsettleValue = 0;

			foreach (var segments in AllSegments)
			{
				Vector2 row = segments.Key;

				for (int i = 0; i < segments.Value.Count; i++)
				{
					FluidSegment segment = segments.Value[i];

					foreach (VectorI3 indices in segment.GetIndices(in row, worldApi))
					{
						ref Voxel writeVoxel = ref worldApi.GetVoxelWritable(in indices);

						// preserve one voxel if rebuilding so that we can start from it and dont have to allocate new containers
						if (ToRebuild && !startVoxelSet && UpdateJob.IsValidVoxel(worldApi, in writeVoxel, in indices))
						{
							worldApi.GetVoxelWorldPos(in indices, out startWorldPos);
							startVoxelSet = true;
						}
						else
						{
							writeVoxel.Unsettle(unsettleValue);
							worldApi.UnsettleChunk(in indices);
						}
					}
				}

				segments.Value.Clear();
			}

			AllSegments.Clear();
			Outlets?.Clear();

			// reinitialize the component if it was marked for rebuild
			if (ToRebuild && startVoxelSet)
			{
				Vector2 row = new Vector2(startWorldPos.x, startWorldPos.y);
				FluidSegment segment = new FluidSegment(startWorldPos.z);
				Initialize(in segment, in row);
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		public void UpdateLifetime(float realtimeSinceStartup)
		{
			Lifetime = realtimeSinceStartup - _creationTime;
		}

		/// <summary>
		/// Traverses all segments and updates the approx bounds of this component.
		/// </summary>
		private bool UpdateBounds()
		{
			UnityEngine.Profiling.Profiler.BeginSample("UpdateBounds");

			bool centerSet = false;

			foreach (var segments in AllSegments)
			{
				Vector2 row = segments.Key;

				for (int i = 0; i < segments.Value.Count; i++)
				{
					Bounds segmentBounds = segments.Value[i].GetBounds(in row);

					if (!centerSet)
					{
						centerSet = true;
						Bounds = segmentBounds;
					}
					else
					{
						Bounds.Encapsulate(segmentBounds);
					}
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();

			return false;
		}
	}
}