using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Initializes the world and contains methods to update it.
/// </summary>
public class World : MonoBehaviour
{
    public static readonly Vector3 BlockSize = new Vector3(WorldGridInfo.kBlockSize, WorldGridInfo.kBlockSize, WorldGridInfo.kBlockSize);
    public static readonly Vector3 ChunkSize = new Vector3(WorldGridInfo.kChunkSize, WorldGridInfo.kChunkSize, WorldGridInfo.kChunkSize);
    public static readonly Vector3 VoxelSize = new Vector3(WorldGridInfo.kVoxelSize, WorldGridInfo.kVoxelSize, WorldGridInfo.kVoxelSize);

    [Header("Debug grids")]
    public bool debugBlockGrid = false;
    public bool debugBlockLabels = false;
    public bool debugChunkGrid = false;
    public bool debugVoxelGrid = false;
    public bool debugVoxelLabels = false;

    [Header("World generation")]
    public int sizeX = 1;
    public int sizeY = 1;
    public int sizeZ = 1;

    public Block[] blocks;

    [HideInInspector]
    public bool terrainLoaded = false;

    private TerrainGenerator terrainGenerator;
    private FluidProcessor fluidProcessor;
    private MarchingCubesMeshGenerator meshGenerator;

    void Awake()
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        gameManager.world = this;

        sizeX = gameManager.worldSizeX;
        sizeY = gameManager.worldSizeY;
        sizeZ = gameManager.worldSizeZ;

        terrainGenerator = gameManager.terrainGenerator;
        fluidProcessor = gameManager.fluidProcessor;
        meshGenerator = gameManager.meshGenerator;
    }

    void Start()
    {
        // initialize
        terrainGenerator.Initialize(this);
        fluidProcessor.Initialize(this);
        meshGenerator.Initialize(this);

        // initialize arrays and generate terrain
        Initialize();
        LoadTerrain();
        UpdateMeshes(true);
        TrySettleChunks();
        terrainLoaded = true;
    }

    /// <summary>
    /// Initializes array of blocks and the neighbour references in blocks and chunks.
    /// </summary>
    public void Initialize()
    {
        blocks = new Block[sizeX * sizeY * sizeZ];

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    int blockId = x * sizeZ * sizeY + y * sizeZ + z;
                    Vector3I pos = new Vector3I(x, y, z);

                    Block block = new GameObject("Block" + pos).AddComponent<Block>();
                    blocks[blockId] = block;
                    block.Initialize(this, blockId, ref pos);
                }
            }
        }

        InitializeBlockReferences();

        for (int blockId = 0; blockId < blocks.Length; blockId++)
        {
            blocks[blockId].InitializeChunkReferences();
        }
    }

    /// <summary>
    /// Generates terrain for the whole world.
    /// </summary>
    public void LoadTerrain()
    {
        for (int blockId = 0; blockId < blocks.Length; blockId++)
        {
            blocks[blockId].GenerateTerrain(terrainGenerator, fluidProcessor.flowRadius);
        }

        for (int blockId = 0; blockId < blocks.Length; blockId++)
        {
            for (int chunkId = 0; chunkId < blocks[blockId].chunks.Length; chunkId++)
            {
                blocks[blockId].UpdateValues(chunkId);
            }
        }

        // smooth out the terrain if randomly generated
        if (terrainGenerator.shape == TerrainShape.Random)
        {
            for (int step = 0; step < terrainGenerator.smoothSteps; step++)
            {
                for (int blockId = 0; blockId < blocks.Length; blockId++)
                {
                    blocks[blockId].SmoothTerrain(terrainGenerator);
                }

                for (int blockId = 0; blockId < blocks.Length; blockId++)
                {
                    for (int chunkId = 0; chunkId < blocks[blockId].chunks.Length; chunkId++)
                    {
                        blocks[blockId].UpdateValues(chunkId);
                    }
                }
            }
        }
    }

    #region world update methods

    /// <summary>
    /// Marks chunk as unsettled so that its mesh and values written to its voxels get updated.
    /// </summary>
    public void UnsettleChunk(ref Vector3I indices)
    {
        if (indices.valid && blocks[indices.x].chunks[indices.y].settled)
        {
            blocks[indices.x].chunks[indices.y].settled = false;
        }
    }

    public void UnsettleChunkAndVoxel(ref Vector3I indices)
    {
        if (!indices.valid)
            return;

        blocks[indices.x].writeVoxels[indices.y][indices.z].Unsettle();

        if (blocks[indices.x].chunks[indices.y].settled)
        {
            blocks[indices.x].chunks[indices.y].settled = false;
        }
    }

    /// <summary>
    /// Tries to settle chunks which haven't been written to for a long time and have all their voxels settled.
    /// </summary>
    public void TrySettleChunks()
    {
        for (int blockId = 0; blockId < blocks.Length; blockId++)
        {
            for (int chunkId = 0; chunkId < blocks[blockId].chunks.Length; chunkId++)
            {
                if (!blocks[blockId].chunks[chunkId].settled)
                {
                    blocks[blockId].TrySettle(chunkId);

                    // just settled
                    if (blocks[blockId].chunks[chunkId].settled && !debugVoxelGrid)
                    {
                        blocks[blockId].UpdateMesh(meshGenerator, chunkId, false, false);
                        blocks[blockId].UpdateMesh(meshGenerator, chunkId, false, true);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates values written to unsettled chunks.
    /// </summary>
    public void UpdateValues()
    {
        for (int blockId = 0; blockId < blocks.Length; blockId++)
        {
            blocks[blockId].UpdateUnsettledValues();
        }
    }

    /// <summary>
    /// Updates solid or fluid meshes of unsettled chunks.
    /// </summary>
    public void UpdateMeshes(bool solid, bool lava = false)
    {
        if (solid || !debugVoxelGrid)
        {
            for (int blockId = 0; blockId < blocks.Length; blockId++)
            {
                blocks[blockId].UpdateUnsettledMeshes(meshGenerator, solid, lava);
            }
        }
    }

    /// <summary>
    /// Forces update of all meshes.
    /// </summary>
    public void UpdateAllMeshes()
    {
        if (!debugVoxelGrid)
        {
            for (int blockId = 0; blockId < blocks.Length; blockId++)
            {
                blocks[blockId].UpdateAllMeshes(meshGenerator, false, false);
                blocks[blockId].UpdateAllMeshes(meshGenerator, false, true);
            }
        }
    }

    /// <summary>
    /// Marks all voxels as not visited.
    /// </summary>
    public void Unvisit()
    {
        for (int blockId = 0; blockId < blocks.Length; blockId++)
        {
            for (int chunkId = 0; chunkId < blocks[blockId].chunks.Length; chunkId++)
            {
                blocks[blockId].visited[chunkId].SetAll(false);
            }
        }
    }

    #endregion

    #region world getters

    /// <summary>
    /// Given world position of a voxel returns its indices.
    /// </summary>
    public bool GetVoxel(ref Vector3 worldPosition, out Vector3I indices)
    {
        indices.x = indices.y = indices.z = -1;

        if (IsBorder(ref worldPosition))
            return false;

        Vector3I blockPos = WorldGridInfo.WorldToBlock(worldPosition);
        indices.x = BlockToBlockId(ref blockPos);

        if (indices.x < 0)
            return false;

        Vector3I blockVoxelPos = WorldGridInfo.WorldToBlockVoxel(worldPosition);
        indices.y = WorldGridInfo.BlockVoxelToChunkId(blockVoxelPos.x, blockVoxelPos.y, blockVoxelPos.z);
        indices.z = WorldGridInfo.BlockVoxelToVoxelId(blockVoxelPos.x, blockVoxelPos.y, blockVoxelPos.z);

        return true;
    }

    /// <summary>
    /// Given indices of a voxel return its neighbour indices.
    /// Neighbour indices are being calculated everytime we an access rather than permanently stored per-voxel.
    /// </summary>
    public bool GetNeighbour(ref Vector3I indices, Neighbour neighbour, out Vector3I neighbourIndices)
    {
        int i = indices.z;
        Chunk chunk = blocks[indices.x].chunks[indices.y];

        neighbourIndices.x = -1;
        neighbourIndices.y = indices.y;
        neighbourIndices.z = indices.z;

        if (neighbour == Neighbour.Forward)
        {
            if (i + 1 < WorldGridInfo.kTotalVoxelsInChunk && (i + 1) / Chunk.Row == i / Chunk.Row)
            {
                neighbourIndices.x = indices.x;
                neighbourIndices.z += 1;
            }
            else if (chunk.forward != null)
            {
                neighbourIndices.x = chunk.forward.block.id;
                neighbourIndices.y = chunk.forward.id;
                neighbourIndices.z += 1 * Chunk.Offset;
            }
        }
        else if (neighbour == Neighbour.Top)
        {
            if (i + Chunk.Row < WorldGridInfo.kTotalVoxelsInChunk && (i + Chunk.Row) / Chunk.Column == i / Chunk.Column)
            {
                neighbourIndices.x = indices.x;
                neighbourIndices.z += Chunk.Row;
            }
            else if (chunk.top != null)
            {
                neighbourIndices.x = chunk.top.block.id;
                neighbourIndices.y = chunk.top.id;
                neighbourIndices.z += Chunk.Row * Chunk.Offset;
            }
        }
        else if (neighbour == Neighbour.Right)
        {
            if (i + Chunk.Column < WorldGridInfo.kTotalVoxelsInChunk)
            {
                neighbourIndices.x = indices.x;
                neighbourIndices.z += Chunk.Column;
            }
            else if (chunk.right != null)
            {
                neighbourIndices.x = chunk.right.block.id;
                neighbourIndices.y = chunk.right.id;
                neighbourIndices.z += Chunk.Column * Chunk.Offset;
            }
        }
        else if (neighbour == Neighbour.Backward)
        {
            if (i - 1 >= 0 && (i - 1) / Chunk.Row == i / Chunk.Row)
            {
                neighbourIndices.x = indices.x;
                neighbourIndices.z -= 1;
            }
            else if (chunk.backward != null)
            {
                neighbourIndices.x = chunk.backward.block.id;
                neighbourIndices.y = chunk.backward.id;
                neighbourIndices.z -= 1 * Chunk.Offset;
            }
        }
        else if (neighbour == Neighbour.Bottom)
        {
            if (i - Chunk.Row >= 0 && (i - Chunk.Row) / Chunk.Column == i / Chunk.Column)
            {
                neighbourIndices.x = indices.x;
                neighbourIndices.z -= Chunk.Row;
            }
            else if (chunk.bottom != null)
            {
                neighbourIndices.x = chunk.bottom.block.id;
                neighbourIndices.y = chunk.bottom.id;
                neighbourIndices.z -= Chunk.Row * Chunk.Offset;
            }
        }
        else if (neighbour == Neighbour.Left)
        {
            if (i - Chunk.Column >= 0)
            {
                neighbourIndices.x = indices.x;
                neighbourIndices.z -= Chunk.Column;
            }
            else if (chunk.left != null)
            {
                neighbourIndices.x = chunk.left.block.id;
                neighbourIndices.y = chunk.left.id;
                neighbourIndices.z -= Chunk.Column * Chunk.Offset;
            }
        }

        return neighbourIndices.x >= 0;
    }

    public bool IsBorder(ref Vector3 worldPosition)
    {
        return worldPosition.x < WorldGridInfo.kVoxelSize || worldPosition.x >= GetWidth() || worldPosition.y < WorldGridInfo.kVoxelSize || worldPosition.y >= GetHeight() || worldPosition.z < WorldGridInfo.kVoxelSize || worldPosition.z >= GetDepth();
    }

    public float GetWidth()
    {
        return WorldGridInfo.kBlockSize * sizeX - WorldGridInfo.kVoxelSize;
    }

    public float GetHeight()
    {
        return WorldGridInfo.kBlockSize * sizeY - WorldGridInfo.kVoxelSize;
    }

    public float GetDepth()
    {
        return WorldGridInfo.kBlockSize * sizeZ - WorldGridInfo.kVoxelSize;
    }

    /// <summary>
    /// Returns world position of block's origin from its indices.
    /// </summary>
    public void GetBlockWorldPos(ref Vector3I indices, out Vector3 worldPosition)
    {
        worldPosition.x = blocks[indices.x].pos.x * WorldGridInfo.kBlockSize;
        worldPosition.y = blocks[indices.x].pos.y * WorldGridInfo.kBlockSize;
        worldPosition.z = blocks[indices.x].pos.z * WorldGridInfo.kBlockSize;
    }

    /// <summary>
    /// Returns world position of chunk's origin from its indices.
    /// </summary>
    public void GetChunkWorldPos(ref Vector3I indices, out Vector3 worldPosition)
    {
        GetBlockWorldPos(ref indices, out worldPosition);

        int localZ = indices.y & (WorldGridInfo.kChunksPerBlock - 1);
        int localY = (indices.y >> WorldGridInfo.kChunksPerBlockLog2) & (WorldGridInfo.kChunksPerBlock - 1);
        int localX = (indices.y >> (WorldGridInfo.kChunksPerBlockLog2 * 2)) & (WorldGridInfo.kChunksPerBlock - 1);

        worldPosition.x += localX * WorldGridInfo.kChunkSize;
        worldPosition.y += localY * WorldGridInfo.kChunkSize;
        worldPosition.z += localZ * WorldGridInfo.kChunkSize;
    }

    /// <summary>
    /// Returns world position of voxel's origin from its indices.
    /// </summary>
    public void GetVoxelWorldPos(ref Vector3I indices, out Vector3 worldPosition)
    {
        GetChunkWorldPos(ref indices, out worldPosition);

        int localZ = indices.z & (WorldGridInfo.kVoxelsPerChunk - 1);
        int localY = (indices.z >> WorldGridInfo.kVoxelsPerChunkLog2) & (WorldGridInfo.kVoxelsPerChunk - 1);
        int localX = (indices.z >> (WorldGridInfo.kVoxelsPerChunkLog2 * 2)) & (WorldGridInfo.kVoxelsPerChunk - 1);

        worldPosition.x += localX * WorldGridInfo.kVoxelSize;
        worldPosition.y += localY * WorldGridInfo.kVoxelSize;
        worldPosition.z += localZ * WorldGridInfo.kVoxelSize;
    }

    public int GetVoxelWorldPosY(ref Vector3I indices)
    {
        int voxelLocalY = (indices.z >> WorldGridInfo.kVoxelsPerChunkLog2) & (WorldGridInfo.kVoxelsPerChunk - 1);
        int chunkLocalY = (indices.y >> WorldGridInfo.kChunksPerBlockLog2) & (WorldGridInfo.kChunksPerBlock - 1);

        return (int)(blocks[indices.x].pos.y * WorldGridInfo.kBlockSize + chunkLocalY * WorldGridInfo.kChunkSize + voxelLocalY * WorldGridInfo.kVoxelSize);
    }

    #endregion

    #region private methods

    /// <summary>
    /// Returns block's index from its position.
    /// </summary>
    private int BlockToBlockId(ref Vector3I blockPosition)
    {
        for (int blockId = 0; blockId < blocks.Length; blockId++)
        {
            if (blocks[blockId].pos.Equals(blockPosition))
            {
                return blockId;
            }
        }

        return -1;
    }

    private void InitializeBlockReferences()
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            Block block = blocks[i];

            int row = sizeZ;
            int column = sizeY * sizeZ;

            if (i + 1 < blocks.Length && (i + 1) / row == i / row)
                block.forward = blocks[i + 1];

            if (i + row < blocks.Length && (i + row) / column == i / column)
                block.top = blocks[i + row];

            if (i + column < blocks.Length)
                block.right = blocks[i + column];

            if (i - 1 >= 0 && (i - 1) / row == i / row)
                block.backward = blocks[i - 1];

            if (i - row >= 0 && (i - row) / column == i / column)
                block.bottom = blocks[i - row];

            if (i - column >= 0)
                block.left = blocks[i - column];
        }
    }

    #endregion

#if UNITY_EDITOR

    void OnDrawGizmos()
    {
        if (blocks != null)
        {
            Vector3I indices = new Vector3I();
            Vector3 worldPosition;

            for (int blockId = 0; blockId < blocks.Length; blockId++)
            {
                indices.x = blockId;

                if (debugBlockGrid)
                {
                    GetBlockWorldPos(ref indices, out worldPosition);
                    fluidProcessor.DrawDebugCube(new Color(1f, 0f, 0f, 0.3f), worldPosition, World.BlockSize, Voxel.MaxVolume, debugBlockLabels, blocks[blockId].pos.ToString());
                }

                for (int chunkId = 0; chunkId < blocks[blockId].chunks.Length; chunkId++)
                {
                    indices.y = chunkId;

                    if (debugChunkGrid)
                    {
                        GetChunkWorldPos(ref indices, out worldPosition);
                        fluidProcessor.DrawDebugCube(new Color(0f, 1f, 0f, 0.1f), worldPosition, World.ChunkSize);
                    }

                    if (debugVoxelGrid)
                    {
                        for (int voxelId = 0; voxelId < blocks[blockId].voxels[chunkId].Length; voxelId++)
                        {
                            indices.z = voxelId;

                            Voxel voxel = blocks[blockId].voxels[chunkId][voxelId];

                            // draw only voxels with fluid
                            if (voxel.HasFluid)
                            {
                                GetVoxelWorldPos(ref indices, out worldPosition);
                                fluidProcessor.DrawDebugCube(new Color(0f, 0f, 1f, 0.05f), worldPosition, World.VoxelSize, voxel.RenderFluid, debugVoxelLabels, voxel.fluid.ToString());
                            }
                        }
                    }
                }
            }
        }
    }

#endif

}
