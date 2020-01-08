using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// A background job where the results of the completed sim jobs are processed.
	/// </summary>
	public struct FluidBlockMaintenanceJob : IJob
	{
		public NativeList<int> ChunksToUnsettle;
		public NativeList<VectorI3> VoxelsToProcess;

		public GCHandle BlockPointer;
		public GCHandle ComponentManagerPointer;

		public void Execute()
		{
			Block block = (Block)BlockPointer.Target;

			// unsettle chunks
			for (int i = 0; i < ChunksToUnsettle.Length; i++)
			{
				block.Chunks[ChunksToUnsettle[i]].Unsettle();
			}

			ChunksToUnsettle.Clear();

			// add just settled voxels to components
			FluidComponentManager componentManager = (FluidComponentManager)ComponentManagerPointer.Target;

			lock (componentManager.hashSetLock)
			{
				for (int i = 0; i < VoxelsToProcess.Length; i++)
				{
					componentManager.VoxelsToProcess.Add(VoxelsToProcess[i]);
				}
			}

			VoxelsToProcess.Clear();

			// prepare for the next iteration
			FluidBlockSimData simData = block.SimData;

			simData.SwapArrays();
		}
	}
}