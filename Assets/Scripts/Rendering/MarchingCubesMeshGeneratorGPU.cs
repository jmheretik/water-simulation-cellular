#pragma warning disable 0162

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marching Cubes algorithm on GPU using Compute Shaders.
/// Implementation details inspired by: https://github.com/pavelkouril/unity-marching-cubes-gpu
/// </summary>
public partial class MarchingCubesMeshGenerator : MonoBehaviour
{
    public ComputeShader marchingCubesShader;
    public bool gpuFluidRendering = true;

    private int marchingCubesKernel;
    private int tripleCountKernel;
    private ComputeBuffer argBuffer;

    private Material lavaGpuMaterial;
    private Material waterGpuMaterial;
    private ComputeBuffer lavaTriangleBuffer;
    private ComputeBuffer waterTriangleBuffer;

    private float[] chunkData;
    private List<Chunk> chunksToRender;
    private bool gpuRenderingInitialized = false;

    void InitializeGpuRendering()
    {
        if (WorldGridInfo.kVoxelsPerChunk % 8 != 0)
        {
            Debug.LogError("Chunk dimensions have to be divisible by 8!");
            return;
        }

        // shaders
        marchingCubesKernel = marchingCubesShader.FindKernel("MarchingCubes");
        tripleCountKernel = marchingCubesShader.FindKernel("TripleCount");

        // arrays
        int[] args = new int[] { 0, 1, 0, 0 };
        chunkData = new float[(WorldGridInfo.kVoxelsPerChunk + 2) * (WorldGridInfo.kVoxelsPerChunk + 2) * (WorldGridInfo.kVoxelsPerChunk + 2)];

        // buffers
        argBuffer = new ComputeBuffer(args.Length, sizeof(int), ComputeBufferType.IndirectArguments);
        waterTriangleBuffer = new ComputeBuffer(WorldGridInfo.kTotalVoxelsInChunk * 5, sizeof(float) * 18, ComputeBufferType.Append);
        lavaTriangleBuffer = new ComputeBuffer(WorldGridInfo.kTotalVoxelsInChunk * 5, sizeof(float) * 18, ComputeBufferType.Append);

        // materials
        waterGpuMaterial = new Material(Shader.Find("Procedural Geometry/Marching Cubes/Transparent"));
        waterGpuMaterial.SetColor("_color", waterMaterial.color);
        waterGpuMaterial.SetBuffer("triangleBuffer", waterTriangleBuffer);
        lavaGpuMaterial = new Material(Shader.Find("Procedural Geometry/Marching Cubes/Opaque"));
        lavaGpuMaterial.SetColor("_color", lavaMaterial.color);
        lavaGpuMaterial.SetBuffer("triangleBuffer", lavaTriangleBuffer);

        // send params to gpu
        marchingCubesShader.SetInt("_width", WorldGridInfo.kVoxelsPerChunk + 2);
        marchingCubesShader.SetInt("_height", WorldGridInfo.kVoxelsPerChunk + 2);
        marchingCubesShader.SetInt("_depth", WorldGridInfo.kVoxelsPerChunk + 2);
        argBuffer.SetData(args);

        chunksToRender = new List<Chunk>();
    }

    /// <summary>
    /// Generates mesh on GPU.
    /// Use for fluid mesh generation, which needs high performance rendering.
    /// Not suitable for solid (terrain) mesh since the generated mesh stays on GPU and can't be assigned to a MeshCollider.
    /// </summary>
    public void GenerateMeshGPU(Chunk chunk, bool lava)
    {
        // cleanup gpu render data
        if (!chunk.hasFluid)
        {
            if (chunksToRender.Contains(chunk))
            {
                chunk.waterBuffer.Release();
                chunk.lavaBuffer.Release();
                chunk.waterCommandBuffer.Release();
                chunk.lavaCommandBuffer.Release();

                chunksToRender.Remove(chunk);
            }

            return;
        }

        // initialize gpu render data
        if (!chunksToRender.Contains(chunk))
        {
            chunk.lavaBuffer = new ComputeBuffer(chunkData.Length, sizeof(float), ComputeBufferType.Default);
            chunk.waterBuffer = new ComputeBuffer(chunkData.Length, sizeof(float), ComputeBufferType.Default);
            chunk.lavaCommandBuffer = new CommandBuffer();
            chunk.waterCommandBuffer = new CommandBuffer();

            chunksToRender.Add(chunk);

            ReinitializeCommandBuffers(chunk);
        }

        UpdateFluidDataOnGpu(chunk, lava);
    }

    /// <summary>
    /// Reinitialize command buffers either for all chunks that are being rendered or just for a specific chunk.
    /// </summary>
    private void ReinitializeCommandBuffers(Chunk specificChunk = null)
    {
        foreach (Chunk chunk in chunksToRender)
        {
            if (specificChunk == null || (specificChunk != null && chunk == specificChunk))
            {
                Vector3 chunkWorldPos;
                Vector3I indices = new Vector3I(chunk.block.id, chunk.id, 0);
                world.GetChunkWorldPos(ref indices, out chunkWorldPos);

                SetupCommandBuffer(chunk.lavaCommandBuffer, chunk.lavaBuffer, chunkWorldPos, true);
                SetupCommandBuffer(chunk.waterCommandBuffer, chunk.waterBuffer, chunkWorldPos, false);
            }
        }
    }

    private void SetupCommandBuffer(CommandBuffer commandBuffer, ComputeBuffer computeBuffer, Vector3 chunkWorldPos, bool lava)
    {
        Material material = lava ? lavaGpuMaterial : waterGpuMaterial;
        ComputeBuffer triangleBuffer = lava ? lavaTriangleBuffer : waterTriangleBuffer;
        Matrix4x4 chunkWorldMatrix = transform.localToWorldMatrix * Matrix4x4.Translate(chunkWorldPos);

        commandBuffer.Clear();

        // send params to gpu
        commandBuffer.SetComputeFloatParam(marchingCubesShader, "_isoLevel", isoLevel);
        commandBuffer.SetComputeVectorParam(marchingCubesShader, "_chunkWorldPos", chunkWorldPos);

        // bind buffers
        commandBuffer.SetComputeBufferParam(marchingCubesShader, marchingCubesKernel, "triangleBuffer", triangleBuffer);
        commandBuffer.SetComputeBufferParam(marchingCubesShader, marchingCubesKernel, "_dataBuffer", computeBuffer);

        // generate triangles
        commandBuffer.DispatchCompute(marchingCubesShader, marchingCubesKernel, WorldGridInfo.kVoxelsPerChunk / 8, WorldGridInfo.kVoxelsPerChunk / 8, WorldGridInfo.kVoxelsPerChunk / 8);

        // compute number of generated triangles
        commandBuffer.CopyCounterValue(triangleBuffer, argBuffer, 0);
        commandBuffer.SetComputeBufferParam(marchingCubesShader, tripleCountKernel, "argBuffer", argBuffer);
        commandBuffer.DispatchCompute(marchingCubesShader, tripleCountKernel, 1, 1, 1);

        // draw triangles
        commandBuffer.DrawProceduralIndirect(chunkWorldMatrix, material, 0, MeshTopology.Triangles, argBuffer);
    }

    /// <summary>
    /// Fills chunkData array with data from chunk and a border of 1 voxel filled from neighbouring chunks.
    /// </summary>
    private void UpdateFluidDataOnGpu(Chunk chunk, bool lava)
    {
        int i = 0;
        Vector3I nIndices, currIndices, currColumnIndices, currRowIndices;
        Vector3I firstVoxelIndices = new Vector3I(chunk.block.id, chunk.id, 0);

        SetFirstVoxel(ref firstVoxelIndices, ref i);

        currIndices = currColumnIndices = currRowIndices = firstVoxelIndices;

        // fill the array
        while (i < chunkData.Length)
        {
            if (currIndices.valid)
            {
                Voxel voxel = world.blocks[currIndices.x].voxels[currIndices.y][currIndices.z];

                if (lava)
                {
                    chunkData[i++] = voxel.viscosity == (byte)FlowViscosity.Lava ? voxel.RenderFluid : 0;
                }
                else
                {
                    chunkData[i++] = voxel.viscosity != (byte)FlowViscosity.Lava ? voxel.RenderFluid : 0;
                }
            }
            else
            {
                chunkData[i++] = 0;
            }

            // advance 1 voxel forward
            if (world.GetNeighbour(ref currIndices, Neighbour.Forward, out nIndices))
                currIndices = nIndices;

            // advance 1 row upwards
            if (i % (WorldGridInfo.kVoxelsPerChunk + 2) == 0)
            {
                if (world.GetNeighbour(ref currRowIndices, Neighbour.Top, out nIndices))
                    currIndices = currRowIndices = nIndices;
            }

            // advance 1 column right
            if (i % ((WorldGridInfo.kVoxelsPerChunk + 2) * (WorldGridInfo.kVoxelsPerChunk + 2)) == 0)
            {
                if (world.GetNeighbour(ref currColumnIndices, Neighbour.Right, out nIndices))
                    currIndices = currColumnIndices = currRowIndices = nIndices;
            }
        }

        if (lava)
        {
            chunk.lavaBuffer.SetData(chunkData);
        }
        else
        {
            chunk.waterBuffer.SetData(chunkData);
        }
    }

    /// <summary>
    /// Set back first voxel 1 left, 1 bottom and 1 back or skip those voxels if not possible.
    /// </summary>
    private void SetFirstVoxel(ref Vector3I firstVoxelIndices, ref int i)
    {
        Vector3I nIndices;

        // move 1 column left or skip it
        if (world.GetNeighbour(ref firstVoxelIndices, Neighbour.Left, out nIndices))
        {
            firstVoxelIndices = nIndices;
        }
        else
        {
            for (int j = 0; j < (WorldGridInfo.kVoxelsPerChunk + 2) * (WorldGridInfo.kVoxelsPerChunk + 2); j++)
                chunkData[i++] = 0;
        }

        // move 1 row down or skip it
        if (world.GetNeighbour(ref firstVoxelIndices, Neighbour.Bottom, out nIndices))
        {
            firstVoxelIndices = nIndices;
        }
        else
        {
            for (int j = 0; j < WorldGridInfo.kVoxelsPerChunk + 2; j++)
                chunkData[i++] = 0;
        }

        // move 1 voxel back or skip it
        if (world.GetNeighbour(ref firstVoxelIndices, Neighbour.Backward, out nIndices))
        {
            firstVoxelIndices = nIndices;
        }
        else
        {
            chunkData[i++] = 0;
        }
    }

    private void OnRenderObject()
    {
        if (!gpuRenderingInitialized)
            return;

        // execute command buffers for chunks with fluid (draw opaque lava first)
        foreach (Chunk chunk in chunksToRender)
        {
            lavaTriangleBuffer.SetCounterValue(0);
            Graphics.ExecuteCommandBuffer(chunk.lavaCommandBuffer);
        }

        foreach (Chunk chunk in chunksToRender)
        {
            waterTriangleBuffer.SetCounterValue(0);
            Graphics.ExecuteCommandBuffer(chunk.waterCommandBuffer);
        }
    }

    private void OnDestroy()
    {
        if (!gpuRenderingInitialized)
            return;

        // cleanup
        foreach (Chunk chunk in chunksToRender)
        {
            chunk.waterBuffer.Release();
            chunk.lavaBuffer.Release();
            chunk.lavaCommandBuffer.Release();
            chunk.waterCommandBuffer.Release();
        }

        argBuffer.Release();
        waterTriangleBuffer.Release();
        lavaTriangleBuffer.Release();
    }
}
