using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Maintains and manages connected components of fluid in the world.
	/// </summary>
	public class FluidComponentManager : IDisposable
	{
		#region constants

		/// <summary>
		/// How long (seconds) should a component exist before we can start updating or removing it.
		/// </summary>
		public const float kMinComponentLifetime = 0.5f;

		/// <summary>
		/// How big (voxels) should a component be before it can be updated or removed.
		/// </summary>
		public const int kMinComponentSize = 15;

		/// <summary>
		/// Max number how many voxels to process each iteration.
		/// </summary>
		public const int kMaxVoxelsProcessedPerIteration = WorldGridInfo.kTotalVoxelsInChunk;

		/// <summary>
		/// Fluids with viscosity lower or equal than this will not be equalized (will have 'stairs' effect preserved and no pressure).
		/// </summary>
		public const byte kMaxViscosityNotEqualize = (byte)Viscosity.Lava;

		#endregion

		public List<FluidComponent> Components;

		public FluidComponentsMaintenanceJob MaintenanceJob;

		// TODO replace with NativeHashMap when updated and made iterable, also in FluidComponent
		/// <summary>
		/// Indices of voxels which are yet to be assigned to a component.
		/// Note:	we need to access it from multiple jobs in parallel.
		///			NativeHashMap.Concurrent or NativeList can be used only in a single IJobParallelFor, not in many IJobs running in parallel.
		///			But in IJobParallelFor we can't read the NativeLists of FluidBlockSimData ('declared as [WriteOnly]' exception).
		///			So we need to use HashSet with locking or something from System.Collections.Concurrent.
		/// </summary>
		public HashSet<VectorI3> VoxelsToProcess;
		public readonly object hashSetLock = new object();

		public bool RebuildEnabled = true;

		private List<FluidComponent> _componentsWithJobsRunning;
		private WorldApi _worldApi;

		public FluidComponentManager(WorldApi worldApi)
		{
			_worldApi = worldApi;

			MaintenanceJob.CreateData(this, _worldApi);

			Components = new List<FluidComponent>();
			VoxelsToProcess = new HashSet<VectorI3>();
			_componentsWithJobsRunning = new List<FluidComponent>();
		}

		/// <summary>
		/// Returns the component the given voxel belongs to.
		/// </summary>
		public FluidComponent GetComponent(in VectorI3 indices)
		{
			_worldApi.GetVoxelWorldPos(in indices, out Vector3 worldPos);

			for (int i = 0; i < Components.Count; i++)
			{
				FluidComponent component = Components[i];

				// skip components far away
				if (!component.Bounds.Contains(worldPos))
					continue;

				Vector2 row = new Vector2(worldPos.x, worldPos.y);

				if (component.AllSegments.TryGetValue(row, out var segments))
				{
					for (int j = 0; j < segments.Count; j++)
					{
						if (segments[j].Contains(worldPos.z))
							return component;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Remove jobs for components that are done updating or are about to be removed.
		/// </summary>
		public void TryRemoveJobs()
		{
			UnityEngine.Profiling.Profiler.BeginSample("TryRemoveComponentJobs");

			for (int i = 0; i < _componentsWithJobsRunning.Count; i++)
			{
				FluidComponent component = _componentsWithJobsRunning[i];

				if (component.UpdateJob.ToRemove || !component.ToUpdate)
				{
					component.UpdateJob.DestroyData();
					_componentsWithJobsRunning.RemoveAtViaEndSwap(i--);
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Creates data required for a component update and configures job for each component in need of an update.
		/// </summary>
		public void TryCreateJobs()
		{
			UnityEngine.Profiling.Profiler.BeginSample("TryCreateComponentJobs");

			for (int i = 0; i < Components.Count; i++)
			{
				FluidComponent component = Components[i];

				// skip components with existing job or the ones that dont need an update
				if (component.UpdateJob.IsRunning || !component.ToUpdate)
					continue;

				component.UpdateJob.CreateData(_worldApi, this, component);

				_componentsWithJobsRunning.Add(component);
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Prepares the individual jobs for update and maintenance of components, creates dependencies between them and actually schedules them for execution in worker threads.
		/// </summary>
		public JobHandle TryScheduleJobs(JobHandle dependency)
		{
			UnityEngine.Profiling.Profiler.BeginSample("TryScheduleComponentJobs");

			MaintenanceJob.DebugColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
			MaintenanceJob.RealtimeSinceStartup = Time.realtimeSinceStartup;

			JobHandle updateCompleteHandle = dependency;

			if (_componentsWithJobsRunning.Count > 0)
			{
				NativeArray<JobHandle> updatePartHandles = new NativeArray<JobHandle>(_componentsWithJobsRunning.Count, Unity.Collections.Allocator.Temp, NativeArrayOptions.UninitializedMemory);

				for (int i = 0; i < _componentsWithJobsRunning.Count; i++)
				{
					updatePartHandles[i] = _componentsWithJobsRunning[i].UpdateJob.Schedule(updateCompleteHandle);
				}

				updateCompleteHandle = JobHandle.CombineDependencies(updatePartHandles);
				updatePartHandles.Dispose();
			}

			JobHandle maintenanceCompleteHandle = MaintenanceJob.Schedule(updateCompleteHandle);

			UnityEngine.Profiling.Profiler.EndSample();

			return maintenanceCompleteHandle;
		}

		public void Dispose()
		{
			MaintenanceJob.DestroyData();

			for (int i = 0; i < _componentsWithJobsRunning.Count; i++)
			{
				Components[i].UpdateJob.DestroyData();
			}
		}
	}
}