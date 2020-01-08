#pragma warning disable 0162

using System;
using System.Collections;
using System.Collections.Generic;
using TerrainEngine;
using TerrainEngine.Fluid.New;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marching Cubes algorithm on GPU using Compute Shaders.
/// Implementation details inspired by: https://github.com/pavelkouril/unity-marching-cubes-gpu
/// </summary>
public partial class MarchingCubesMeshGenerator : MonoBehaviour, IDisposable
{
	private int _marchingCubesKernel;
	private int _tripleCountKernel;
	private ComputeBuffer _argBuffer;

	/// <summary>
	/// Reusable data required for drawing mesh on GPU for each fluid type.
	/// </summary>
	private Dictionary<Viscosity, (Material material, ComputeBuffer triangleBuffer)> _sharedRenderData;
	private float[] _borderedChunkData;

	/// <summary>
	/// List of chunks currently being rendered.
	/// </summary>
	private List<Chunk> _chunksToRender;

	private void InitializeGpuRendering()
	{
		if (WorldGridInfo.kVoxelsPerChunk % 8 != 0)
		{
			Debug.LogError("Chunk dimensions have to be divisible by 8!");
			return;
		}

		// shaders
		_marchingCubesKernel = MarchingCubesShader.FindKernel("MarchingCubes");
		_tripleCountKernel = MarchingCubesShader.FindKernel("TripleCount");

		// arrays
		int[] args = new int[] { 0, 1, 0, 0 };
		_borderedChunkData = new float[Chunk.kTotalVoxelsInBordered];

		// buffers and materials
		_argBuffer = new ComputeBuffer(args.Length, sizeof(int), ComputeBufferType.IndirectArguments);
		_sharedRenderData = new Dictionary<Viscosity, (Material, ComputeBuffer)>(FluidProcessor.Types.Count);

		foreach (Viscosity viscosity in FluidProcessor.Types.Keys)
		{
			// triangle buffer
			ComputeBuffer buffer = new ComputeBuffer(WorldGridInfo.kTotalVoxelsInChunk * 5, sizeof(float) * 18, ComputeBufferType.Append);

			// shader
			Material material = new Material(FluidProcessor.Types[viscosity].shader);
			material.SetFloat("_voxelSize", WorldGridInfo.kVoxelSize);
			material.SetColor("_color", FluidProcessor.Types[viscosity].material.color);
			material.SetBuffer("triangleBuffer", buffer);

			_sharedRenderData.Add(viscosity, (material, buffer));
		}

		// send params to gpu
		MarchingCubesShader.SetInt("_width", WorldGridInfo.kVoxelsPerChunk + 2);
		MarchingCubesShader.SetInt("_height", WorldGridInfo.kVoxelsPerChunk + 2);
		MarchingCubesShader.SetInt("_depth", WorldGridInfo.kVoxelsPerChunk + 2);
		_argBuffer.SetData(args);

		_chunksToRender = new List<Chunk>();
	}

	/// <summary>
	/// Generates mesh on GPU - updates data in chunk's computeBuffer and initializes its commandBuffer if needed.
	/// Use for fluid mesh generation, which needs high performance rendering.
	/// Not suitable for solid (terrain) mesh since the generated mesh stays on GPU and can't be assigned to a MeshCollider.
	/// </summary>
	public void GenerateMeshGPU(Chunk chunk)
	{
		foreach (var renderData in chunk.RenderData.Fluid)
		{
			Viscosity viscosity = renderData.Key;
			CommandBuffer chunkCommandBuffer = (CommandBuffer)renderData.Value.data[0];
			ComputeBuffer chunkComputeBuffer = (ComputeBuffer)renderData.Value.data[1];

			for (int i = 0; i < Chunk.kTotalVoxelsInBordered; i++)
			{
				if (_borderedChunk[i].Valid && _borderedChunk[i].Viscosity == (byte)viscosity)
				{
					_borderedChunkData[i] = _borderedChunk[i].Fluid * Voxel.kByteToFloat;
				}
				else
				{
					_borderedChunkData[i] = 0;
				}
			}

			chunkComputeBuffer.SetData(_borderedChunkData);

			if (chunkCommandBuffer.sizeInBytes == 0)
			{
				InitializeCommandBuffers(chunk);
			}
		}
	}

	/// <summary>
	/// Initializes command buffers for each fluid type in a given chunk.
	/// </summary>
	private void InitializeCommandBuffers(Chunk chunk)
	{
		Matrix4x4 chunkWorldMatrix = transform.localToWorldMatrix * Matrix4x4.Translate(chunk.WorldPos);

		foreach (var renderData in chunk.RenderData.Fluid)
		{
			Viscosity viscosity = renderData.Key;
			CommandBuffer chunkCommandBuffer = (CommandBuffer)renderData.Value.data[0];
			ComputeBuffer chunkComputeBuffer = (ComputeBuffer)renderData.Value.data[1];
			ComputeBuffer gpuTriangleBuffer = _sharedRenderData[viscosity].triangleBuffer;
			Material gpuMaterial = _sharedRenderData[viscosity].material;

			chunkCommandBuffer.Clear();

			// send params to gpu
			chunkCommandBuffer.SetComputeFloatParam(MarchingCubesShader, "_isoLevel", IsoLevel * (byte)viscosity * Voxel.kByteMaxValueToFloat);

			// bind buffers
			chunkCommandBuffer.SetComputeBufferParam(MarchingCubesShader, _marchingCubesKernel, "triangleBuffer", gpuTriangleBuffer);
			chunkCommandBuffer.SetComputeBufferParam(MarchingCubesShader, _marchingCubesKernel, "_dataBuffer", chunkComputeBuffer);

			// generate triangles
			chunkCommandBuffer.DispatchCompute(MarchingCubesShader, _marchingCubesKernel, WorldGridInfo.kVoxelsPerChunk / 8, WorldGridInfo.kVoxelsPerChunk / 8, WorldGridInfo.kVoxelsPerChunk / 8);

			// compute number of generated triangles
			chunkCommandBuffer.CopyCounterValue(gpuTriangleBuffer, _argBuffer, 0);
			chunkCommandBuffer.SetComputeBufferParam(MarchingCubesShader, _tripleCountKernel, "argBuffer", _argBuffer);
			chunkCommandBuffer.DispatchCompute(MarchingCubesShader, _tripleCountKernel, 1, 1, 1);

			// draw triangles
			chunkCommandBuffer.DrawProceduralIndirect(chunkWorldMatrix, gpuMaterial, 0, MeshTopology.Triangles, _argBuffer);
		}
	}

	/// <summary>
	/// Execute command buffers for chunks with fluid.
	/// </summary>
	private void OnRenderObject()
	{
		if (_worldApi == null || !GpuFluidRendering)
			return;

		// draw opaque fluids in chunk first, then transparent
		RenderFluidInChunks(RenderQueue.Geometry);
		RenderFluidInChunks(RenderQueue.Transparent);
	}

	private void RenderFluidInChunks(RenderQueue renderQueue)
	{
		for (int chunkId = 0; chunkId < _chunksToRender.Count; chunkId++)
		{
			foreach (var renderData in _chunksToRender[chunkId].RenderData.Fluid)
			{
				Viscosity viscosity = renderData.Key;

				if (_sharedRenderData[viscosity].material.renderQueue == (int)renderQueue)
				{
					_sharedRenderData[viscosity].triangleBuffer.SetCounterValue(0);
					Graphics.ExecuteCommandBuffer((CommandBuffer)renderData.Value.data[0]);
				}
			}
		}
	}

	public void Dispose()
	{
		_borderedChunk.Dispose();

		if (GpuFluidRendering)
		{
			_argBuffer.Release();

			foreach (var renderData in _sharedRenderData)
			{
				renderData.Value.triangleBuffer.Release();
			}
		}
	}
}
