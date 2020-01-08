using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace TerrainEngine.Fluid.New
{
	// TODO unity mathematics everywhere in the solution?
	/// <summary>
	/// A background job where the actual fluid simulation takes place.
	/// </summary>
	[BurstCompile]
	public struct FluidBlockSimJob : IJob
	{
		[ReadOnly] public int BlockId;
		[ReadOnly] public SimulationStep Step;

		[ReadOnly] public NativeArray<Voxel> ForwardBlock;
		[ReadOnly] public NativeArray<Voxel> BackwardBlock;
		[ReadOnly] public NativeArray<Voxel> TopBlock;
		[ReadOnly] public NativeArray<Voxel> BottomBlock;
		[ReadOnly] public NativeArray<Voxel> RightBlock;
		[ReadOnly] public NativeArray<Voxel> LeftBlock;

		[ReadOnly] public NativeArray<Voxel> ReadVoxels;
		[WriteOnly] public NativeArray<Voxel> WriteVoxels;

		[ReadOnly] public NativeList<int> ChunksToSimulate;
		[WriteOnly] public NativeList<int> ChunksToUnsettle;
		[WriteOnly] public NativeList<VectorI3> VoxelsToProcess;

		/// <summary>
		/// Calculates a new state for each voxel according to the states of its neighbours.
		/// </summary>
		public void Execute()
		{
			for (int i = 0; i < ChunksToSimulate.Length; i++)
			{
				int chunkId = ChunksToSimulate[i];
				bool chunkUnsettled = false;

				for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
				{
					// TODO replace GetWritable with Get once Burst supports return ref readonly
					ref readonly Voxel voxel = ref ReadVoxels.GetWritable(chunkId, voxelId);

					int blockVoxelId = WorldGridInfoHelper.ChunkToBlockVoxelId(chunkId, voxelId);
					WriteVoxels[blockVoxelId] = voxel;

					// skip settled terrain
					if (voxel.Settled && voxel.Solid == Voxel.kMaxVolume && voxel.Fluid == 0)
						continue;

					ref readonly Voxel forward = ref (!WorldGridInfoHelper.GetNeighbour(chunkId, voxelId, Neighbour.Forward, out int nChunkId, out int nVoxelId) ? ref ReadVoxels.GetWritable(nChunkId, nVoxelId) : ref (ForwardBlock.Length > 0 ? ref ForwardBlock.GetWritable(nChunkId, nVoxelId) : ref Voxel.Invalid));
					ref readonly Voxel backward = ref (!WorldGridInfoHelper.GetNeighbour(chunkId, voxelId, Neighbour.Backward, out nChunkId, out nVoxelId) ? ref ReadVoxels.GetWritable(nChunkId, nVoxelId) : ref (BackwardBlock.Length > 0 ? ref BackwardBlock.GetWritable(nChunkId, nVoxelId) : ref Voxel.Invalid));
					ref readonly Voxel top = ref (!WorldGridInfoHelper.GetNeighbour(chunkId, voxelId, Neighbour.Top, out nChunkId, out nVoxelId) ? ref ReadVoxels.GetWritable(nChunkId, nVoxelId) : ref (TopBlock.Length > 0 ? ref TopBlock.GetWritable(nChunkId, nVoxelId) : ref Voxel.Invalid));
					ref readonly Voxel bottom = ref (!WorldGridInfoHelper.GetNeighbour(chunkId, voxelId, Neighbour.Bottom, out nChunkId, out nVoxelId) ? ref ReadVoxels.GetWritable(nChunkId, nVoxelId) : ref (BottomBlock.Length > 0 ? ref BottomBlock.GetWritable(nChunkId, nVoxelId) : ref Voxel.Invalid));
					ref readonly Voxel right = ref (!WorldGridInfoHelper.GetNeighbour(chunkId, voxelId, Neighbour.Right, out nChunkId, out nVoxelId) ? ref ReadVoxels.GetWritable(nChunkId, nVoxelId) : ref (RightBlock.Length > 0 ? ref RightBlock.GetWritable(nChunkId, nVoxelId) : ref Voxel.Invalid));
					ref readonly Voxel left = ref (!WorldGridInfoHelper.GetNeighbour(chunkId, voxelId, Neighbour.Left, out nChunkId, out nVoxelId) ? ref ReadVoxels.GetWritable(nChunkId, nVoxelId) : ref (LeftBlock.Length > 0 ? ref LeftBlock.GetWritable(nChunkId, nVoxelId) : ref Voxel.Invalid));

					// skip settled voxel with settled neighbours = air or settled fluid below the water surface
					if (voxel.Settled && top.Settled && bottom.Settled && forward.Settled && backward.Settled && right.Settled && left.Settled)
						continue;

					Voxel writeVoxel = voxel;

					// main simulation steps
					switch (Step)
					{
						case SimulationStep.Up:
							FlowUp(in voxel, in top, in bottom, ref writeVoxel);
							break;

						case SimulationStep.Down:
							FlowDown(in voxel, in top, in bottom, ref writeVoxel);
							break;

						case SimulationStep.Sideways:
							FlowSideways(in voxel, in forward, in backward, in right, in left, ref writeVoxel);
							break;
					}

					// voxel settling
					bool falling = top.HasFluid && !bottom.Settled;
					int diff = voxel.Fluid - writeVoxel.Fluid;

					// fluid changed
					if (diff != 0)
					{
						writeVoxel.Unsettle(diff);
					}
					else if (Step == SimulationStep.Sideways && !voxel.Settled && !falling)
					{
						writeVoxel.DecreaseSettle();

						// just settled
						if (writeVoxel.Settled && writeVoxel.HasFluid)
						{
							VoxelsToProcess.Add(new VectorI3(BlockId, chunkId, voxelId));
						}
					}

					// chunk unsettling
					if (!chunkUnsettled && !writeVoxel.Settled)
					{
						chunkUnsettled = true;
						ChunksToUnsettle.Add(chunkId);
					}

					WriteVoxels[blockVoxelId] = writeVoxel;
				}
			}
		}

		#region steps

		/// <summary>
		/// Give all excess to voxel above and take all excess from voxel below.
		/// </summary>
		private void FlowUp(in Voxel voxel, in Voxel topNeighbour, in Voxel bottomNeighbour, ref Voxel writeVoxel)
		{
			if (!voxel.HasCompatibleViscosity(in bottomNeighbour))
				return;

			int transfer = 0;

			transfer -= math.select(0, voxel.ExcessVolume, voxel.HasCompatibleViscosity(in topNeighbour));
			transfer += bottomNeighbour.ExcessVolume;

			ModifyVoxel(ref writeVoxel, transfer, bottomNeighbour.Viscosity);
		}

		/// <summary>
		/// Give as much as possible to voxel below and take as much as possible from voxel above.
		/// </summary>
		private void FlowDown(in Voxel voxel, in Voxel topNeighbour, in Voxel bottomNeighbour, ref Voxel writeVoxel)
		{
			if (!voxel.HasCompatibleViscosity(in topNeighbour))
				return;

			int transfer = 0;

			transfer -= math.select(0, math.clamp(voxel.Fluid, 0, bottomNeighbour.FreeVolume), voxel.HasCompatibleViscosity(in bottomNeighbour));
			transfer += math.clamp(topNeighbour.Fluid, 0, voxel.FreeVolume);

			ModifyVoxel(ref writeVoxel, transfer, topNeighbour.Viscosity);
		}

		/// <summary>
		/// Proportionally distribute the fluid to horizontal neighbours which have less than this voxel.
		/// </summary>
		private void FlowSideways(in Voxel voxel, in Voxel forward, in Voxel backward, in Voxel right, in Voxel left, ref Voxel writeVoxel)
		{
			Voxel neighbour = voxel;

			for (int i = 0; i < Voxel.kNeighbourCount; i++)
			{
				switch ((Neighbour)i)
				{
					case Neighbour.Forward:
						neighbour = forward;
						break;
					case Neighbour.Backward:
						neighbour = backward;
						break;
					case Neighbour.Right:
						neighbour = right;
						break;
					case Neighbour.Left:
						neighbour = left;
						break;
					default:
						continue;
				}

				if (!voxel.HasCompatibleViscosity(in neighbour))
					continue;

				float transfer = 0;

				// proportional difference in fluid between this voxel and the neighbour
				float diff = math.mul(voxel.CurrentVolume - neighbour.CurrentVolume, Voxel.kNeighbourShareFloat);

				// give to the neighbour which has less, clamped by the max share of this voxel's fluid
				transfer -= math.clamp(diff, 0, math.mul(voxel.Fluid, Voxel.kNeighbourShareFloat));

				// take from the neighbour which has more, clamped by the max share of its fluid
				transfer += math.clamp(-diff, 0, math.mul(neighbour.Fluid, Voxel.kNeighbourShareFloat));

				ModifyVoxel(ref writeVoxel, transfer, neighbour.Viscosity, true);
			}
		}

		#endregion

		/// <summary>
		/// Scales the volume transfer according to fluid's expected viscosity and writes the new fluid value and viscosity.
		/// </summary>
		private void ModifyVoxel(ref Voxel writeVoxel, double transfer, int neighbourViscosity, bool sideways = false)
		{
			// choose non-zero viscosity
			byte viscosity = (byte)math.max(writeVoxel.Viscosity, neighbourViscosity);

			if (sideways)
			{
				// scale transfer by viscosity
				float floatViscosity = math.mul(viscosity, Voxel.kByteMaxValueToFloat);

				transfer = math.mul(transfer, floatViscosity);

				// handle edge case when transfer is too small (between -1 and 1)
				transfer = math.select(transfer, 1, transfer < 1 && transfer > floatViscosity);
				transfer = math.select(transfer, -1, transfer > -1 && transfer < -floatViscosity);
			}

			writeVoxel.Fluid = (byte)math.clamp(writeVoxel.Fluid + math.round(transfer), 0, byte.MaxValue);
			writeVoxel.Viscosity = (byte)math.select(0, viscosity, writeVoxel.HasFluid);
		}
	}
}