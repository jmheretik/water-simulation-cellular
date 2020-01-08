using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour
{
    public const int Row = WorldGridInfo.kChunksPerBlock;
    public const int Column = WorldGridInfo.kChunksPerBlock * WorldGridInfo.kChunksPerBlock;
    public const int Offset = 1 - WorldGridInfo.kChunksPerBlock;

    public int id;

    public Vector3I pos;

    public Block top;
    public Block bottom;
    public Block forward;
    public Block backward;
    public Block right;
    public Block left;

    /// <summary>
    /// Array of chunks of voxels. Contains previous generation of voxels in cellular automata and should be used read-only.
    /// </summary>
    public Voxel[][] voxels;

    /// <summary>
    /// Array of chunks of voxels. Contains current generation of voxels in cellular automata and should be used for writing to.
    /// </summary>
    public Voxel[][] writeVoxels;

    public BitArray[] visited;

    public Chunk[] chunks;

    /// <summary>
    /// Initialize array of voxels and chunks.
    /// </summary>
    public void Initialize(World world, int id, ref Vector3I pos)
    {
        this.transform.parent = world.transform;
        this.id = id;
        this.pos = pos;

        voxels = new Voxel[WorldGridInfo.kTotalChunksInBlock][];
        writeVoxels = new Voxel[WorldGridInfo.kTotalChunksInBlock][];
        visited = new BitArray[WorldGridInfo.kTotalChunksInBlock];

        chunks = new Chunk[WorldGridInfo.kTotalChunksInBlock];

        for (int chunkX = 0; chunkX < WorldGridInfo.kChunksPerBlock; chunkX++)
        {
            for (int chunkY = 0; chunkY < WorldGridInfo.kChunksPerBlock; chunkY++)
            {
                for (int chunkZ = 0; chunkZ < WorldGridInfo.kChunksPerBlock; chunkZ++)
                {
                    int chunkId = chunkX * WorldGridInfo.kChunksPerBlock * WorldGridInfo.kChunksPerBlock + chunkY * WorldGridInfo.kChunksPerBlock + chunkZ;

                    Chunk chunk = new GameObject("Chunk " + chunkId).AddComponent<Chunk>();
                    chunks[chunkId] = chunk;
                    chunk.Initialize(this, chunkId);

                    voxels[chunkId] = new Voxel[WorldGridInfo.kTotalVoxelsInChunk];
                    writeVoxels[chunkId] = new Voxel[WorldGridInfo.kTotalVoxelsInChunk];
                    visited[chunkId] = new BitArray(WorldGridInfo.kTotalVoxelsInChunk);

                    for (int voxelX = 0; voxelX < WorldGridInfo.kVoxelsPerChunk; voxelX++)
                    {
                        for (int voxelY = 0; voxelY < WorldGridInfo.kVoxelsPerChunk; voxelY++)
                        {
                            for (int voxelZ = 0; voxelZ < WorldGridInfo.kVoxelsPerChunk; voxelZ++)
                            {
                                int voxelId = voxelX * WorldGridInfo.kVoxelsPerChunk * WorldGridInfo.kVoxelsPerChunk + voxelY * WorldGridInfo.kVoxelsPerChunk + voxelZ;

                                voxels[chunkId][voxelId].valid = true;
                                writeVoxels[chunkId][voxelId].valid = true;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generates terrain in this block's chunks.
    /// Set up a random generator to be consistent along chunks and blocks and respect the user inputted seed if generating random terrain.
    /// </summary>
    public void GenerateTerrain(TerrainGenerator terrainGenerator, float fluidFlowRadius)
    {
        System.Random randomGenerator = null;

        for (int chunkId = 0; chunkId < chunks.Length; chunkId++)
        {
            Vector3I indices = new Vector3I(id, chunkId, 0);

            if (terrainGenerator.shape == TerrainShape.Random)
            {
                randomGenerator = new System.Random(terrainGenerator.randomSeed.GetHashCode() + indices.GetHashCode());
            }

            for (int voxelId = 0; voxelId < writeVoxels[chunkId].Length; voxelId++)
            {
                indices.z = voxelId;

                terrainGenerator.GenerateTerrain(ref indices, ref writeVoxels[chunkId][voxelId], fluidFlowRadius, randomGenerator);
            }
        }
    }

    /// <summary>
    /// Smooths out terrain in this block's chunks.
    /// </summary>
    public void SmoothTerrain(TerrainGenerator terrainGenerator)
    {
        Vector3I indices;

        for (int chunkId = 0; chunkId < chunks.Length; chunkId++)
        {
            for (int voxelId = 0; voxelId < writeVoxels[chunkId].Length; voxelId++)
            {
                indices.x = id;
                indices.y = chunkId;
                indices.z = voxelId;

                terrainGenerator.SmoothTerrain(ref indices, ref writeVoxels[chunkId][voxelId]);
            }
        }
    }

    /// <summary>
    /// Copies voxel values from write to read array either for specific chunk or all unsettled chunks.
    /// </summary>
    public void UpdateUnsettledValues()
    {
        for (int chunkId = 0; chunkId < chunks.Length; chunkId++)
        {
            if (!chunks[chunkId].settled)
            {
                UpdateValues(chunkId);
            }
        }
    }

    public void UpdateUnsettledMeshes(MarchingCubesMeshGenerator meshGenerator, bool solid, bool lava)
    {
        for (int chunkId = 0; chunkId < chunks.Length; chunkId++)
        {
            if (!chunks[chunkId].settled)
            {
                UpdateMesh(meshGenerator, chunkId, solid, lava);
            }
        }
    }

    public void UpdateAllMeshes(MarchingCubesMeshGenerator meshGenerator, bool solid, bool lava)
    {
        for (int chunkId = 0; chunkId < chunks.Length; chunkId++)
        {
            UpdateMesh(meshGenerator, chunkId, solid, lava);
        }
    }

    /// <summary>
    /// Settles chunks which have all their voxels settled.
    /// </summary>
    public void TrySettle(int chunkId)
    {
        chunks[chunkId].hasFluid = false;
        chunks[chunkId].settled = true;

        for (int voxelId = 0; voxelId < voxels[chunkId].Length; voxelId++)
        {
            if (voxels[chunkId][voxelId].HasFluid)
            {
                chunks[chunkId].hasFluid = true;
            }

            if (!voxels[chunkId][voxelId].settled)
            {
                chunks[chunkId].settled = false;
            }
        }
    }

    public void UpdateValues(int chunkId)
    {
        Array.Copy(writeVoxels[chunkId], voxels[chunkId], WorldGridInfo.kTotalVoxelsInChunk);
    }

    public void UpdateMesh(MarchingCubesMeshGenerator meshGenerator, int chunkId, bool solid, bool lava)
    {
        if (meshGenerator.gpuFluidRendering && !solid)
        {
            meshGenerator.GenerateMeshGPU(chunks[chunkId], lava);
            return;
        }

        GameObject meshGo = chunks[chunkId].transform.GetChild(solid ? 0 : lava ? 1 : 2).gameObject;

        meshGo.GetComponent<MeshFilter>().mesh = meshGenerator.GenerateMeshCPU(chunks[chunkId], solid, lava);
        meshGo.GetComponent<MeshRenderer>().sharedMaterial = solid ? meshGenerator.terrainMaterial : lava ? meshGenerator.lavaMaterial : meshGenerator.waterMaterial;

        if (solid)
        {
            meshGo.GetComponent<MeshCollider>().sharedMesh = meshGo.GetComponent<MeshFilter>().mesh;
        }
    }

    public void InitializeChunkReferences()
    {
        for (int i = 0; i < chunks.Length; i++)
        {
            Chunk chunk = chunks[i];

            if (i + 1 < WorldGridInfo.kTotalChunksInBlock && (i + 1) / Block.Row == i / Block.Row)
                chunk.forward = chunks[i + 1];
            else
                chunk.forward = chunk.block.forward == null ? null : chunk.block.forward.chunks[i + 1 * Block.Offset];

            if (i + Block.Row < WorldGridInfo.kTotalChunksInBlock && (i + Block.Row) / Block.Column == i / Block.Column)
                chunk.top = chunks[i + Block.Row];
            else
                chunk.top = chunk.block.top == null ? null : chunk.block.top.chunks[i + Block.Row * Block.Offset];

            if (i + Block.Column < WorldGridInfo.kTotalChunksInBlock)
                chunk.right = chunks[i + Block.Column];
            else
                chunk.right = chunk.block.right == null ? null : chunk.block.right.chunks[i + Block.Column * Block.Offset];

            if (i - 1 >= 0 && (i - 1) / Block.Row == i / Block.Row)
                chunk.backward = chunks[i - 1];
            else
                chunk.backward = chunk.block.backward == null ? null : chunk.block.backward.chunks[i - 1 * Block.Offset];

            if (i - Block.Row >= 0 && (i - Block.Row) / Block.Column == i / Block.Column)
                chunk.bottom = chunks[i - Block.Row];
            else
                chunk.bottom = chunk.block.bottom == null ? null : chunk.block.bottom.chunks[i - Block.Row * Block.Offset];

            if (i - Block.Column >= 0)
                chunk.left = chunks[i - Block.Column];
            else
                chunk.left = chunk.block.left == null ? null : chunk.block.left.chunks[i - Block.Column * Block.Offset];
        }
    }
}
