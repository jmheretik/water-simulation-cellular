using System;
using System.Collections.Generic;
using TerrainEngine;
using TerrainEngine.Fluid.New;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// Supported fluid viscosities. The smaller the more viscous.
/// </summary>
public enum Viscosity : byte
{
	Water = byte.MaxValue,
	Lava = 20
}

/// <summary>
/// Allows defining different material and shader for each Viscosity in inspector.
/// </summary>
[Serializable]
public struct FluidType
{
	public Viscosity viscosity;
	public Material material;
	public Shader shader;
}

/// <summary>
/// Individual steps of single iteration of simulation.
/// </summary>
public enum SimulationStep
{
	Up,
	Down,
	Sideways
}

/// <summary>
/// Handles fluid modification and simulation.
/// </summary>
public class FluidProcessor : MonoBehaviour, IDisposable
{
	/// <summary>
	/// Number of simulation steps. Must correspond to number of members of SimulationStep enum.
	/// </summary>
	public const int kSimStepsCount = 3;

	/// <summary>
	/// Supported fluid types with their corresponding materials and shaders.
	/// </summary>
	public static Dictionary<Viscosity, (Material material, Shader shader)> Types;

	[Header("Fluid definition")]
	public FluidType[] FluidTypeDefinitions;

	[Header("Fluid modification")]
	[Range(Voxel.kEpsilon, Voxel.kMaxVolume)]
	public byte FlowValue = 20;
	public int FlowRadius = 2;
	public Viscosity FlowViscosity = Viscosity.Water;

	public FluidComponentManager ComponentManager;

	private List<Block> _blocksWithJobsRunning;
	private JobHandle _mainJobHandle;
	private WorldApi _worldApi;

	public void Initialize()
	{
		_worldApi = GetComponent<WorldApi>();

		_blocksWithJobsRunning = new List<Block>();
		_mainJobHandle = new JobHandle();

		Types = new Dictionary<Viscosity, (Material, Shader)>();

		for (int i = 0; i < FluidTypeDefinitions.Length; i++)
		{
			Types.Add(FluidTypeDefinitions[i].viscosity, (FluidTypeDefinitions[i].material, FluidTypeDefinitions[i].shader));
		}

		ComponentManager = new FluidComponentManager(_worldApi);
	}

	#region modification

	/// <summary>
	/// Adds or removes fluid.
	/// If adding, first tries to add around the given point.
	/// If it fails or if removing, adds or removes at the current top.
	/// </summary>
	public void ModifyFluid(Vector3 point, bool add)
	{
		if (add && TryAddAroundPoint(in point))
			return;

		for (float x = -FlowRadius * WorldGridInfo.kVoxelSize; x <= FlowRadius * WorldGridInfo.kVoxelSize; x += WorldGridInfo.kVoxelSize)
		{
			for (float z = -FlowRadius * WorldGridInfo.kVoxelSize; z <= FlowRadius * WorldGridInfo.kVoxelSize; z += WorldGridInfo.kVoxelSize)
			{
				// start at the bottom of world if adding terrain or at the top otherwise
				Vector3 worldPosition = new Vector3(point.x + x, add ? WorldGridInfo.kVoxelSize : _worldApi.GetHeight() - WorldGridInfo.kVoxelSize, point.z + z);

				// traverse the column at XZ position up or down
				while (worldPosition.y > 0 && worldPosition.y < _worldApi.GetHeight())
				{
					Voxel voxelCopy = _worldApi.TryGetVoxel(in worldPosition, out VectorI3 indices);

					if (!voxelCopy.Valid)
						break;

					// if adding and found a not full voxel yet
					// if removing and found a not empty voxel yet
					if ((add && !voxelCopy.IsFull && (voxelCopy.Viscosity == 0 || voxelCopy.Viscosity == (byte)FlowViscosity)) ||
						(!add && voxelCopy.HasFluid && (voxelCopy.Viscosity == (byte)FlowViscosity)))
					{
						ModifyVoxel(ref voxelCopy, in indices, add);
						break;
					}

					worldPosition.y += add ? WorldGridInfo.kVoxelSize : -WorldGridInfo.kVoxelSize;
				}
			}
		}
	}

	/// <summary>
	/// Try to add fluid around a given point.
	/// </summary>
	private bool TryAddAroundPoint(in Vector3 point)
	{
		bool added = false;

		for (float y = -FlowRadius * WorldGridInfo.kVoxelSize; y <= FlowRadius * WorldGridInfo.kVoxelSize; y += WorldGridInfo.kVoxelSize)
		{
			for (float x = -FlowRadius * WorldGridInfo.kVoxelSize; x <= FlowRadius * WorldGridInfo.kVoxelSize; x += WorldGridInfo.kVoxelSize)
			{
				for (float z = -FlowRadius * WorldGridInfo.kVoxelSize; z <= FlowRadius * WorldGridInfo.kVoxelSize; z += WorldGridInfo.kVoxelSize)
				{
					Vector3 worldPosition = new Vector3(point.x + x, point.y + y, point.z + z);

					Voxel voxelCopy = _worldApi.TryGetVoxel(in worldPosition, out VectorI3 indices);

					if (!voxelCopy.Valid)
						continue;

					if (!voxelCopy.IsFull && (voxelCopy.Viscosity == 0 || voxelCopy.Viscosity == (byte)FlowViscosity))
					{
						ModifyVoxel(ref voxelCopy, in indices, true);

						added = true;
					}
				}
			}
		}

		return added;
	}

	private void ModifyVoxel(ref Voxel voxelCopy, in VectorI3 indices, bool add)
	{
		float scaledFlowValue = FlowValue * (byte)FlowViscosity * Voxel.kByteMaxValueToFloat;

		voxelCopy.Fluid = (byte)Mathf.Clamp(voxelCopy.Fluid + (add ? scaledFlowValue : -scaledFlowValue), 0, byte.MaxValue);

		if (add)
		{
			voxelCopy.Viscosity = (byte)FlowViscosity;
		}
		else
		{
			// fluid subtraction may split the component
			ComponentManager.GetComponent(in indices)?.MarkForRebuild();
		}

		voxelCopy.Unsettle((int)scaledFlowValue);
		_worldApi.UnsettleChunk(in indices);
		_worldApi.SetVoxelAfterSim(in indices, in voxelCopy);
	}

	#endregion

	#region simulation

	/// <summary>
	/// Waits for simulation to complete. Use this when unsure if the new jobs have been scheduled already and voxels can be still safely read.
	/// </summary>
	public void WaitUntilSimulationComplete()
	{
		UnityEngine.Profiling.Profiler.BeginSample("WaitUntilSimulationComplete");

		_mainJobHandle.Complete();

		UnityEngine.Profiling.Profiler.EndSample();
	}

	/// <summary>
	/// Manages the creation, update, removal and scheduling of sim jobs. Handles external changes between the individual iterations of simulation.
	/// Waits in a non-blocking fashion for job completion - returns right away if jobs are still running.
	/// </summary>
	public void FluidUpdate()
	{
		if (!_mainJobHandle.IsCompleted)
			return;

		_mainJobHandle.Complete();

		_worldApi.ProcessPendingChanges();
		_worldApi.UpdateUnsettledMeshes(false);

		TryRemoveJobs();
		TryCreateJobs();
		_mainJobHandle = TryScheduleJobs();

		ComponentManager.TryRemoveJobs();
		ComponentManager.TryCreateJobs();
		_mainJobHandle = ComponentManager.TryScheduleJobs(_mainJobHandle);
	}

	#endregion

	#region jobs

	/// <summary>
	/// Remove jobs for blocks that are done simulating.
	/// </summary>
	private void TryRemoveJobs()
	{
		UnityEngine.Profiling.Profiler.BeginSample("TryRemoveJobs");

		for (int i = 0; i < _blocksWithJobsRunning.Count; i++)
		{
			FluidBlockSimData simData = _blocksWithJobsRunning[i].SimData;

			if (!simData.UpdateChunksToSimulate())
			{
				simData.Destroy();
				_blocksWithJobsRunning.RemoveAtViaEndSwap(i--);
			}
		}

		UnityEngine.Profiling.Profiler.EndSample();
	}

	/// <summary>
	/// Creates data required for a simulation and configures jobs for each block in need of simulation.
	/// </summary>
	private void TryCreateJobs()
	{
		UnityEngine.Profiling.Profiler.BeginSample("TryCreateJobs");

		Block[] blocks = _worldApi.GetBlocks();

		for (int blockId = 0; blockId < blocks.Length; blockId++)
		{
			Block block = blocks[blockId];
			FluidBlockSimData simData = block.SimData;

			// skip blocks with existing jobs or the ones that dont contain any unsettled chunks
			if (simData.IsRunning || !simData.UpdateChunksToSimulate())
				continue;

			simData.Create(ComponentManager);
			simData.Setup();

			_blocksWithJobsRunning.Add(block);
		}

		UnityEngine.Profiling.Profiler.EndSample();
	}

	/// <summary>
	/// Prepares the individual jobs for another simulation iteration, creates dependencies between the separate steps and actually schedules the jobs for execution in worker threads.
	/// </summary>
	private JobHandle TryScheduleJobs()
	{
		if (_blocksWithJobsRunning.Count == 0)
			return default;

		UpdateJobReferences();

		UnityEngine.Profiling.Profiler.BeginSample("TryScheduleJobs");

		JobHandle stepCompleteHandle = default;
		NativeArray<JobHandle> stepPartHandles = new NativeArray<JobHandle>(_blocksWithJobsRunning.Count, Unity.Collections.Allocator.Temp, NativeArrayOptions.UninitializedMemory);

		for (int step = 0; step < kSimStepsCount; step++)
		{
			for (int i = 0; i < _blocksWithJobsRunning.Count; i++)
			{
				stepPartHandles[i] = _blocksWithJobsRunning[i].SimData.SimJobs[step].Schedule(stepCompleteHandle);
			}

			// dont start a new step before the last is done in all blocks
			stepCompleteHandle = JobHandle.CombineDependencies(stepPartHandles);
		}

		for (int i = 0; i < _blocksWithJobsRunning.Count; i++)
		{
			stepPartHandles[i] = _blocksWithJobsRunning[i].SimData.MaintenanceJob.Schedule(stepCompleteHandle);
		}

		stepCompleteHandle = JobHandle.CombineDependencies(stepPartHandles);
		stepPartHandles.Dispose();

		UnityEngine.Profiling.Profiler.EndSample();

		return stepCompleteHandle;
	}

	/// <summary>
	/// Updates the references to neighbour blocks so that every job reads the latest values every step.
	/// </summary>
	private void UpdateJobReferences()
	{
		UnityEngine.Profiling.Profiler.BeginSample("UpdateJobReferences");

		for (int i = 0; i < _blocksWithJobsRunning.Count; i++)
		{
			Block block = _blocksWithJobsRunning[i];

			for (int step = 0; step < kSimStepsCount; step++)
			{
				if (block.Top != null)
					block.SimData.SimJobs[step].TopBlock = block.Top.SimData.IsRunning ? block.Top.SimData.SimJobs[step].ReadVoxels : block.Top.Voxels;

				if (block.Bottom != null)
					block.SimData.SimJobs[step].BottomBlock = block.Bottom.SimData.IsRunning ? block.Bottom.SimData.SimJobs[step].ReadVoxels : block.Bottom.Voxels;

				if (block.Forward != null)
					block.SimData.SimJobs[step].ForwardBlock = block.Forward.SimData.IsRunning ? block.Forward.SimData.SimJobs[step].ReadVoxels : block.Forward.Voxels;

				if (block.Backward != null)
					block.SimData.SimJobs[step].BackwardBlock = block.Backward.SimData.IsRunning ? block.Backward.SimData.SimJobs[step].ReadVoxels : block.Backward.Voxels;

				if (block.Right != null)
					block.SimData.SimJobs[step].RightBlock = block.Right.SimData.IsRunning ? block.Right.SimData.SimJobs[step].ReadVoxels : block.Right.Voxels;

				if (block.Left != null)
					block.SimData.SimJobs[step].LeftBlock = block.Left.SimData.IsRunning ? block.Left.SimData.SimJobs[step].ReadVoxels : block.Left.Voxels;
			}
		}

		UnityEngine.Profiling.Profiler.EndSample();
	}

	public void Dispose()
	{
		_mainJobHandle.Complete();

		for (int i = 0; i < _blocksWithJobsRunning.Count; i++)
		{
			_blocksWithJobsRunning[i].SimData.Destroy();
		}

		ComponentManager.Dispose();
	}

	#endregion

}
