using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Holds and manages data required for fluid simulation in a block.
	/// </summary>
	public class FluidBlockSimData : IDisposable
	{
		public bool IsRunning;

		/// <summary>
		/// One job for each step of a simulation stored separately so that dependencies between them can be created.
		/// </summary>
		public FluidBlockSimJob[] SimJobs;
		public FluidBlockMaintenanceJob MaintenanceJob;

		public NativeArray<Voxel> ReadVoxels;

		// TODO replace with NativeHashMap when updated and made iterable or integrate somehow with FluidBlockDataImpl.ChunksToSimulate
		public HashSet<int> UnsettledChunks;
		public readonly object hashSetLock = new object();

		/// <summary>
		/// A flag reflecting if ReadVoxels and WriteVoxels are currently swapped (double buffering technique).
		/// </summary>
		private bool _writeIsRead;
		private NativeArray<Voxel> _writeVoxels;
		private NativeList<int> _chunksToSimulate;
		private NativeList<int> _chunksToUnsettle;
		private NativeList<VectorI3> _voxelsToProcess;
		private NativeArray<Voxel> _emptyHelperArray;
		private GCHandle _blockPointer;
		private GCHandle _componentManagerPointer;
		private Block _block;

		public FluidBlockSimData(Block block)
		{
			_block = block;

			UnsettledChunks = new HashSet<int>();
			_chunksToSimulate = new NativeList<int>(Unity.Collections.Allocator.Persistent);

			SimJobs = new FluidBlockSimJob[FluidProcessor.kSimStepsCount];
		}

		/// <summary>
		/// Rebuilds chunk settled in previous iteration and determine which chunks to simulate next.
		/// Returns false if there are no chunks to simulate in the block.
		/// </summary>
		public bool UpdateChunksToSimulate()
		{
			UnityEngine.Profiling.Profiler.BeginSample("UpdateChunksToSimulate");

			// mark chunks settled in the last iteration for rebuild
			for (int i = 0; i < _chunksToSimulate.Length; i++)
			{
				int chunkId = _chunksToSimulate[i];

				if (!UnsettledChunks.Contains(chunkId))
					_block.Chunks[chunkId].RenderData.FluidNeedsRebuild = true;
			}

			_chunksToSimulate.Clear();

			// add chunks to simulate in the next iteration
			foreach (int chunkId in UnsettledChunks)
			{
				_chunksToSimulate.Add(chunkId);
			}

			UnsettledChunks.Clear();

			UnityEngine.Profiling.Profiler.EndSample();

			return _chunksToSimulate.Length > 0;
		}

		public void Create(FluidComponentManager componentManager)
		{
			UnityEngine.Profiling.Profiler.BeginSample("CreateSimData");

			_writeIsRead = false;
			_writeVoxels = new NativeArray<Voxel>(ReadVoxels, Unity.Collections.Allocator.Persistent);
			_chunksToUnsettle = new NativeList<int>(Unity.Collections.Allocator.Persistent);
			_voxelsToProcess = new NativeList<VectorI3>(Unity.Collections.Allocator.Persistent);
			_emptyHelperArray = new NativeArray<Voxel>(0, Unity.Collections.Allocator.Persistent);
			_blockPointer = GCHandle.Alloc(_block, GCHandleType.Pinned);
			_componentManagerPointer = GCHandle.Alloc(componentManager, GCHandleType.Pinned);

			UnityEngine.Profiling.Profiler.EndSample();
		}

		public void Setup()
		{
			UnityEngine.Profiling.Profiler.BeginSample("SetupJobs");

			IsRunning = true;

			ref FluidBlockSimJob simJob = ref SimJobs[(int)SimulationStep.Up];
			simJob.BlockId = _block.Id;
			simJob.Step = SimulationStep.Up;
			simJob.ReadVoxels = ReadVoxels;
			simJob.WriteVoxels = _writeVoxels;
			simJob.ChunksToSimulate = _chunksToSimulate;
			simJob.ChunksToUnsettle = _chunksToUnsettle;
			simJob.VoxelsToProcess = _voxelsToProcess;

			if (_block.Top == null)
				simJob.TopBlock = _emptyHelperArray;
			if (_block.Bottom == null)
				simJob.BottomBlock = _emptyHelperArray;
			if (_block.Forward == null)
				simJob.ForwardBlock = _emptyHelperArray;
			if (_block.Backward == null)
				simJob.BackwardBlock = _emptyHelperArray;
			if (_block.Right == null)
				simJob.RightBlock = _emptyHelperArray;
			if (_block.Left == null)
				simJob.LeftBlock = _emptyHelperArray;

			ref FluidBlockSimJob simJob1 = ref SimJobs[(int)SimulationStep.Down];
			simJob1 = simJob;
			simJob1.Step = SimulationStep.Down;
			simJob1.ReadVoxels = _writeVoxels;
			simJob1.WriteVoxels = ReadVoxels;

			ref FluidBlockSimJob simJob2 = ref SimJobs[(int)SimulationStep.Sideways];
			simJob2 = simJob;
			simJob2.Step = SimulationStep.Sideways;

			ref FluidBlockMaintenanceJob maintenanceJob = ref MaintenanceJob;
			maintenanceJob.ChunksToUnsettle = _chunksToUnsettle;
			maintenanceJob.VoxelsToProcess = _voxelsToProcess;
			maintenanceJob.BlockPointer = _blockPointer;
			maintenanceJob.ComponentManagerPointer = _componentManagerPointer;

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Swap read and write arrays in a job (double buffering) to continue the simulation.
		/// </summary>
		public void SwapArrays()
		{
			UnityEngine.Profiling.Profiler.BeginSample("SwapArrays");

			NativeArray<Voxel> tmp = ReadVoxels;
			ReadVoxels = _writeVoxels;
			_writeVoxels = tmp;

			_writeIsRead = !_writeIsRead;

			// updates the references to swapped read/write arrays for every step
			for (int step = 0; step < FluidProcessor.kSimStepsCount; step++)
			{
				ref FluidBlockSimJob simJob = ref SimJobs[step];

				bool even = (step % 2 == 0);
				simJob.ReadVoxels = even ? ReadVoxels : _writeVoxels;
				simJob.WriteVoxels = even ? _writeVoxels : ReadVoxels;
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Deallocates data required for simulation in a block and sets the pointer to ReadVoxels as it was before the simulation.
		/// </summary>
		public void Destroy()
		{
			UnityEngine.Profiling.Profiler.BeginSample("DestroySimData");

			if (IsRunning)
			{
				IsRunning = false;

				if (!_writeIsRead)
				{
					_writeVoxels.Dispose();
				}
				else
				{
					ReadVoxels.Dispose();
					ReadVoxels = _writeVoxels;
				}

				_chunksToUnsettle.Dispose();
				_voxelsToProcess.Dispose();
				_emptyHelperArray.Dispose();
				_blockPointer.Free();
				_componentManagerPointer.Free();
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		public void Dispose()
		{
			_chunksToSimulate.Dispose();
		}
	}
}