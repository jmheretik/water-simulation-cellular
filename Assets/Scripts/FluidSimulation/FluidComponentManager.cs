using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates, removes and updates connected components of fluid.
/// </summary>
public class FluidComponentManager
{
    /// <summary>
    /// How long should this component exist before we start updating it.
    /// Multiply this with how often the coroutine in FluidProcessor calls UpdateComponents to get the time in seconds.
    /// </summary>
    public const int MinComponentLifetime = 20;

    /// <summary>
    /// How many new settled fluid voxels can make a new component and how small components should be removed.
    /// </summary>
    public const int MinComponentSize = 15;

    public World world;
    public List<FluidComponent> components;
    public Stack<Vector3I> searchStack;
    public List<Vector3I> toRemoveList;

    private HashSet<Vector3I> voxelsToProcess;
    private Dictionary<Vector3I, FluidComponent> voxelComponents;   // because checking each component if it contains specific voxel is too expensive
    private ComponentComparer componentComparer;

    public FluidComponentManager(World world)
    {
        this.world = world;

        components = new List<FluidComponent>();
        voxelsToProcess = new HashSet<Vector3I>();
        voxelComponents = new Dictionary<Vector3I, FluidComponent>();
        componentComparer = new ComponentComparer();
        searchStack = new Stack<Vector3I>();
        toRemoveList = new List<Vector3I>();
    }

    public void ProcessVoxel(ref Vector3I indices)
    {
        if (!voxelComponents.ContainsKey(indices))
        {
            // settled fluid voxel that doesn't belong to any component yet
            voxelsToProcess.Add(indices);
        }
    }

    public void AssignComponent(ref Vector3I indices, FluidComponent componentToAssign)
    {
        FluidComponent component;
        voxelComponents.TryGetValue(indices, out component);

        if (component != componentToAssign)
        {
            componentToAssign.Unsettle();
            voxelComponents[indices] = componentToAssign;
        }
    }

    public void RemoveComponent(ref Vector3I indices, bool alsoUnsettleVoxel = false)
    {
        if (alsoUnsettleVoxel)
        {
            world.UnsettleChunkAndVoxel(ref indices);
        }

        voxelComponents.Remove(indices);
    }

    public FluidComponent GetComponent(ref Vector3I indices)
    {
        FluidComponent component;
        voxelComponents.TryGetValue(indices, out component);
        return component;
    }

    public void UpdateComponents()
    {
        // sort components by their size descending so that bigger ones update sooner and swallow smaller ones
        components.Sort(componentComparer);

        // update already existing unsettled components
        for (int i = 0; i < components.Count; i++)
        {
            if (!components[i].settled || voxelsToProcess.Count > 0)
            {
                components[i].Update(voxelsToProcess);
            }
        }

        // make new components from remaining voxels which havent been reached yet from any other component
        foreach (Vector3I indices in voxelsToProcess)
        {
            Vector3I tmpIndices = indices;

            if (GetComponent(ref tmpIndices) == null && voxelsToProcess.Count >= MinComponentSize)
            {
                components.Add(new FluidComponent(this, indices));
            }
        }

        // remove small components
        for (int i = 0; i < components.Count; i++)
        {
            if (components[i].voxels.Count < MinComponentSize)
            {
                components[i].Rebuild();
            }
        }

        components.RemoveAll(x => x.voxels.Count < MinComponentSize);

        voxelsToProcess.Clear();
    }
}

public class ComponentComparer : IComparer<FluidComponent>
{
    public int Compare(FluidComponent x, FluidComponent y)
    {
        return y.voxels.Count.CompareTo(x.voxels.Count);
    }
}
