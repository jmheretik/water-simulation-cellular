using System;
using System.Collections;
using System.Collections.Generic;
using TerrainEngine;
using TerrainEngine.Fluid.New;
using UnityEngine;

/// <summary>
/// Options for terrain shapes to generate.
/// </summary>
public enum TerrainShape
{
	Ground,
	Downhill,
	Ubend,
	Random
}

/// <summary>
/// Handles terrain generation and modification.
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
	[Header("Terrain generation")]
	public TerrainShape Shape = TerrainShape.Ground;
	public string RandomSeed;
	[Range(0, 100)]
	public int RandomFillPercent = 50;
	public int SmoothSteps = 3;
	public bool DebugFillWorldWithWater = false;

	[Header("Terrain modification")]
	[Range(0, Voxel.kMaxVolume)]
	public byte TerrainValue = 20;
	public int TerrainRadius = 2;

	private WorldApi _worldApi;
	private FluidProcessor _fluidProcessor;

	public void Initialize()
	{
		_worldApi = GetComponent<WorldApi>();
		_fluidProcessor = GetComponent<FluidProcessor>();
	}

	#region modification

	/// <summary>
	/// Adds or removes terrain at its surface.
	/// </summary>
	public void ModifyTerrain(Vector3 point, bool add)
	{
		for (float x = -TerrainRadius; x <= TerrainRadius; x = x + WorldGridInfo.kVoxelSize)
		{
			for (float z = -TerrainRadius; z <= TerrainRadius; z = z + WorldGridInfo.kVoxelSize)
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
					if ((add && voxelCopy.Solid < Voxel.kMaxVolume) || (!add && voxelCopy.Solid > 0))
					{
						ModifyVoxel(ref voxelCopy, in indices, add, false);
						break;
					}

					worldPosition.y += add ? WorldGridInfo.kVoxelSize : -WorldGridInfo.kVoxelSize;
				}
			}
		}
	}

	/// <summary>
	/// Removes all the terrain around a given point.
	/// </summary>
	public void RemoveTerrain(Vector3 point)
	{
		for (float y = -TerrainRadius * WorldGridInfo.kVoxelSize; y <= TerrainRadius * WorldGridInfo.kVoxelSize; y += WorldGridInfo.kVoxelSize)
		{
			for (float x = -TerrainRadius * WorldGridInfo.kVoxelSize; x <= TerrainRadius * WorldGridInfo.kVoxelSize; x += WorldGridInfo.kVoxelSize)
			{
				for (float z = -TerrainRadius * WorldGridInfo.kVoxelSize; z <= TerrainRadius * WorldGridInfo.kVoxelSize; z += WorldGridInfo.kVoxelSize)
				{
					Vector3 worldPosition = new Vector3(point.x + x, point.y + y, point.z + z);

					Voxel voxelCopy = _worldApi.TryGetVoxel(in worldPosition, out VectorI3 indices);

					if (!voxelCopy.Valid)
						continue;

					ModifyVoxel(ref voxelCopy, in indices, false, true);
				}
			}
		}
	}

	/// <summary>
	/// Modifies terrain value in a voxel.
	/// </summary>
	private void ModifyVoxel(ref Voxel voxelCopy, in VectorI3 indices, bool add, bool remove)
	{
		// adjust value
		voxelCopy.Solid = (byte)(remove ? 0 : Mathf.Clamp(voxelCopy.Solid + (add ? TerrainValue : -TerrainValue), 0, Voxel.kMaxVolume));

		// terrain modification may split or connect components
		_fluidProcessor.ComponentManager.GetComponent(in indices)?.MarkForRebuild();

		// unsettle voxel and write the new value
		voxelCopy.Unsettle(TerrainValue);
		_worldApi.UnsettleChunk(in indices);
		_worldApi.SetVoxelAfterSim(in indices, in voxelCopy);
	}

	#endregion

	#region generation

	/// <summary>
	/// Generates terrain in this block's chunks.
	/// </summary>
	public void LoadTerrain(Block block)
	{
		System.Random randomGenerator = null;

		for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
		{
			VectorI3 indices = new VectorI3(block.Id, chunkId, 0);

			if (Shape == TerrainShape.Random)
			{
				// ensure consistency along chunks and blocks and respect the user inputted seed
				randomGenerator = new System.Random(RandomSeed.GetHashCode() + indices.GetHashCode());
			}

			for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
			{
				indices.z = voxelId;

				block.Voxels.GetWritable(chunkId, voxelId).Solid = GenerateTerrain(in indices, randomGenerator);
			}
		}
	}

	/// <summary>
	/// Returns average smoothed terrain value for a voxel depending on the amount of solid in its neighbours.
	/// </summary>
	public byte SmoothTerrain(in VectorI3 indices)
	{
		_worldApi.GetVoxelWorldPos(in indices, out Vector3 voxelWorldPos);

		if (_worldApi.IsBorder(in voxelWorldPos))
			return Voxel.kMaxVolume;

		return (byte)(GetSolidNeighboursCount(in voxelWorldPos) > 13 * Voxel.kMaxVolume ? Voxel.kMaxVolume : 0);
	}

	/// <summary>
	/// Return a terrain value for a voxel according to user inputted parameters and chosen TerrainShape.
	/// </summary>
	private byte GenerateTerrain(in VectorI3 indices, System.Random randomGenerator)
	{
		_worldApi.GetVoxelWorldPos(in indices, out Vector3 voxelWorldPos);

		// solid border
		if (_worldApi.IsBorder(in voxelWorldPos))
			return Voxel.kMaxVolume;

		switch (Shape)
		{
			case TerrainShape.Downhill:
				return (byte)(voxelWorldPos.y < voxelWorldPos.z ? Voxel.kMaxVolume : 0);

			case TerrainShape.Ground:
				return (byte)(voxelWorldPos.y == WorldGridInfo.kVoxelSize ? Voxel.kMaxVolume : 0);

			case TerrainShape.Random:
				return (byte)(randomGenerator.Next(0, 100) < RandomFillPercent ? Voxel.kMaxVolume : 0);

			case TerrainShape.Ubend:
				return (byte)(voxelWorldPos.y == WorldGridInfo.kVoxelSize ||
					(
						voxelWorldPos.y > 3 * WorldGridInfo.kVoxelSize &&
						voxelWorldPos.x > (_worldApi.GetWidth() + WorldGridInfo.kVoxelSize) * 0.5f - _fluidProcessor.FlowRadius &&
						voxelWorldPos.x < (_worldApi.GetWidth() + WorldGridInfo.kVoxelSize) * 0.5f + _fluidProcessor.FlowRadius
					)
					? Voxel.kMaxVolume : 0);
		}

		return 0;
	}

	/// <summary>
	/// Returns amount of solid in voxels around given world position.
	/// </summary>
	private int GetSolidNeighboursCount(in Vector3 voxelWorldPos)
	{
		int count = 0;

		// 3x3x3 neighbourhood
		for (float x = -WorldGridInfo.kVoxelSize; x <= WorldGridInfo.kVoxelSize; x += WorldGridInfo.kVoxelSize)
		{
			for (float y = -WorldGridInfo.kVoxelSize; y <= WorldGridInfo.kVoxelSize; y += WorldGridInfo.kVoxelSize)
			{
				for (float z = -WorldGridInfo.kVoxelSize; z <= WorldGridInfo.kVoxelSize; z += WorldGridInfo.kVoxelSize)
				{
					// skip self
					if (x == voxelWorldPos.x && y == voxelWorldPos.y && z == voxelWorldPos.z)
						continue;

					Vector3 neighbourWorldPos = new Vector3(voxelWorldPos.x + x, voxelWorldPos.y + y, voxelWorldPos.z + z);

					count += _worldApi.TryGetVoxel(in neighbourWorldPos, out _).Solid;
				}
			}
		}

		return count;
	}

	#endregion

}
