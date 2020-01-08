using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	public class Block : MonoBehaviour, IDisposable
	{
		#region constants

		public const int kRow = WorldGridInfo.kChunksPerBlock;
		public const int kColumn = kRow * kRow;
		public const int kOffset = 1 - kRow;
		public const int kRowOffseted = kRow * kOffset;
		public const int kColumnOffseted = kColumn * kOffset;

		#endregion

		public int Id;

		public VectorI3 Coords;
		public Vector3 WorldPos;

		public Block Top;
		public Block Bottom;
		public Block Forward;
		public Block Backward;
		public Block Right;
		public Block Left;

		public Chunk[] Chunks;

		public FluidBlockSimData SimData;

		/// <summary>
		/// A pointer to the latest version of array of voxels. To be used for reading between simulation iterations. Use for writing only if you know what you're doing.
		/// Call fluidProcessor.WaitUntilSimulationComplete prior to access when unsure if new jobs might have already been scheduled.
		/// </summary>
		public NativeArray<Voxel> Voxels
		{
			get
			{
				return SimData.ReadVoxels;
			}
		}

		/// <summary>
		/// Underlying array holding all chunks of voxels within a block.
		/// Not to be used directly because the simulation uses double buffering technique so this array might not be always safe to access while the simulation is running and might not contain the latest values.
		/// To access voxels in a block use Voxels.
		/// </summary>
		private NativeArray<Voxel> _voxels;

		/// <summary>
		/// Initialize array of voxels and chunks.
		/// </summary>
		public void Initialize(int id, Transform parent, WorldApi worldApi, VectorI3 coords)
		{
			Id = id;
			transform.parent = parent;
			Coords = coords;

			worldApi.GetBlockWorldPos(id, out Vector3 worldPos);
			WorldPos = worldPos;

			Chunks = new Chunk[WorldGridInfo.kTotalChunksInBlock];
			_voxels = new NativeArray<Voxel>(WorldGridInfo.kTotalVoxelsInBlock, Unity.Collections.Allocator.Persistent);

			SimData = new FluidBlockSimData(this);
			SimData.ReadVoxels = _voxels;

			for (int chunkX = 0; chunkX < WorldGridInfo.kChunksPerBlock; chunkX++)
			{
				for (int chunkY = 0; chunkY < WorldGridInfo.kChunksPerBlock; chunkY++)
				{
					for (int chunkZ = 0; chunkZ < WorldGridInfo.kChunksPerBlock; chunkZ++)
					{
						int chunkId = chunkX * WorldGridInfo.kChunksPerBlock * WorldGridInfo.kChunksPerBlock + chunkY * WorldGridInfo.kChunksPerBlock + chunkZ;

						Chunk chunk = new GameObject($"Chunk ({chunkX}, {chunkY}, {chunkZ})").AddComponent<Chunk>();
						Chunks[chunkId] = chunk;

						chunk.Initialize(chunkId, transform, worldApi, this);

						for (int voxelX = 0; voxelX < WorldGridInfo.kVoxelsPerChunk; voxelX++)
						{
							for (int voxelY = 0; voxelY < WorldGridInfo.kVoxelsPerChunk; voxelY++)
							{
								for (int voxelZ = 0; voxelZ < WorldGridInfo.kVoxelsPerChunk; voxelZ++)
								{
									int voxelId = voxelX * WorldGridInfo.kVoxelsPerChunk * WorldGridInfo.kVoxelsPerChunk + voxelY * WorldGridInfo.kVoxelsPerChunk + voxelZ;

									_voxels.GetWritable(chunkId, voxelId).Valid = true;
								}
							}
						}
					}
				}
			}
		}

		public void InitializeChunkReferences()
		{
			for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
			{
				Chunk chunk = Chunks[chunkId];

				chunk.Forward = !WorldGridInfoHelper.IsNeighbourAtBorder(chunkId, Neighbour.Forward, WorldGridInfo.kChunksPerBlock, WorldGridInfo.kChunksPerBlockLog2) ? Chunks[chunkId + 1] : chunk.Block.Forward?.Chunks?[chunkId + kOffset];
				chunk.Backward = !WorldGridInfoHelper.IsNeighbourAtBorder(chunkId, Neighbour.Backward, WorldGridInfo.kChunksPerBlock, WorldGridInfo.kChunksPerBlockLog2) ? Chunks[chunkId - 1] : chunk.Block.Backward?.Chunks?[chunkId - kOffset];

				chunk.Top = !WorldGridInfoHelper.IsNeighbourAtBorder(chunkId, Neighbour.Top, WorldGridInfo.kChunksPerBlock, WorldGridInfo.kChunksPerBlockLog2) ? Chunks[chunkId + kRow] : chunk.Block.Top?.Chunks?[chunkId + kRowOffseted];
				chunk.Bottom = !WorldGridInfoHelper.IsNeighbourAtBorder(chunkId, Neighbour.Bottom, WorldGridInfo.kChunksPerBlock, WorldGridInfo.kChunksPerBlockLog2) ? Chunks[chunkId - kRow] : chunk.Block.Bottom?.Chunks?[chunkId - kRowOffseted];

				chunk.Right = !WorldGridInfoHelper.IsNeighbourAtBorder(chunkId, Neighbour.Right, WorldGridInfo.kChunksPerBlock, WorldGridInfo.kChunksPerBlockLog2) ? Chunks[chunkId + kColumn] : chunk.Block.Right?.Chunks?[chunkId + kColumnOffseted];
				chunk.Left = !WorldGridInfoHelper.IsNeighbourAtBorder(chunkId, Neighbour.Left, WorldGridInfo.kChunksPerBlock, WorldGridInfo.kChunksPerBlockLog2) ? Chunks[chunkId - kColumn] : chunk.Block.Left?.Chunks?[chunkId - kColumnOffseted];
			}
		}

		public Block GetNeighbour(Neighbour neighbour)
		{
			switch (neighbour)
			{
				case Neighbour.Top:
					return Top;
				case Neighbour.Bottom:
					return Bottom;
				case Neighbour.Forward:
					return Forward;
				case Neighbour.Backward:
					return Backward;
				case Neighbour.Left:
					return Left;
				case Neighbour.Right:
					return Right;
			}

			return null;
		}

		public void Dispose()
		{
			_voxels.Dispose();
			SimData.Dispose();

			for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
			{
				Chunks[chunkId].Dispose();
			}
		}
	}
}