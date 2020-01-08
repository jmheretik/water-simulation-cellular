using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Predefined fluid viscosities.
/// </summary>
public enum FlowViscosity
{
    Water = 255,
    Lava = 20
}

public class FluidProcessor : MonoBehaviour
{
    [Header("Fluid modification")]
    [Range(Voxel.Epsilon, Voxel.MaxVolume)]
    public byte flowValue = 20;
    public int flowRadius = 2;
    public FlowViscosity flowViscosity = FlowViscosity.Water;      // the smaller the more viscous

    [Header("Fluid components")]
    public float componentsUpdateInterval = 0.1f;
    public bool debugComponentGrid = false;
    public FluidComponentManager componentManager;

    private World world;

    public void Initialize(World world)
    {
        this.world = world;

        componentManager = new FluidComponentManager(world);
        StartCoroutine(FluidComponentsUpdate());
    }

    #region fluid modification

    /// <summary>
    /// Adds or removes fluid.
    /// If adding, first tries to add around the given point.
    /// If it fails or if removing, adds or removes at the current top.
    /// </summary>
    public void ModifyFluid(Vector3 point, bool add)
    {
        if (add && TryAddAroundPoint(ref point))
            return;

        Vector3I indices;
        Vector3 worldPosition;

        for (float x = -flowRadius; x <= flowRadius; x = x + WorldGridInfo.kVoxelSize)
        {
            for (float z = -flowRadius; z <= flowRadius; z = z + WorldGridInfo.kVoxelSize)
            {
                // start at the bottom of world if adding terrain or at the top otherwise
                worldPosition.y = add ? WorldGridInfo.kVoxelSize : world.GetHeight() - WorldGridInfo.kVoxelSize;

                worldPosition.x = point.x + x;
                worldPosition.z = point.z + z;

                // traverse the column at XZ position up or down
                while (world.GetVoxel(ref worldPosition, out indices))
                {
                    Voxel voxel = world.blocks[indices.x].writeVoxels[indices.y][indices.z];

                    // if adding and found a not full voxel yet
                    if (add && !voxel.IsFull && (voxel.viscosity == 0 || voxel.viscosity == (byte)flowViscosity))
                        break;

                    // if removing and found a not empty voxel yet
                    if (!add && voxel.HasFluid && (voxel.viscosity == (byte)flowViscosity))
                        break;

                    worldPosition.y += add ? 1 : -1;
                }

                if (!indices.valid)
                    continue;

                HandleModifyFluidVoxel(add, ref indices, ref world.blocks[indices.x].writeVoxels[indices.y][indices.z]);
            }
        }
    }

    /// <summary>
    /// Try to add fluid around a given point.
    /// </summary>
    private bool TryAddAroundPoint(ref Vector3 point)
    {
        bool added = false;
        Vector3 worldPosition;
        Vector3I indices;

        for (float y = -flowRadius; y <= flowRadius; y = y + WorldGridInfo.kVoxelSize)
        {
            for (float x = -flowRadius; x <= flowRadius; x = x + WorldGridInfo.kVoxelSize)
            {
                for (float z = -flowRadius; z <= flowRadius; z = z + WorldGridInfo.kVoxelSize)
                {
                    worldPosition.x = point.x + x;
                    worldPosition.y = point.y + y;
                    worldPosition.z = point.z + z;

                    if (!world.GetVoxel(ref worldPosition, out indices))
                        continue;

                    Voxel voxel = world.blocks[indices.x].writeVoxels[indices.y][indices.z];

                    if (!voxel.IsFull && (voxel.viscosity == 0 || voxel.viscosity == (byte)flowViscosity))
                    {
                        HandleModifyFluidVoxel(true, ref indices, ref world.blocks[indices.x].writeVoxels[indices.y][indices.z]);
                        added = true;
                    }
                }
            }
        }

        return added;
    }

    private void HandleModifyFluidVoxel(bool add, ref Vector3I indices, ref Voxel writeVoxel)
    {
        float scaledFlowValue = flowValue * ((float)flowViscosity / byte.MaxValue);

        writeVoxel.fluid = (byte)Mathf.Clamp(writeVoxel.fluid + (add ? scaledFlowValue : -scaledFlowValue), byte.MinValue, byte.MaxValue);

        if (add)
        {
            writeVoxel.viscosity = (byte)flowViscosity;
        }

        world.UnsettleChunkAndVoxel(ref indices);

        if (!add && componentManager.GetComponent(ref indices) != null)
        {
            componentManager.GetComponent(ref indices).Unsettle();
        }

        // unsettle bottom chunk also so that meshes connect up properly
        Vector3I nIndices;
        world.GetNeighbour(ref indices, Neighbour.Bottom, out nIndices);
        world.UnsettleChunk(ref nIndices);
    }

    #endregion

    #region fluid simulation

    public void FluidUpdate()
    {
        Vector3I indices;

        // simulation consists of 3 steps and the values written to chunks have to be updated after each step
        for (int step = 0; step < 3; step++)
        {
            for (int blockId = 0; blockId < world.blocks.Length; blockId++)
            {
                for (int chunkId = 0; chunkId < world.blocks[blockId].chunks.Length; chunkId++)
                {
                    Chunk chunk = world.blocks[blockId].chunks[chunkId];

                    // skip settled chunks
                    if (chunk.settled && chunk.HasSettledNeighbours())
                        continue;

                    for (int voxelId = 0; voxelId < world.blocks[blockId].voxels[chunkId].Length; voxelId++)
                    {
                        indices.x = blockId;
                        indices.y = chunkId;
                        indices.z = voxelId;

                        HandleVoxel(step, ref indices, ref world.blocks[indices.x].voxels[indices.y][voxelId], ref world.blocks[indices.x].writeVoxels[indices.y][voxelId]);
                    }
                }
            }

            world.UpdateValues();
        }
    }

    /// <summary>
    /// Calculate new voxel state according to the states of its neighbours.
    /// </summary>
    private void HandleVoxel(int step, ref Vector3I indices, ref Voxel voxel, ref Voxel writeVoxel)
    {
        // settled terrain
        if (voxel.settled && voxel.IsTerrain)
            return;

        Vector3I top, bottom, forward, backward, right, left;

        bool topSettled = world.GetNeighbour(ref indices, Neighbour.Top, out top) ? world.blocks[top.x].voxels[top.y][top.z].settled : true;
        bool bottomSettled = world.GetNeighbour(ref indices, Neighbour.Bottom, out bottom) ? world.blocks[bottom.x].voxels[bottom.y][bottom.z].settled : true;
        bool forwardSettled = world.GetNeighbour(ref indices, Neighbour.Forward, out forward) ? world.blocks[forward.x].voxels[forward.y][forward.z].settled : true;
        bool backwardSettled = world.GetNeighbour(ref indices, Neighbour.Backward, out backward) ? world.blocks[backward.x].voxels[backward.y][backward.z].settled : true;
        bool rightSettled = world.GetNeighbour(ref indices, Neighbour.Right, out right) ? world.blocks[right.x].voxels[right.y][right.z].settled : true;
        bool leftSettled = world.GetNeighbour(ref indices, Neighbour.Left, out left) ? world.blocks[left.x].voxels[left.y][left.z].settled : true;

        // skip settled voxel with settled neighbours = air or settled fluid below the water surface
        if (voxel.settled && topSettled && bottomSettled && forwardSettled && backwardSettled && rightSettled && leftSettled)
            return;

        // main simulation steps
        if (step == 0 && !(voxel.settled && topSettled && bottomSettled))
            FlowUp(ref voxel, ref writeVoxel, ref bottom);

        if (step == 1 && !(voxel.settled && topSettled && bottomSettled))
            FlowDown(ref voxel, ref writeVoxel, ref top, ref bottom);

        if (step == 2 && !(voxel.settled && forwardSettled && backwardSettled && rightSettled && leftSettled))
            FlowSideways(ref indices, ref voxel, ref writeVoxel, ref bottom);

        // settling
        bool topHasFluid = top.valid && world.blocks[top.x].voxels[top.y][top.z].HasFluid;
        bool falling = topHasFluid && !bottomSettled;

        // fluid changed
        if (Mathf.Abs(voxel.fluid - writeVoxel.fluid) > 0)
        {
            world.UnsettleChunkAndVoxel(ref indices);
        }
        else if (step == 2 && !voxel.settled && !falling)
        {
            writeVoxel.DecreaseSettle();

            if (writeVoxel.settled && writeVoxel.HasFluid)
            {
                componentManager.ProcessVoxel(ref indices);
            }
        }

        if (!topHasFluid)
        {
            writeVoxel.teleporting = false;
        }
    }

    private IEnumerator FluidComponentsUpdate()
    {
        while (true)
        {
            componentManager.UpdateComponents();

            yield return new WaitForSeconds(componentsUpdateInterval);
        }
    }

    #endregion

    #region fluid simulation steps

    /// <summary>
    /// Take excess fluid from voxel below and push this voxel's excess fluid to voxel above.
    /// </summary>
    private void FlowUp(ref Voxel voxel, ref Voxel writeVoxel, ref Vector3I bottom)
    {
        Voxel bottomNeighbour = bottom.valid ? world.blocks[bottom.x].voxels[bottom.y][bottom.z] : new Voxel();

        int outFlow = voxel.ExcessFluid;
        int inFlow = voxel.HasCompatibleViscosity(ref bottomNeighbour) ? bottomNeighbour.ExcessFluid : 0;

        if (inFlow > 0)
            world.UnsettleChunk(ref bottom);

        WriteChanges(ref writeVoxel, inFlow, outFlow, voxel.HasCompatibleViscosity(ref bottomNeighbour) ? bottomNeighbour.viscosity : byte.MinValue);
    }

    /// <summary>
    /// Give as much as possible to voxel under and take as much as possible from voxel above.
    /// </summary>
    private void FlowDown(ref Voxel voxel, ref Voxel writeVoxel, ref Vector3I top, ref Vector3I bottom)
    {
        Voxel topNeighbour = top.valid ? world.blocks[top.x].voxels[top.y][top.z] : new Voxel();
        Voxel bottomNeighbour = bottom.valid ? world.blocks[bottom.x].voxels[bottom.y][bottom.z] : new Voxel();

        int outFlow = voxel.HasCompatibleViscosity(ref bottomNeighbour) ? Mathf.Clamp(voxel.fluid, 0, bottomNeighbour.FreeVolume) : 0;
        int inFlow = voxel.HasCompatibleViscosity(ref topNeighbour) ? Mathf.Clamp(topNeighbour.fluid, 0, voxel.FreeVolume) : 0;

        if (inFlow > 0)
            world.UnsettleChunk(ref top);

        if (outFlow > 0)
            world.UnsettleChunk(ref bottom);

        WriteChanges(ref writeVoxel, inFlow, outFlow, voxel.HasCompatibleViscosity(ref topNeighbour) ? topNeighbour.viscosity : byte.MinValue);
    }

    /// <summary>
    /// If this voxel's fluid can't flow down anymore - distribute it to its horizontal neighbours which have less.
    /// </summary>
    private void FlowSideways(ref Vector3I indices, ref Voxel voxel, ref Voxel writeVoxel, ref Vector3I bottom)
    {
        for (int i = 0; i < Voxel.NeighbourCount; i++)
        {
            Neighbour nDirection = (Neighbour)i;

            // skip vertical neighbours
            if (nDirection == Neighbour.Top || nDirection == Neighbour.Bottom)
                continue;

            Vector3I nIndices;
            Voxel neighbour = world.GetNeighbour(ref indices, nDirection, out nIndices) ? world.blocks[nIndices.x].voxels[nIndices.y][nIndices.z] : new Voxel();

            if (!voxel.HasCompatibleViscosity(ref neighbour))
                continue;

            float inFlow = 0;
            float outFlow = 0;
            byte neighbourViscosity = byte.MinValue;

            Vector3I nBottomNeighbourIndices;
            Voxel bottomNeighbour = bottom.valid ? world.blocks[bottom.x].voxels[bottom.y][bottom.z] : new Voxel();
            Voxel nBottomNeighbour = world.GetNeighbour(ref nIndices, Neighbour.Bottom, out nBottomNeighbourIndices) ? world.blocks[nBottomNeighbourIndices.x].voxels[nBottomNeighbourIndices.y][nBottomNeighbourIndices.z] : new Voxel();

            if (!bottomNeighbour.valid || bottomNeighbour.FreeVolume < voxel.fluid)
            {
                // give as much (this voxel's fluid / neighbour count) to neighbours as possible
                float outVolume = voxel.CurrentVolume - neighbour.CurrentVolume;
                outFlow = Mathf.Clamp(outVolume / (Voxel.NeighbourCount - 1), 0, voxel.fluid / (Voxel.NeighbourCount - 1));

                if (outFlow > 0)
                    world.UnsettleChunk(ref nIndices);
            }

            if (!nBottomNeighbour.valid || nBottomNeighbour.FreeVolume < neighbour.fluid)
            {
                // take as much (neighbour's fluid / neighbour count) from a neighbour as possible
                float inVolume = neighbour.CurrentVolume - voxel.CurrentVolume;
                inFlow = Mathf.Clamp(inVolume / (Voxel.NeighbourCount - 1), 0, neighbour.fluid / (Voxel.NeighbourCount - 1));

                neighbourViscosity = neighbour.viscosity;

                if (inFlow > 0)
                    world.UnsettleChunk(ref nIndices);
            }

            WriteChanges(ref writeVoxel, inFlow, outFlow, neighbourViscosity);
        }
    }

    /// <summary>
    /// Scales voxel's inflow and outflow according to fluid's viscosity and writes new fluid value to a given voxel.
    /// </summary>
    private void WriteChanges(ref Voxel writeVoxel, float inFlow, float outFlow, byte inFlowViscosity)
    {
        if (inFlowViscosity != 0 && inFlow > 0)
        {
            writeVoxel.viscosity = inFlowViscosity;
        }

        float transfer = (inFlow - outFlow) * (writeVoxel.viscosity / (float)byte.MaxValue);

        if (transfer > (writeVoxel.viscosity / (float)byte.MaxValue) && transfer < 1)
            transfer = 1;

        if (transfer < -(writeVoxel.viscosity / (float)byte.MaxValue) && transfer > -1)
            transfer = -1;

        writeVoxel.fluid = (byte)Mathf.Clamp(writeVoxel.fluid + Mathf.Round(transfer), byte.MinValue, byte.MaxValue);
    }

    #endregion

#if UNITY_EDITOR

    void OnDrawGizmos()
    {
        if (componentManager != null && debugComponentGrid)
        {
            Vector3 voxelWorldPos;

            for (int i = 0; i < componentManager.components.Count; i++)
            {
                FluidComponent component = componentManager.components[i];

                // settled component voxels
                foreach (Vector3I indices in component.voxels)
                {
                    Vector3I tmpIndices = indices;
                    Voxel voxel = world.blocks[indices.x].voxels[indices.y][indices.z];

                    world.GetVoxelWorldPos(ref tmpIndices, out voxelWorldPos);
                    DrawDebugCube(component.debugColor, voxelWorldPos, World.VoxelSize, voxel.RenderFluid);
                }

                // component outlets
                foreach (Vector3I indices in component.outlets)
                {
                    Vector3I tmpIndices = indices;

                    world.GetVoxelWorldPos(ref tmpIndices, out voxelWorldPos);
                    DrawDebugCube(new Color(0.5f, 0f, 0f, 0.1f), voxelWorldPos, World.VoxelSize, 1);
                }
            }
        }
    }

    public void DrawDebugCube(Color color, Vector3 worldPos, Vector3 size, float fluid = Voxel.MaxVolume, bool labels = false, string label = null)
    {
        worldPos -= new Vector3(0.5f, 0.5f, 0.5f);

        if (labels)
        {
            UnityEditor.Handles.Label(worldPos + 0.5f * size, label);
        }

        Vector3 cubeSize = size;

        if (fluid != Voxel.MaxVolume)
        {
            // shift not full voxel's center and size
            worldPos.y -= (1 - fluid) / 2;
            cubeSize.y = fluid;
        }

        Gizmos.color = color;
        Gizmos.DrawCube(worldPos + 0.5f * size, cubeSize);
    }

#endif

}
