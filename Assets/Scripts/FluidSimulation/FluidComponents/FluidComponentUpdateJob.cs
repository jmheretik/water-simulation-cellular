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
	/// A background job where the actual fluid component update takes place.
	/// </summary>
	public struct FluidComponentUpdateJob : IJob
	{
		public bool IsRunning;
		public bool ToRemove;

		private NativeList<VectorI3> _helperIndicesList;
		private NativeList<Vector2> _helperDictKeysList;

		private GCHandle _componentPointer;
		private GCHandle _worldApiPointer;
		private GCHandle _managerPointer;

		/// <summary>
		/// How much fluid was added to the outlets below the average level and can be therefore subtracted from the outlets above the average level.
		/// </summary>
		private int _fluidBalance;
		private float _diffOutletLevel;
		private float _avgOutletLevel;

		public void CreateData(WorldApi worldApi, FluidComponentManager manager, FluidComponent component)
		{
			IsRunning = true;
			ToRemove = false;

			_helperIndicesList = new NativeList<VectorI3>(Unity.Collections.Allocator.Persistent);
			_helperDictKeysList = new NativeList<Vector2>(Unity.Collections.Allocator.Persistent);

			_componentPointer = GCHandle.Alloc(component, GCHandleType.Pinned);
			_worldApiPointer = GCHandle.Alloc(worldApi, GCHandleType.Pinned);
			_managerPointer = GCHandle.Alloc(manager, GCHandleType.Pinned);
		}

		// TODO speedup
		/// <summary>
		/// Validates the existing segments and updates / equalizes the outlets of this component.
		/// </summary>
		public void Execute()
		{
			UnityEngine.Profiling.Profiler.BeginSample("Update");

			FluidComponent component = (FluidComponent)_componentPointer.Target;
			WorldApi worldApi = (WorldApi)_worldApiPointer.Target;
			FluidComponentManager manager = (FluidComponentManager)_managerPointer.Target;

			int diff = component.Count;

			if (component.Viscosity > FluidComponentManager.kMaxViscosityNotEqualize && component.Outlets == null)
				component.Outlets = new HashSet<VectorI3>();

			UpdateSegments(worldApi, component.AllSegments, component.Outlets, ref component.Count, ref component.WaterLevel);

			diff -= component.Count;

			if (diff != 0)
				component.Unsettle(diff * component.Viscosity);
			else
				component.DecreaseSettle();

			if (component.Viscosity > FluidComponentManager.kMaxViscosityNotEqualize && component.Outlets.Count > 0)
			{
				UpdateOutlets(worldApi, component.Outlets, component.Rebuilding, component.Lifetime, ref component.WaterLevel);

				EqualizeOutlets(true, worldApi, component.Outlets, component.Viscosity);
				EqualizeOutlets(false, worldApi, component.Outlets, component.Viscosity);
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		#region segments

		/// <summary>
		/// Traverses the 2 top rows of component and validates them.
		/// Removes/shortens segments containing invalid voxels and adds eligible voxels to outlets.
		/// </summary>
		private void UpdateSegments(WorldApi worldApi, Dictionary<Vector2, List<FluidSegment>> allSegments, HashSet<VectorI3> outlets, ref int componentCount, ref float componentWaterLevel)
		{
			UnityEngine.Profiling.Profiler.BeginSample("UpdateSegments");

			foreach (var segments in allSegments)
			{
				Vector2 row = segments.Key;

				// skip deep y segments
				if (row.y < componentWaterLevel - 2 * WorldGridInfo.kVoxelSize)
					continue;

				bool allValid = ValidateSegmentRow(worldApi, in row, segments.Value, outlets, ref componentCount);

				// grow water level to the highest valid segment row if there are no outlets
				if (componentWaterLevel < row.y && allValid && outlets?.Count == 0)
					componentWaterLevel = row.y;

				if (segments.Value.Count == 0)
					_helperDictKeysList.Add(row);
			}

			allSegments.RemoveRange(in _helperDictKeysList);
			_helperDictKeysList.Clear();

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Traverses each segment in the row voxel by voxel and adds outlets from valid ones or removes the invalid ones.
		/// </summary>
		private bool ValidateSegmentRow(WorldApi worldApi, in Vector2 row, List<FluidSegment> segments, HashSet<VectorI3> outlets, ref int componentCount)
		{
			bool allValid = true;

			for (int i = 0; i < segments.Count; i++)
			{
				bool shortened = false;
				bool firstVoxel = true;

				FluidSegment segment = segments[i];

				foreach (VectorI3 indices in segment.GetIndices(in row, worldApi))
				{
					ref readonly Voxel voxel = ref worldApi.GetVoxel(in indices);

					if (IsValidVoxel(worldApi, in voxel, in indices) && !shortened)
					{
						TryAddOutlets(worldApi, in voxel, in indices, outlets);
					}
					else
					{
						// unsettle invalid voxel so when it settles later it gets processed anew
						worldApi.GetVoxelWritable(in indices).Unsettle(0);

						if (!shortened)
						{
							shortened = true;

							componentCount -= segment.Count;

							if (firstVoxel)
							{
								segments.RemoveAtViaEndSwap(i--);
							}
							else
							{
								segments[i] = ShortenSegment(worldApi, in segment, in indices);
								componentCount += segments[i].Count;
							}
						}
					}

					firstVoxel = false;
				}

				if (shortened)
					allValid = false;
			}

			return allValid;
		}

		/// <summary>
		/// Shortens the given segment till currIndices and returns the new version.
		/// </summary>
		private FluidSegment ShortenSegment(WorldApi worldApi, in FluidSegment segment, in VectorI3 currIndices)
		{
			FluidSegment newSegment = new FluidSegment(segment.ZMin);
			worldApi.GetVoxelWorldPos(in currIndices, out Vector3 currWorldPos);
			newSegment.Encapsulate(currWorldPos.z - WorldGridInfo.kVoxelSize);

			return newSegment;
		}

		/// <summary>
		/// Settled voxel with fluid having settled and full bottomNeighbour.
		/// </summary>
		public bool IsValidVoxel(WorldApi worldApi, in Voxel voxel, in VectorI3 indices)
		{
			ref readonly Voxel bottomNeighbour = ref worldApi.TryGetNeighbour(in indices, Neighbour.Bottom, out _);

			return voxel.Settled && voxel.HasFluid && bottomNeighbour.Settled && bottomNeighbour.IsFull;
		}

		#endregion

		#region outlets

		/// <summary>
		/// Add this voxel OR its topNeighbour if its not yet full of fluid.
		/// </summary>
		private void TryAddOutlets(WorldApi worldApi, in Voxel voxel, in VectorI3 indices, HashSet<VectorI3> outlets)
		{
			if (!voxel.IsFull)
				outlets?.Add(indices);
			else
			{
				ref readonly Voxel topNeighbour = ref worldApi.TryGetNeighbour(in indices, Neighbour.Top, out VectorI3 nIndices);

				if (!topNeighbour.IsFull && voxel.HasCompatibleViscosity(in topNeighbour))
					outlets?.Add(nIndices);
			}
		}

		/// <summary>
		/// Traverses all outlets, removes invalid ones and updates the water levels of this component.
		/// </summary>
		private void UpdateOutlets(WorldApi worldApi, HashSet<VectorI3> outlets, bool componentRebuilding, float componentLifetime, ref float componentWaterLevel)
		{
			UnityEngine.Profiling.Profiler.BeginSample("UpdateOutlets");

			float sum = 0;
			float newWaterLevel = float.MaxValue;
			float minOutletLevel = float.MaxValue;
			float maxOutletLevel = float.MinValue;

			foreach (VectorI3 indices in outlets)
			{
				ref readonly Voxel outlet = ref worldApi.GetVoxel(in indices);

				if (IsInvalidOutlet(worldApi, in outlet, in indices))
				{
					_helperIndicesList.Add(indices);
				}
				else
				{
					float posY = worldApi.GetVoxelWorldPosY(in indices);
					float outletLevel = GetOutletWaterLevel(posY, in outlet);

					sum += outletLevel;

					if (newWaterLevel > posY)
						newWaterLevel = posY;

					if (minOutletLevel > outletLevel)
						minOutletLevel = outletLevel;

					if (maxOutletLevel < outletLevel)
						maxOutletLevel = outletLevel;
				}
			}

			outlets.RemoveRange(in _helperIndicesList);
			_helperIndicesList.Clear();

			if (outlets.Count == 0)
				return;

			_avgOutletLevel = sum / outlets.Count;
			_diffOutletLevel = maxOutletLevel - minOutletLevel;

			componentWaterLevel = newWaterLevel;

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Equalize the fluid in outlets.
		/// As a result, the voxels close to a water surface of some fluid component distribute their fluid across the whole water surface.
		/// Prevents the "stairs" effect characteristical in cellular automatas and mimics "pressure" in the fluid.
		/// </summary>
		private void EqualizeOutlets(bool give, WorldApi worldApi, HashSet<VectorI3> outlets, byte componentViscosity)
		{
			if (_diffOutletLevel < Voxel.kMaxVolume * 0.5f)
				return;

			UnityEngine.Profiling.Profiler.BeginSample("EqualizeOutlets");

			/// if give == true, fluid is added to outlets below the average level and the total amount transferred is saved in _fluidBalance
			/// if give == false, fluid is removed from the outlets above the average level until _fluidBalance is depleted (to approximately preserve the total volume exchanged)
			foreach (VectorI3 indices in outlets)
			{
				ref Voxel writeOutlet = ref worldApi.GetVoxelWritable(in indices);

				float posY = worldApi.GetVoxelWorldPosY(in indices);
				float outletLevel = GetOutletWaterLevel(posY, in writeOutlet);

				// compute new fluid value
				byte newFluid = (byte)Mathf.Clamp(_avgOutletLevel - (outletLevel - writeOutlet.Fluid), 0, Voxel.kMaxVolume);

				int diff = newFluid - writeOutlet.Fluid;

				if ((give && diff >= 0) || (!give && diff <= 0 && _fluidBalance > 0))
				{
					_fluidBalance += diff;

					ModifyOutlet(ref writeOutlet, worldApi, in indices, newFluid, diff, componentViscosity);
				}
			}

			outlets.AddRange(in _helperIndicesList);
			_helperIndicesList.Clear();

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Writes the new fluid value and viscosity to the outlet.
		/// </summary>
		private void ModifyOutlet(ref Voxel writeOutlet, WorldApi worldApi, in VectorI3 indices, byte newFluid, int diff, byte componentViscosity)
		{
			writeOutlet.Fluid = newFluid;

			// just emptied - add its bottomNeighbour to outlets
			if (!writeOutlet.HasFluid)
			{
				ref readonly Voxel bottomNeighbour = ref worldApi.TryGetNeighbour(in indices, Neighbour.Bottom, out VectorI3 nIndices);

				if (bottomNeighbour.HasFluid && writeOutlet.HasCompatibleViscosity(in bottomNeighbour))
					_helperIndicesList.Add(nIndices);
			}

			writeOutlet.Viscosity = (byte)(writeOutlet.HasFluid ? componentViscosity : 0);

			writeOutlet.Unsettle(diff);
			worldApi.UnsettleChunk(in indices);
		}

		private float GetOutletWaterLevel(float posY, in Voxel outlet)
		{
			return posY * Voxel.kMaxVolume + outlet.CurrentVolume;
		}

		/// <summary>
		/// Full voxel with topNeighbour full of fluid OR empty voxel with bottomNeighbour not full of fluid.
		/// </summary>
		private bool IsInvalidOutlet(WorldApi worldApi, in Voxel outlet, in VectorI3 indices)
		{
			ref readonly Voxel topNeighbour = ref worldApi.TryGetNeighbour(in indices, Neighbour.Top, out _);
			ref readonly Voxel bottomNeighbour = ref worldApi.TryGetNeighbour(in indices, Neighbour.Bottom, out _);

			return (outlet.IsFull && (topNeighbour.HasFluid || topNeighbour.IsFull)) ||
				   (!outlet.HasFluid && (!bottomNeighbour.HasFluid || !bottomNeighbour.IsFull));
		}

		#endregion

		public void DestroyData()
		{
			if (IsRunning)
			{
				IsRunning = false;

				_helperIndicesList.Dispose();
				_helperDictKeysList.Dispose();

				_componentPointer.Free();
				_worldApiPointer.Free();
				_managerPointer.Free();
			}
		}
	}
}