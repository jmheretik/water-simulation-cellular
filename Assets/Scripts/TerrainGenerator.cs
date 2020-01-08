using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public TerrainShape shape = TerrainShape.Ground;
    public string randomSeed;
    [Range(0, 100)]
    public int randomFillPercent = 50;
    public int smoothSteps = 3;

    [Header("Terrain modification")]
    [Range(byte.MinValue, Voxel.MaxVolume)]
    public byte terrainValue = 20;
    public int terrainRadius = 2;

    private World world;

    public void Initialize(World world)
    {
        this.world = world;
    }

    #region terrain generation

    /// <summary>
    /// Generates terrain in a voxel according to user inputted parameters and chosen TerrainShape.
    /// </summary>
    public void GenerateTerrain(ref Vector3I indices, ref Voxel writeVoxel, float fluidFlowRadius, System.Random randomGenerator)
    {
        Vector3 voxelWorldPos;

        world.GetVoxelWorldPos(ref indices, out voxelWorldPos);

        if (world.IsBorder(ref voxelWorldPos))
        {
            writeVoxel.solid = Voxel.MaxVolume;
            return;
        }

        switch (shape)
        {
            case TerrainShape.Downhill:
                if (voxelWorldPos.y < voxelWorldPos.z)
                    writeVoxel.solid = Voxel.MaxVolume;
                return;

            case TerrainShape.Ground:
                if (voxelWorldPos.y == 1)
                    writeVoxel.solid = Voxel.MaxVolume;
                return;

            case TerrainShape.Random:
                writeVoxel.solid = (byte)(randomGenerator.Next(0, 100) < randomFillPercent ? Voxel.MaxVolume : 0);
                return;

            case TerrainShape.Ubend:
                if (voxelWorldPos.y == 1)
                    writeVoxel.solid = Voxel.MaxVolume;
                else if (voxelWorldPos.y > 3 && voxelWorldPos.x > (world.GetWidth() + 1) / 2 - fluidFlowRadius && voxelWorldPos.x < (world.GetWidth() + 1) / 2 + fluidFlowRadius)
                    writeVoxel.solid = Voxel.MaxVolume;
                return;
        }
    }

    /// <summary>
    /// Average smoothing of terrain in a voxel depending on the amount of solid in its neighbours.
    /// </summary>
    public void SmoothTerrain(ref Vector3I indices, ref Voxel writeVoxel)
    {
        Vector3 voxelWorldPos;

        world.GetVoxelWorldPos(ref indices, out voxelWorldPos);

        if (world.IsBorder(ref voxelWorldPos))
            return;

        // ground
        if (voxelWorldPos.y == 0)
            return;

        writeVoxel.solid = (byte)(GetSolidNeighboursCount(ref voxelWorldPos) > 13 * Voxel.MaxVolume ? Voxel.MaxVolume : 0);
    }

    #endregion

    #region terrain modification

    /// <summary>
    /// Adds or removes terrain at its surface.
    /// </summary>
    public void ModifyTerrain(Vector3 point, bool add, FluidComponentManager componentManager)
    {
        Vector3I indices;
        Vector3 worldPosition;

        for (int x = -terrainRadius; x <= terrainRadius; x++)
        {
            for (int z = -terrainRadius; z <= terrainRadius; z++)
            {
                // start at the bottom of world if adding terrain or at the top otherwise
                worldPosition.y = add ? 1 : world.GetHeight() - 1;

                worldPosition.x = point.x + x;
                worldPosition.z = point.z + z;

                // traverse the column at XZ position up or down
                while (world.GetVoxel(ref worldPosition, out indices))
                {
                    // if adding and found a not full voxel yet
                    if (add && world.blocks[indices.x].writeVoxels[indices.y][indices.z].solid < Voxel.MaxVolume)
                        break;

                    // if removing and found a not empty voxel yet
                    if (!add && world.blocks[indices.x].writeVoxels[indices.y][indices.z].solid > 0)
                        break;

                    worldPosition.y += add ? 1 : -1;
                }

                if (!indices.valid)
                    continue;

                HandleChanges(ref world.blocks[indices.x].writeVoxels[indices.y][indices.z], ref indices, false, add, componentManager);
            }
        }
    }

    /// <summary>
    /// Removes all the terrain around a given point.
    /// </summary>
    public void RemoveTerrain(Vector3 point, FluidComponentManager componentManager)
    {
        Vector3I indices;
        Vector3 worldPosition;

        for (int y = -terrainRadius; y <= terrainRadius; y++)
        {
            for (int x = -terrainRadius; x <= terrainRadius; x++)
            {
                for (int z = -terrainRadius; z <= terrainRadius; z++)
                {
                    worldPosition.x = point.x + x;
                    worldPosition.y = point.y + y;
                    worldPosition.z = point.z + z;

                    if (world.GetVoxel(ref worldPosition, out indices))
                    {
                        HandleChanges(ref world.blocks[indices.x].writeVoxels[indices.y][indices.z], ref indices, true, false, componentManager);
                    }
                }
            }
        }
    }

    #endregion

    #region private methods

    /// <summary>
    /// Terrain modification may split fluid components or connect them - they need to be reconstructed.
    /// </summary>
    private void HandleChanges(ref Voxel writeVoxel, ref Vector3I indices, bool remove, bool add, FluidComponentManager fluidComponentManager)
    {
        writeVoxel.solid = (byte)(remove ? 0 : Mathf.Clamp(writeVoxel.solid + (add ? 1 : -1) * terrainValue, byte.MinValue, Voxel.MaxVolume));

        if (fluidComponentManager.GetComponent(ref indices) != null)
        {
            fluidComponentManager.GetComponent(ref indices).Rebuild();
        }

        world.UnsettleChunkAndVoxel(ref indices);

        // unsettle back and left chunk also so that meshes connect up properly
        Vector3I nIndices;
        world.GetNeighbour(ref indices, Neighbour.Backward, out nIndices);
        world.UnsettleChunk(ref nIndices);
        world.GetNeighbour(ref indices, Neighbour.Left, out nIndices);
        world.UnsettleChunk(ref nIndices);
    }

    /// <summary>
    /// Returns amount of solid in voxels around given world position.
    /// </summary>
    private int GetSolidNeighboursCount(ref Vector3 voxelWorldPos)
    {
        Vector3I indices;
        Vector3 worldPosition;

        int count = 0;

        // 3x3x3 neighbourhood
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    // skip self
                    if (x == (int)voxelWorldPos.x && y == (int)voxelWorldPos.y && z == (int)voxelWorldPos.z)
                        continue;

                    worldPosition.x = voxelWorldPos.x + x;
                    worldPosition.y = voxelWorldPos.y + y;
                    worldPosition.z = voxelWorldPos.z + z;

                    if (world.GetVoxel(ref worldPosition, out indices))
                    {
                        count += world.blocks[indices.x].voxels[indices.y][indices.z].solid;
                    }
                }
            }
        }

        return count;
    }

    #endregion
}
