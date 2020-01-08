using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Represents the game world subdivided into Blocks, Chunks and Voxels.
	/// </summary>
	public class World : MonoBehaviour, IDisposable
	{
		private WorldApi _worldApi;

		private Block[] _blocks;

		/// <summary>
		/// Initializes array of blocks and the neighbour references in blocks and chunks.
		/// </summary>
		public void Initialize()
		{
			_worldApi = GetComponent<WorldApi>();

			// initialize arrays
			_blocks = new Block[_worldApi.SizeX * _worldApi.SizeY * _worldApi.SizeZ];

			_worldApi.Initialize(_blocks);

			for (int blockX = 0; blockX < _worldApi.SizeX; blockX++)
			{
				for (int blockY = 0; blockY < _worldApi.SizeY; blockY++)
				{
					for (int blockZ = 0; blockZ < _worldApi.SizeZ; blockZ++)
					{
						int blockId = blockX * _worldApi.SizeZ * _worldApi.SizeY + blockY * _worldApi.SizeZ + blockZ;

						VectorI3 coords = new VectorI3(blockX, blockY, blockZ);

						Block block = new GameObject($"Block ({blockX}, {blockY}, {blockZ})").AddComponent<Block>();
						_blocks[blockId] = block;

						block.Initialize(blockId, transform, _worldApi, coords);
					}
				}
			}

			InitializeBlockReferences();

			for (int blockId = 0; blockId < _blocks.Length; blockId++)
			{
				_blocks[blockId].InitializeChunkReferences();
			}
		}

		/// <summary>
		/// Loads terrain for all the blocks in the world.
		/// </summary>
		public void LoadTerrain(TerrainGenerator terrainGenerator)
		{
			for (int blockId = 0; blockId < _blocks.Length; blockId++)
			{
				terrainGenerator.LoadTerrain(_blocks[blockId]);
			}

			// smooth out the terrain if randomly generated
			if (terrainGenerator.Shape == TerrainShape.Random && terrainGenerator.SmoothSteps > 0)
			{
				VectorI3 indices;

				byte[][][] solidToWrite = GetTemporaryAllVoxelPropertyArray();

				// fill out the array with solid data of all voxels
				for (int blockId = 0; blockId < _blocks.Length; blockId++)
					for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
						for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
							solidToWrite[blockId][chunkId][voxelId] = _blocks[blockId].Voxels.Get(chunkId, voxelId).Solid;

				for (int step = 0; step < terrainGenerator.SmoothSteps; step++)
				{
					// single iteration of smoothing in all voxels
					for (indices.x = 0; indices.x < _blocks.Length; indices.x++)
						for (indices.y = 0; indices.y < WorldGridInfo.kTotalChunksInBlock; indices.y++)
							for (indices.z = 0; indices.z < WorldGridInfo.kTotalVoxelsInChunk; indices.z++)
								solidToWrite[indices.x][indices.y][indices.z] = terrainGenerator.SmoothTerrain(in indices);

					// copy new solid data to actual voxels
					for (int blockId = 0; blockId < _blocks.Length; blockId++)
						for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
							for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
								_blocks[blockId].Voxels.GetWritable(chunkId, voxelId).Solid = solidToWrite[blockId][chunkId][voxelId];
				}
			}

			// fill the whole world with water
			if (terrainGenerator.DebugFillWorldWithWater)
			{
				VectorI3 indices;

				for (indices.x = 0; indices.x < _blocks.Length; indices.x++)
				{
					for (indices.y = 0; indices.y < WorldGridInfo.kTotalChunksInBlock; indices.y++)
					{
						for (indices.z = 0; indices.z < WorldGridInfo.kTotalVoxelsInChunk; indices.z++)
						{
							if (_worldApi.GetVoxelWorldPosY(in indices) < _worldApi.GetHeight() - WorldGridInfo.kVoxelSize)
							{
								ref Voxel writeVoxel = ref _blocks[indices.x].Voxels.GetWritable(indices.y, indices.z);

								writeVoxel.Fluid = writeVoxel.FreeVolume;
								writeVoxel.Viscosity = (byte)Viscosity.Water;
								writeVoxel.Unsettle(writeVoxel.Fluid);
								_blocks[indices.x].Chunks[indices.y].Unsettle();
							}
						}
					}
				}
			}
		}

		private void InitializeBlockReferences()
		{
			int row = _worldApi.SizeZ;
			int column = _worldApi.SizeY * _worldApi.SizeZ;
			int total = _worldApi.SizeX * _worldApi.SizeY * _worldApi.SizeZ;

			for (int blockId = 0; blockId < _blocks.Length; blockId++)
			{
				Block block = _blocks[blockId];

				block.Forward = !WorldGridInfoHelper.IsNeighbourAtBorderSlow(blockId, Neighbour.Forward, row, column, total) ? _blocks[blockId + 1] : null;
				block.Backward = !WorldGridInfoHelper.IsNeighbourAtBorderSlow(blockId, Neighbour.Backward, row, column, total) ? _blocks[blockId - 1] : null;

				block.Top = !WorldGridInfoHelper.IsNeighbourAtBorderSlow(blockId, Neighbour.Top, row, column, total) ? _blocks[blockId + row] : null;
				block.Bottom = !WorldGridInfoHelper.IsNeighbourAtBorderSlow(blockId, Neighbour.Bottom, row, column, total) ? _blocks[blockId - row] : null;

				block.Right = !WorldGridInfoHelper.IsNeighbourAtBorderSlow(blockId, Neighbour.Right, row, column, total) ? _blocks[blockId + column] : null;
				block.Left = !WorldGridInfoHelper.IsNeighbourAtBorderSlow(blockId, Neighbour.Left, row, column, total) ? _blocks[blockId - column] : null;
			}
		}

		/// <summary>
		/// Returns a byte array of the size and indexation as all voxels. Suitable as a tmp array for storing a single property of each voxel.
		/// </summary>
		private byte[][][] GetTemporaryAllVoxelPropertyArray()
		{
			byte[][][] array = new byte[_blocks.Length][][];

			for (int blockId = 0; blockId < _blocks.Length; blockId++)
			{
				for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
				{
					for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
					{
						if (array[blockId] == null)
						{
							array[blockId] = new byte[WorldGridInfo.kTotalChunksInBlock][];
						}

						if (array[blockId][chunkId] == null)
						{
							array[blockId][chunkId] = new byte[WorldGridInfo.kTotalVoxelsInChunk];
						}
					}
				}
			}

			return array;
		}

		public void Dispose()
		{
			for (int i = 0; i < _blocks.Length; i++)
			{
				_blocks[i].Dispose();
			}
		}

	}
}