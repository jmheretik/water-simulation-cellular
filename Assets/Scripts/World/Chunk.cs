using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	public class Chunk : MonoBehaviour, IDisposable
	{
		#region constants

		public const int kRow = WorldGridInfo.kVoxelsPerChunk;
		public const int kRowBordered = kRow + 2;
		public const int kColumn = kRow * kRow;
		public const int kColumnBordered = kRowBordered * kRowBordered;
		public const int kTotalVoxelsInBordered = kRowBordered * kRowBordered * kRowBordered;
		public const int kOffset = 1 - kRow;
		public const int kRowOffseted = kRow * kOffset;
		public const int kColumnOffseted = kColumn * kOffset;

		#endregion

		public Block Block;

		public int Id;

		public Vector3 WorldPos;

		public Chunk Top;
		public Chunk Bottom;
		public Chunk Forward;
		public Chunk Backward;
		public Chunk Right;
		public Chunk Left;

		public ChunkRenderData RenderData;

		public void Initialize(int id, Transform parent, WorldApi worldApi, Block block)
		{
			Id = id;
			transform.parent = parent;
			Block = block;

			worldApi.GetChunkWorldPos(block.Id, Id, out Vector3 worldPos);
			WorldPos = worldPos;

			RenderData = new ChunkRenderData(this);
		}

		/// <summary>
		/// Marks chunk as unsettled to notify the simulation of changes in this chunk and also so that its mesh gets updated.
		/// </summary>
		public void Unsettle(bool alsoNeighbours = true)
		{
			lock (Block.SimData.hashSetLock)
			{
				Block.SimData.UnsettledChunks.Add(Id);
			}

			if (alsoNeighbours)
			{
				for (int i = 0; i < Voxel.kNeighbourCount; i++)
				{
					GetNeighbour((Neighbour)i)?.Unsettle(false);
				}
			}
		}

		private Chunk GetNeighbour(Neighbour neighbour)
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
			RenderData.Dispose();
		}
	}
}