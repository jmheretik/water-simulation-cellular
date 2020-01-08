using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// A background job where the newly settled fluid voxels are assigned to components, new components are created and the ones marked for removal are removed.
	/// </summary>
	public struct FluidComponentsMaintenanceJob : IJob
	{
		public Color DebugColor;
		public float RealtimeSinceStartup;

		private NativeList<VectorI3> _helperIndicesList;

		private GCHandle _worldApiPointer;
		private GCHandle _managerPointer;

		public void CreateData(FluidComponentManager manager, WorldApi worldApi)
		{
			_helperIndicesList = new NativeList<VectorI3>(Unity.Collections.Allocator.Persistent);

			_worldApiPointer = GCHandle.Alloc(worldApi, GCHandleType.Pinned);
			_managerPointer = GCHandle.Alloc(manager, GCHandleType.Pinned);
		}

		public void Execute()
		{
			WorldApi worldApi = (WorldApi)_worldApiPointer.Target;
			FluidComponentManager manager = (FluidComponentManager)_managerPointer.Target;

			TryCreateComponents(worldApi, manager);
			TryRemoveComponents(worldApi, manager.Components, manager.RebuildEnabled);
		}

		public void TryCreateComponents(WorldApi worldApi, FluidComponentManager manager)
		{
			UnityEngine.Profiling.Profiler.BeginSample("TryCreateComponents");

			// first try to add voxel to a nearby existing component because creating a new one is expensive
			int processed = 0;

			foreach (VectorI3 indices in manager.VoxelsToProcess)
			{
				if (TryAddToExistingComponent(worldApi, manager, in indices))
					_helperIndicesList.Add(indices);

				if (processed++ > FluidComponentManager.kMaxVoxelsProcessedPerIteration)
					break;
			}

			manager.VoxelsToProcess.RemoveRange(_helperIndicesList);
			_helperIndicesList.Clear();

			// no nearby existing component found
			if (manager.VoxelsToProcess.Count > FluidComponentManager.kMinComponentSize)
			{
				// try to create a single new component
				foreach (VectorI3 indices in manager.VoxelsToProcess)
				{
					if (TryCreateNewComponent(worldApi, manager, in indices))
						manager.VoxelsToProcess.Remove(indices);

					break;
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		public void TryRemoveComponents(WorldApi worldApi, List<FluidComponent> components, bool rebuildEnabled)
		{
			UnityEngine.Profiling.Profiler.BeginSample("TryRemoveComponents");

			for (int i = 0; i < components.Count; i++)
			{
				FluidComponent component = components[i];

				component.UpdateLifetime(RealtimeSinceStartup);

				// cleanup components marked for rebuild
				if (rebuildEnabled && component.ToRebuild)
				{
					component.Unsettle(component.Count * component.Viscosity);
					component.Cleanup(worldApi);
				}

				// remove small and old components
				if (component.ToRemove)
				{
					component.Cleanup(worldApi);
					component.UpdateJob.ToRemove = true;
					components.RemoveAtViaEndSwap(i--);
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		#region add

		/// <summary>
		/// Try to create a new component out of given voxel indices.
		/// </summary>
		private bool TryCreateNewComponent(WorldApi worldApi, FluidComponentManager manager, in VectorI3 indices)
		{
			VectorI3 tmpIndices, currIndices = indices;

			// scan downwards for an existing component
			do
			{
				// dont create a new component if there is an existing one below
				if (manager.GetComponent(in currIndices) != null)
					return false;

				ref readonly Voxel bottomNeighbour = ref worldApi.GetVoxel(in currIndices);

				// reached terrain
				if (bottomNeighbour.Solid == Voxel.kMaxVolume && bottomNeighbour.Fluid == 0)
					break;

				tmpIndices = currIndices;

			} while (worldApi.TryGetNeighbour(in tmpIndices, Neighbour.Bottom, out currIndices).Valid);

			// create new component
			worldApi.GetVoxelWorldPos(in indices, out Vector3 worldPos);
			Vector2 row = new Vector2(worldPos.x, worldPos.y);
			FluidSegment segment = new FluidSegment(worldPos.z);

			manager.Components.Add(new FluidComponent(worldApi.GetVoxel(in indices).Viscosity, RealtimeSinceStartup, in segment, in row, in DebugColor));

			CheckComponentIntersection(worldApi, manager.Components, manager.Components.Count - 1, in segment, in row);

			return true;
		}

		/// <summary>
		/// Try to add given voxel to an existing component nearby.
		/// </summary>
		private bool TryAddToExistingComponent(WorldApi worldApi, FluidComponentManager manager, in VectorI3 indices)
		{
			ref readonly Voxel voxel = ref worldApi.GetVoxel(in indices);

			// not eligible voxel
			if (!voxel.Settled || !voxel.HasFluid || manager.GetComponent(in indices) != null)
				return true;

			worldApi.GetVoxelWorldPos(in indices, out Vector3 worldPos);
			Vector2 row = new Vector2(worldPos.x, worldPos.y);
			FluidSegment segment = new FluidSegment(worldPos.z);
			Bounds voxelBounds = segment.GetBounds(in row);

			if (TryAssignToNearbyComponent(manager.Components, true, voxel.Viscosity, in voxelBounds, in segment, in row, out int componentId) ||
				TryAssignToNearbyComponent(manager.Components, false, voxel.Viscosity, in voxelBounds, in segment, in row, out componentId))
			{
				FluidComponent component = manager.Components[componentId];

				component.Count++;
				component.Bounds.Encapsulate(voxelBounds);
				component.Unsettle(voxel.Viscosity);

				CheckComponentIntersection(worldApi, manager.Components, componentId, in segment, in row);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Try to check all existing components if they're touching the given segment.
		/// </summary>
		/// <param name="sameRow">If true, check if they touch the segment directly. Otherwise check if they touch the segment from neighbouring rows.</param>
		private bool TryAssignToNearbyComponent(List<FluidComponent> components, bool sameRow, byte voxelViscosity, in Bounds voxelBounds, in FluidSegment segment, in Vector2 row, out int id)
		{
			for (id = 0; id < components.Count; id++)
			{
				FluidComponent component = components[id];

				// skip components far away
				if (component.Viscosity != voxelViscosity || !component.Bounds.Intersects(voxelBounds))
					continue;

				if (sameRow && Touches(component, in segment, in row, out var segments, out int segmentId))
				{
					FluidSegment touchingSegment = segments[segmentId];
					touchingSegment.Encapsulate(segment);
					segments[segmentId] = touchingSegment;

					// fix possible intersections
					CheckSegmentIntersections(segments);

					return true;
				}
				else if (!sameRow && TouchesNeighbour(component, in segment, in row))
				{
					// add new segment
					if (component.AllSegments.TryGetValue(row, out segments))
						segments.Add(segment);
					else
						component.AllSegments.Add(row, new List<FluidSegment>() { segment });

					return true;
				}
			}

			return false;
		}

		#endregion

		#region intersections

		/// <summary>
		/// Determines if the given segment touches directly some segment in the same row of the given component.
		/// Returns the list containing the touched segment and its index if it does.
		/// </summary>
		private bool Touches(FluidComponent component, in FluidSegment segment, in Vector2 row, out List<FluidSegment> segments, out int id)
		{
			if (component.AllSegments.TryGetValue(row, out segments))
			{
				for (id = 0; id < segments.Count; id++)
				{
					if (segments[id].Intersects(segment))
						return true;
				}
			}

			id = default;
			return false;
		}

		/// <summary>
		/// Determines if the given segment touches some segment in the neighbouring rows of the given component.
		/// </summary>
		private bool TouchesNeighbour(FluidComponent component, in FluidSegment segment, in Vector2 row)
		{
			Vector2 nRow = row;

			for (nRow.x = row.x - WorldGridInfo.kVoxelSize; nRow.x <= row.x + WorldGridInfo.kVoxelSize; nRow.x += WorldGridInfo.kVoxelSize)
			{
				for (nRow.y = row.y - WorldGridInfo.kVoxelSize; nRow.y <= row.y + WorldGridInfo.kVoxelSize; nRow.y += WorldGridInfo.kVoxelSize)
				{
					if (nRow != row && Touches(component, in segment, in nRow, out _, out _))
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Checks if the given component intersects with some other and merges the two.
		/// </summary>
		public void CheckComponentIntersection(WorldApi worldApi, List<FluidComponent> components, int currentComponentId, in FluidSegment segment, in Vector2 row)
		{
			FluidComponent current = components[currentComponentId];

			for (int otherId = 0; otherId < components.Count; otherId++)
			{
				FluidComponent other = components[otherId];

				// skip components far away
				if (current == other || current.Viscosity != other.Viscosity || !current.Bounds.Intersects(other.Bounds))
					continue;

				// check segment intersections
				if (Touches(other, in segment, in row, out _, out _) || TouchesNeighbour(other, in segment, in row))
				{
					// the component being rebuilt eats the other
					// if both or none of them is being rebuilt then the bigger one eats smaller
					bool decideBySize = (current.Rebuilding && other.Rebuilding) || (!current.Rebuilding && !other.Rebuilding);

					if ((!decideBySize && current.Rebuilding) || (decideBySize && current.Count >= other.Count))
					{
						TransferSegments(worldApi, other, current);
						other.UpdateJob.ToRemove = true;
						components.RemoveAtViaEndSwap(otherId);
					}
					else if ((!decideBySize && other.Rebuilding) || (decideBySize && current.Count < other.Count))
					{
						TransferSegments(worldApi, current, other);
						current.UpdateJob.ToRemove = true;
						components.RemoveAtViaEndSwap(currentComponentId);
					}

					return;
				}
			}
		}

		/// <summary>
		/// Checks the given row of segments for intersections and merges them.
		/// </summary>
		private void CheckSegmentIntersections(List<FluidSegment> segments)
		{
			for (int i = 0; i < segments.Count; i++)
			{
				FluidSegment segment = segments[i];

				// encapsulate all following segments if they touch
				for (int j = i + 1; j < segments.Count; j++)
				{
					FluidSegment otherSegment = segments[j];

					if (segment.Intersects(otherSegment))
					{
						segment.Encapsulate(otherSegment);
						segments.RemoveAtViaEndSwap(j--);
					}
				}

				segments[i] = segment;
			}
		}

		/// <summary>
		/// Transfers all the segments from the given component to the other given component.
		/// </summary>
		private void TransferSegments(WorldApi worldApi, FluidComponent from, FluidComponent to)
		{
			// dont transfer if 'from' is above 'to'
			if (from.WaterLevel >= to.WaterLevel)
			{
				// also unsettle 'from' if neither is being rebuilt
				// this is to avoid assignment of the cleaned up voxels to 'to' in the next iteration and equalizing too fast
				int unsettleValue = (!from.Rebuilding && !to.Rebuilding) ? from.Count * from.Viscosity : 0;
				from.Cleanup(worldApi, unsettleValue);
				return;
			}

			to.WaterLevel = from.WaterLevel;
			to.Bounds.Encapsulate(from.Bounds);

			foreach (var fromSegments in from.AllSegments)
			{
				if (to.AllSegments.TryGetValue(fromSegments.Key, out var toSegments))
				{
					foreach (var segment in toSegments)
						to.Count -= segment.Count;

					toSegments.AddRange(fromSegments.Value);

					// fix possible intersections
					CheckSegmentIntersections(toSegments);

					foreach (var segment in toSegments)
						to.Count += segment.Count;
				}
				else
				{
					to.AllSegments.Add(fromSegments.Key, fromSegments.Value);

					foreach (var segment in fromSegments.Value)
						to.Count += segment.Count;
				}
			}
		}

		#endregion

		public void DestroyData()
		{
			_helperIndicesList.Dispose();

			_worldApiPointer.Free();
			_managerPointer.Free();
		}
	}
}