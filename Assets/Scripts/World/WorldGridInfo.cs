using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// World grid constants and coordinate system conversion.
/// Taken from Ylands.
/// </summary>
public static class WorldGridInfo
{
	/// <summary>
	/// Size of chunk in voxels.
	/// </summary>
	public const int kVoxelsPerChunk = 8;
	public const int kVoxelsPerChunkLog2 = 3;

	/// <summary>
	/// Size of block in chunks.
	/// </summary>
	public const int kChunksPerBlock = 2;
	public const int kChunksPerBlockLog2 = 1;

	/// <summary>
	/// Size of block in voxels.
	/// </summary>
	public const int kVoxelsPerBlock = kVoxelsPerChunk * kChunksPerBlock;
	public const int kChunkCount = kChunksPerBlock * kChunksPerBlock * kChunksPerBlock;

	/// <summary>
	/// Total number of voxels in a single chunk.
	/// </summary>
	public const int kTotalVoxelsInChunk = kVoxelsPerChunk * kVoxelsPerChunk * kVoxelsPerChunk;

	/// <summary>
	/// Total number of chunks in a single block.
	/// </summary>
	public const int kTotalChunksInBlock = kChunksPerBlock * kChunksPerBlock * kChunksPerBlock;

	/// <summary>
	/// Total number of voxels in a single block.
	/// </summary>
	public const int kTotalVoxelsInBlock = kVoxelsPerBlock * kVoxelsPerBlock * kVoxelsPerBlock;

	/// <summary>
	/// Double precision versions of selected non-integral constants.
	/// </summary>
	public class HighPrecision
	{
		public const double kVoxelSize = 1;
		public const double kOneOverVoxelSize = 1 / kVoxelSize;
		public const double kBlockSize = kVoxelsPerBlock * kVoxelSize;
		public const double kChunkSize = kVoxelsPerChunk * kVoxelSize;
	}

	/// <summary>
	/// Size of voxel cube in meters.
	/// </summary>
	public const float kVoxelSize = (float)HighPrecision.kVoxelSize;
	public const float kOneOverVoxelSize = (float)HighPrecision.kOneOverVoxelSize;
	public const float VoxelSizeConst = kVoxelSize;

	/// <summary>
	/// Size of block cube in meters.
	/// </summary>
	public const float kBlockSize = (float)HighPrecision.kBlockSize;

	/// <summary>
	/// Size of chunk cube in meters.
	/// </summary>
	public const float kChunkSize = (float)HighPrecision.kChunkSize;

	public static Vector3I GetVoxelsPerChunkV3()
	{
		return new Vector3I(kVoxelsPerChunk, kVoxelsPerChunk, kVoxelsPerChunk);
	}

	public static Vector3I GetChunksPerBlockV3()
	{
		return new Vector3I(kChunksPerBlock, kChunksPerBlock, kChunksPerBlock);
	}

	public static Vector3I GetVoxelsPerBlockV3()
	{
		return new Vector3I(kVoxelsPerBlock, kVoxelsPerBlock, kVoxelsPerBlock);
	}

	/// <summary>
	/// World space block origin from block coordinates.
	/// </summary>
	public static Vector3 BlockToWorld(Vector3I blockCoordinates)
	{
		return new Vector3(blockCoordinates.x * kBlockSize, blockCoordinates.y * kBlockSize, blockCoordinates.z * kBlockSize);
	}

	/// <summary>
	/// Coordinates of the block a worldPosition falls into.
	/// </summary>
	public static Vector3I WorldToBlock(Vector3 worldPosition)
	{
		Vector3I BlockPosition = new Vector3I();
		const double kOneOverBlockSizeHp = 1 / HighPrecision.kBlockSize;
		BlockPosition.x = (int)Math.Floor(worldPosition.x * kOneOverBlockSizeHp);
		BlockPosition.y = (int)Math.Floor(worldPosition.y * kOneOverBlockSizeHp);
		BlockPosition.z = (int)Math.Floor(worldPosition.z * kOneOverBlockSizeHp);
		return BlockPosition;
	}

	/// <summary>
	/// World position to global coordinate of the voxel it belongs to.
	/// </summary>
	public static Vector3I WorldToVoxel(Vector3 worldPosition)
	{
		//double helps but does not eliminate precision problems
		int x = (int)Math.Floor(worldPosition.x * HighPrecision.kOneOverVoxelSize);
		int y = (int)Math.Floor(worldPosition.y * HighPrecision.kOneOverVoxelSize);
		int z = (int)Math.Floor(worldPosition.z * HighPrecision.kOneOverVoxelSize);
		return new Vector3I(x, y, z);
	}

	/// <summary>
	/// Global voxel coordinate to world position of origin of the voxel.
	/// </summary>
	public static Vector3 VoxelToWorld(Vector3I voxel)
	{
		//double helps but does not eliminate precision problems
		float x = (float)(voxel.x * HighPrecision.kVoxelSize);
		float y = (float)(voxel.y * HighPrecision.kVoxelSize);
		float z = (float)(voxel.z * HighPrecision.kVoxelSize);
		return new Vector3(x, y, z);
	}

	/// <summary>
	/// World position to block-local voxel coordinate.
	/// </summary>
	public static Vector3I WorldToBlockVoxel(Vector3 WorldPosition)
	{
		Vector3I voxel = new Vector3I();
		double x = (((double)WorldPosition.x % HighPrecision.kBlockSize) * HighPrecision.kOneOverVoxelSize);
		double y = (((double)WorldPosition.y % HighPrecision.kBlockSize) * HighPrecision.kOneOverVoxelSize);
		double z = (((double)WorldPosition.z % HighPrecision.kBlockSize) * HighPrecision.kOneOverVoxelSize);

		if (x < 0)
		{
			voxel.x = (int)x - 1 + kVoxelsPerBlock;
		}
		else
		{
			voxel.x = (int)x;
		}

		if (y < 0)
		{
			voxel.y = (int)y - 1 + kVoxelsPerBlock;
		}
		else
		{
			voxel.y = (int)y;
		}

		if (z < 0)
		{
			voxel.z = (int)z - 1 + kVoxelsPerBlock;
		}
		else
		{
			voxel.z = (int)z;
		}
		return voxel;
	}

	/// <summary>
	/// Block-local chunk coordinates from block-local voxel coordinates.
	/// </summary>
	public static Vector3I BlockVoxelToBlockChunk(int vx, int vy, int vz)
	{
		return new Vector3I(vx >> kVoxelsPerChunkLog2, vy >> kVoxelsPerChunkLog2, vz >> kVoxelsPerChunkLog2);
	}

	/// <summary>
	/// Chunk-local voxel coordinates from block-local voxel coordinates.
	/// </summary>
	public static Vector3I BlockVoxelToChunkVoxel(int vx, int vy, int vz)
	{
		return new Vector3I(vx & (kVoxelsPerChunk - 1), vy & (kVoxelsPerChunk - 1), vz & (kVoxelsPerChunk - 1));
	}

	/// <summary>
	/// Block-local chunkId from block-local chunk coordinates.
	/// </summary>
	public static int BlockChunkToChunkId(int cx, int cy, int cz)
	{
		return cz | (cy << kChunksPerBlockLog2) | (cx << (kChunksPerBlockLog2 * 2));
	}

	/// <summary>
	/// Block-local voxel to block-local chunkId.
	/// </summary>
	public static int BlockVoxelToChunkId(int vx, int vy, int vz)
	{
		return (vz >> kVoxelsPerChunkLog2) | ((vy >> kVoxelsPerChunkLog2) << kChunksPerBlockLog2) | ((vx >> kVoxelsPerChunkLog2) << (kChunksPerBlockLog2 * 2));
	}

	/// <summary>
	/// Chunk-local voxel coordinate to chunk-local voxelId
	/// </summary>
	public static int ChunkVoxelToVoxelId(int vx, int vy, int vz)
	{
		return vz | (vy << kVoxelsPerChunkLog2) | (vx << (kVoxelsPerChunkLog2 * 2));
	}

	/// <summary>
	/// Block-local voxel coordinate to chunk-local voxelId.
	/// </summary>
	public static int BlockVoxelToVoxelId(int vx, int vy, int vz)
	{
		return (vz & (kVoxelsPerChunk - 1)) | ((vy & (kVoxelsPerChunk - 1)) << kVoxelsPerChunkLog2) | ((vx & (kVoxelsPerChunk - 1)) << (kVoxelsPerChunkLog2 * 2));
	}

	/// <summary>
	/// Chunk-local voxel coordinate from chunk-local voxel id.
	/// </summary>
	public static Vector3I VoxelIdToChunkVoxel(int voxelId)
	{
		return new Vector3I((voxelId >> kVoxelsPerChunkLog2 * 2) & (kVoxelsPerChunk - 1), (voxelId >> kVoxelsPerChunkLog2) & (kVoxelsPerChunk - 1), voxelId & (kVoxelsPerChunk - 1));
	}

	/// <summary>
	/// World space origin of the voxel the worldPosition belongs to.
	/// </summary>
	public static Vector3 VoxelOrigin(Vector3 worldPosition)
	{
		worldPosition /= VoxelSizeConst;
		worldPosition.x = Mathf.Floor(worldPosition.x);
		worldPosition.y = Mathf.Floor(worldPosition.y);
		worldPosition.z = Mathf.Floor(worldPosition.z);
		worldPosition *= VoxelSizeConst;
		return worldPosition;
	}

	/// <summary>
	/// World space origin of the block the worldPosition belongs to.
	/// </summary>
	public static Vector3 BlockOrigin(Vector3 worldPosition)
	{
		Vector3 rv = worldPosition * (1 / kBlockSize);
		rv.x = Mathf.Floor(rv.x);
		rv.y = Mathf.Floor(rv.y);
		rv.z = Mathf.Floor(rv.z);
		return rv * kBlockSize;
	}

	/// <summary>
	/// World space bounds of a block.
	/// </summary>
	public static Bounds BlockBounds(Vector3I block)
	{
		Vector3 size = new Vector3(kBlockSize, kBlockSize, kBlockSize);
		return new Bounds(BlockToWorld(block) + size * 0.5f, size);
	}

	/// <summary>
	/// World space origin of the chunk the worldPosition belongs to.
	/// </summary>
	public static Vector3 ChunkOrigin(Vector3 worldPosition)
	{
		Vector3 rv = worldPosition * (1 / kChunkSize);
		rv.x = Mathf.Floor(rv.x);
		rv.y = Mathf.Floor(rv.y);
		rv.z = Mathf.Floor(rv.z);
		return rv * kChunkSize;
	}

	/// <summary>
	/// Clamp block-local coordinates to [0, kVoxelsPerBlock) range and adjust block.
	/// </summary>
	public static Vector3I ClampBlockVoxel(Vector3I block, ref Vector3I voxel)
	{
		int vx = voxel.x;
		int vy = voxel.y;
		int vz = voxel.z;

		block = ClampBlockVoxel(block, ref vx, ref vy, ref vz);
		voxel.x = vx;
		voxel.y = vy;
		voxel.z = vz;
		return block;
	}

	/// <summary>
	/// Clamp block-local coordinates to [0, kVoxelsPerBlock) range and adjust block.
	/// </summary>
	public static Vector3I ClampBlockVoxel(Vector3I block, ref int vx, ref int vy, ref int vz)
	{
		while (vx < 0)
		{
			vx += kVoxelsPerBlock;
			--block.x;
		}
		while (vx >= kVoxelsPerBlock)
		{
			vx -= kVoxelsPerBlock;
			++block.x;
		}
		while (vy < 0)
		{
			vy += kVoxelsPerBlock;
			--block.y;
		}
		while (vy >= kVoxelsPerBlock)
		{
			vy -= kVoxelsPerBlock;
			++block.y;
		}
		while (vz < 0)
		{
			vz += kVoxelsPerBlock;
			--block.z;
		}
		while (vz >= kVoxelsPerBlock)
		{
			vz -= kVoxelsPerBlock;
			++block.z;
		}

		return block;
	}

	/// <summary>
	/// Clamp block-local voxel coordinates into range [0, kVoxelsPerBlocks) and return the offset in blocks used.
	/// </summary>
	/// <remarks>
	/// Works values that are less than kVoxelPerBlock distant from range.
	/// </remarks>
	public static bool ClampBlockVoxel(ref int vx, ref int vy, ref int vz, out Vector3I offset)
	{
		bool rv = false;
		offset = default(Vector3I);
		if (vx >= WorldGridInfo.kVoxelsPerBlock)
		{
			vx -= WorldGridInfo.kVoxelsPerBlock;
			offset.x = 1;
			rv = true;
		}
		else if (vx < 0)
		{
			vx += WorldGridInfo.kVoxelsPerBlock;
			offset.x = -1;
			rv = true;
		}

		if (vy >= WorldGridInfo.kVoxelsPerBlock)
		{
			offset.y = 1;
			vy -= WorldGridInfo.kVoxelsPerBlock;
			rv = true;
		}
		else if (vy < 0)
		{
			offset.y = -1;
			vy += WorldGridInfo.kVoxelsPerBlock;
			rv = true;
		}

		if (vz >= WorldGridInfo.kVoxelsPerBlock)
		{
			offset.z = 1;
			vz -= WorldGridInfo.kVoxelsPerBlock;
			rv = true;
		}
		else if (vz < 0)
		{
			offset.z = -1;
			vz += WorldGridInfo.kVoxelsPerBlock;
			rv = true;
		}

		return rv;
	}

	/// <summary>
	/// Clamp block-local subvoxel coordinates to [0, kVoxelsPerBlock * 2) range and adjust block.
	/// </summary>
	/// <remarks>
	/// Subvoxel is one eight the size of voxel (2x2x2 subvoxels is a single voxel).
	/// </remarks>
	public static Vector3I ClampBlockSubVoxel(Vector3I block, ref int vx, ref int vy, ref int vz)
	{
		while (vx < 0)
		{
			vx += kVoxelsPerBlock * 2;
			--block.x;
		}
		while (vx >= kVoxelsPerBlock * 2)
		{
			vx -= kVoxelsPerBlock * 2;
			++block.x;
		}

		while (vy < 0)
		{
			vy += kVoxelsPerBlock * 2;
			--block.y;
		}
		while (vy >= kVoxelsPerBlock * 2)
		{
			vy -= kVoxelsPerBlock * 2;
			++block.y;
		}

		while (vz < 0)
		{
			vz += kVoxelsPerBlock * 2;
			--block.z;
		}
		while (vz >= kVoxelsPerBlock * 2)
		{
			vz -= kVoxelsPerBlock * 2;
			++block.z;
		}

		return block;
	}

	/// <summary>
	/// & with chunk-local voxel id to get coordinate of order-2 supervoxel (2 voxels per axis) id 
	/// </summary>
	public const int kSuperVoxel2IdMask = 0xEEE;

	/// <summary>
	/// Get 2supervoxel id from chunk-local voxel id.
	/// </summary>
	public static int Super2VoxelIdFromVoxelId(int voxelId)
	{
		//chunk local voxel id is (low bits from right to left)
		//xxxx|yyyy|zzzz where x/y/z is one bit of coordinate in respective axis
		//to quantize the voxel id into supervoxel, simply zero out the LSB of each coordinate
		//1110|1110|1110
		return voxelId & kSuperVoxel4IdMask;
	}

	/// <summary>
	/// & with chunk-local voxel id to get coordinate of order-4 supervoxel (4 voxels per axis) id 
	/// </summary>
	public const int kSuperVoxel4IdMask = 0xCCC;

	/// <summary>
	/// Get 4supervoxel id from chunk-local voxel id.
	/// </summary>
	public static int Super4VoxelIdFromVoxelId(int voxelId)
	{
		//chunk local voxel id is (low bits from right to left)
		//xxxx|yyyy|zzzz where x/y/z is one bit of coordinate in respective axis
		//to quantize the voxel id into supervoxel, simply zero out the LSB of each coordinate
		//1100|1100|1100


		return voxelId & kSuperVoxel4IdMask;
	}
}
