using System;
using System.Collections;
using System.Collections.Generic;
using TerrainEngine.Fluid.New;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Generates triangle mesh for a given voxel grid using Marching Cubes algorithm on CPU or GPU.
/// </summary>
public partial class MarchingCubesMeshGenerator : MonoBehaviour, IDisposable
{
	[Range(Voxel.kEpsilon * Voxel.kByteToFloat, Voxel.kMaxVolume * Voxel.kByteToFloat)]
	public float IsoLevel = 0.5f;

	public Material TerrainMaterial;

	public bool GpuFluidRendering = false;
	public ComputeShader MarchingCubesShader;

	private float _lastIsoLevel;
	private NativeArray<Voxel> _borderedChunk;
	private WorldApi _worldApi;

	public void Initialize()
	{
		_worldApi = GetComponent<WorldApi>();

		_borderedChunk = new NativeArray<Voxel>(Chunk.kTotalVoxelsInBordered, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

		_lastIsoLevel = IsoLevel;

		InitializeCpuRendering();

		if (GpuFluidRendering)
		{
			InitializeGpuRendering();
		}
	}

	public bool CheckIsoLevel()
	{
		// isoLevel changed
		if (_lastIsoLevel != IsoLevel)
		{
			_lastIsoLevel = IsoLevel;

			if (GpuFluidRendering)
			{
				for (int chunkId = 0; chunkId < _chunksToRender.Count; chunkId++)
				{
					InitializeCommandBuffers(_chunksToRender[chunkId]);
				}
			}

			return true;
		}

		return false;
	}

	/// <summary>
	/// Update solid or fluid mesh of a chunk.
	/// </summary>
	public void UpdateMesh(Chunk chunk, bool solid)
	{
		EnsureRenderData(chunk, solid);

		if (solid)
		{
			GenerateMeshCPU(chunk, true);
		}
		else
		{
			if (GpuFluidRendering)
			{
				GenerateMeshGPU(chunk);
			}
			else
			{
				GenerateMeshCPU(chunk, false);
			}

			chunk.RenderData.FluidNeedsRebuild = false;
		}
	}

	/// <summary>
	/// Ensures and manages the data required for rendering on the fly.
	/// </summary>
	private void EnsureRenderData(Chunk chunk, bool solid)
	{
		// fill borderedChunk with the latest values to use for rendering
		_worldApi.GetBorderedChunk(chunk, ref _borderedChunk);

		if (solid)
		{
			chunk.RenderData.CheckTerrain();
		}
		else
		{
			bool hasAnyFluid = chunk.RenderData.CheckFluid(GpuFluidRendering);

			if (GpuFluidRendering)
			{
				// stop rendering fluid in this chunk
				if (!hasAnyFluid && _chunksToRender.Contains(chunk))
				{
					_chunksToRender.Remove(chunk);
				}

				// start rendering fluid in this chunk
				if (hasAnyFluid && !_chunksToRender.Contains(chunk))
				{
					_chunksToRender.Add(chunk);
				}
			}
		}
	}
}
