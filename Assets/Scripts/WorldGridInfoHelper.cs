using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Helper for the original WorldGridInfo in Ylands.
	/// </summary>
	public static class WorldGridInfoHelper
	{
		public static readonly Vector3 GetVoxelSizeV3 = new Vector3(WorldGridInfo.kVoxelSize, WorldGridInfo.kVoxelSize, WorldGridInfo.kVoxelSize);

		/// <summary>
		/// Chunk-local voxelId to block-local voxelId.
		/// </summary>
		public static int ChunkToBlockVoxelId(int chunkId, int voxelId)
		{
			return chunkId * WorldGridInfo.kTotalVoxelsInChunk + voxelId;
		}

		/// <summary>
		/// Returns neighbour chunkId and voxelId and true if neighbour voxel is in another block.
		/// </summary>
		public static bool GetNeighbour(int chunkId, int voxelId, Neighbour neighbour, out int nChunkId, out int nVoxelId)
		{
			bool voxelAtChunkBorder = IsNeighbourAtBorder(voxelId, neighbour);
			bool chunkAtBlockBorder = IsNeighbourAtBorder(chunkId, neighbour, WorldGridInfo.kChunksPerBlock, WorldGridInfo.kChunksPerBlockLog2);

			nVoxelId = voxelId;
			nChunkId = chunkId;

			switch (neighbour)
			{
				case Neighbour.Forward:
					nVoxelId += math.select(1, Chunk.kOffset, voxelAtChunkBorder);
					nChunkId += math.select(0, math.select(1, Block.kOffset, chunkAtBlockBorder), voxelAtChunkBorder);
					break;

				case Neighbour.Backward:
					nVoxelId -= math.select(1, Chunk.kOffset, voxelAtChunkBorder);
					nChunkId -= math.select(0, math.select(1, Block.kOffset, chunkAtBlockBorder), voxelAtChunkBorder);
					break;

				case Neighbour.Top:
					nVoxelId += math.select(Chunk.kRow, Chunk.kRowOffseted, voxelAtChunkBorder);
					nChunkId += math.select(0, math.select(Block.kRow, Block.kRowOffseted, chunkAtBlockBorder), voxelAtChunkBorder);
					break;

				case Neighbour.Bottom:
					nVoxelId -= math.select(Chunk.kRow, Chunk.kRowOffseted, voxelAtChunkBorder);
					nChunkId -= math.select(0, math.select(Block.kRow, Block.kRowOffseted, chunkAtBlockBorder), voxelAtChunkBorder);
					break;

				case Neighbour.Right:
					nVoxelId += math.select(Chunk.kColumn, Chunk.kColumnOffseted, voxelAtChunkBorder);
					nChunkId += math.select(0, math.select(Block.kColumn, Block.kColumnOffseted, chunkAtBlockBorder), voxelAtChunkBorder);
					break;

				case Neighbour.Left:
					nVoxelId -= math.select(Chunk.kColumn, Chunk.kColumnOffseted, voxelAtChunkBorder);
					nChunkId -= math.select(0, math.select(Block.kColumn, Block.kColumnOffseted, chunkAtBlockBorder), voxelAtChunkBorder);
					break;
			}

			return voxelAtChunkBorder && chunkAtBlockBorder;
		}

		/// <summary>
		/// Returns true if a neighbour in a given direction from a voxel/chunk/block at given id is at the border of the chunk or block it belongs to.
		/// Inspired by WorldGridInfo.VoxelIdToChunkVoxel()
		/// </summary>
		public static bool IsNeighbourAtBorder(int id, Neighbour neighbour, int elemsPerDim = WorldGridInfo.kVoxelsPerChunk, int elemsPerDimLog2 = WorldGridInfo.kVoxelsPerChunkLog2)
		{
			int pos = id;

			switch (neighbour)
			{
				case Neighbour.Top:
				case Neighbour.Bottom:
					pos >>= elemsPerDimLog2;
					break;

				case Neighbour.Right:
				case Neighbour.Left:
					pos >>= elemsPerDimLog2 * 2;
					break;
			}

			pos &= elemsPerDim - 1;

			switch (neighbour)
			{
				case Neighbour.Forward:
				case Neighbour.Top:
				case Neighbour.Right:
					return pos == elemsPerDim - 1;

				case Neighbour.Backward:
				case Neighbour.Bottom:
				case Neighbour.Left:
					return pos == 0;
			}

			return false;
		}

		/// <summary>
		/// Slower calculation of IsNeighbourAtBorder in cases elemsPerDim is not a power of 2.
		/// </summary>
		public static bool IsNeighbourAtBorderSlow(int id, Neighbour neighbour, int row = Chunk.kRow, int column = Chunk.kColumn, int totalElements = WorldGridInfo.kTotalVoxelsInChunk)
		{
			switch (neighbour)
			{
				case Neighbour.Forward:
					return id + 1 >= totalElements || (id + 1) / row != id / row;

				case Neighbour.Backward:
					return id - 1 < 0 || (id - 1) / row != id / row;

				case Neighbour.Top:
					return id + row >= totalElements || (id + row) / column != id / column;

				case Neighbour.Bottom:
					return id - row < 0 || (id - row) / column != id / column;

				case Neighbour.Right:
					return id + column >= totalElements;

				case Neighbour.Left:
					return id - column < 0;

				default:
					return true;
			}
		}
	}
}
