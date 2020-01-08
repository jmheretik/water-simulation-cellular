using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connected component of settled fluid voxels - represents a single body of water.
/// </summary>
public class FluidComponent
{
	/// <summary>
	/// Indices of settled fluid voxels stored as Vector3I(blockId, chunkId, voxelId) which make up this fluid component.
	/// </summary>
	public HashSet<Vector3I> voxels;

	/// <summary>
	/// Indices of voxels stored as Vector3I(blockId, chunkId, voxelId) which make up outlets of this fluid component.
	/// </summary>
	public HashSet<Vector3I> outlets;

	public bool settled;

	public Color debugColor;

	private short settleCounter;
	private int startLevel;
	private int waterLevel;
	private byte viscosity;
	private int lifetime;

	private FluidComponentManager manager;

	/// <summary>
	/// Initialize and construct fluid component by flood search from a given initial voxel indices.
	/// </summary>
	public FluidComponent(FluidComponentManager manager, Vector3I indices)
	{
		this.manager = manager;

		voxels = new HashSet<Vector3I>();
		outlets = new HashSet<Vector3I>();

		debugColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f, 0.1f, 0.1f);

		viscosity = manager.world.blocks[indices.x].voxels[indices.y][indices.z].viscosity;

		// let's try how many voxels not in this component can we reach through settled fluid
		voxels.Add(indices);
		GrowComponent();

		waterLevel = startLevel;
	}

	/// <summary>
	/// Remove this component and unsettle its voxels.
	/// </summary>
	public void Rebuild()
	{
		foreach (Vector3I indices in voxels)
		{
			Vector3I tmpIndices = indices;
			manager.RemoveComponent(ref tmpIndices, true);
		}

		foreach (Vector3I indices in outlets)
		{
			Vector3I tmpIndices = indices;
			manager.RemoveComponent(ref tmpIndices);
		}

		voxels.Clear();
		outlets.Clear();
		manager.searchStack.Clear();
	}

	/// <summary>
	/// Unsettle component so that it gets updated.
	/// </summary>
	public void Unsettle()
	{
		settleCounter = 10 * byte.MaxValue;
		settled = false;
	}

	public void DecreaseSettle()
	{
		if (settleCounter <= 0)
		{
			settleCounter = 0;
			settled = true;
		}
		else
		{
			settleCounter -= viscosity;
		}
	}

	/// <summary>
	/// Updates component, and removes voxels from the given collection voxelsToProcess which were reached from this component.
	/// </summary>
	public void Update(HashSet<Vector3I> voxelsToProcess)
	{
		int voxelCountBefore = voxels.Count;
		int waterLevelBefore = waterLevel;
		lifetime++;

		// remove invalid voxels, find new ones and update the start level
		GrowComponent();

		// remove found voxels from voxelsToProcess
		ExceptWith(voxelsToProcess, voxels);

		// component is still too young or small or being rebuilt
		if (lifetime < FluidComponentManager.MinComponentLifetime || voxels.Count < FluidComponentManager.MinComponentSize || voxels.Count - voxelCountBefore > FluidComponentManager.MinComponentSize)
			return;

		// remove old outlets, find new ones and update the water level
		GrowComponent(true);

		if (waterLevel == waterLevelBefore)
		{
			EqualizeOutlets();
		}

		DecreaseSettle();
	}

	/// <summary>
	/// Performs a flood search starting with voxels in this component and tries to reach and add new voxels OR outlets.
	/// 
	/// <!-- 
	/// Note: we can't search for new settled voxels AND outlets at the same time
	/// because we could reach another component's voxels or outlets through this component's outlets
	/// and add its voxels/outlets as our own even though the components are not connected by settled voxels.
	/// -->
	/// </summary>
	private void GrowComponent(bool searchingForOutlets = false)
	{
		UpdateVoxels();

		// flood search loop
		while (manager.searchStack.Count > 0)
		{
			// reached a new voxel
			Vector3I indices = manager.searchStack.Pop();

			manager.world.blocks[indices.x].visited[indices.y][indices.z] = true;
			HandleFoundvoxel(ref manager.world.blocks[indices.x].voxels[indices.y][indices.z], ref indices, searchingForOutlets);
		}

		if (searchingForOutlets)
		{
			UpdateOutlets();
		}
	}

	/// <summary>
	/// Adds given voxel or outlet to this component and enqueues its neighbours to search stack if they qualify.
	/// </summary>
	private void HandleFoundvoxel(ref Voxel voxel, ref Vector3I indices, bool searchingForOutlets)
	{
		if (searchingForOutlets)
		{
			int posY = manager.world.GetVoxelWorldPosY(ref indices);

			if (!voxel.IsFull || posY > waterLevel)
			{
				outlets.Add(indices);
				manager.AssignComponent(ref indices, this);
			}
		}
		else
		{
			// leave this method so that neighbours dont get pushed to stack
			if (HasExistingComponent(ref indices))
				return;

			voxels.Add(indices);
			manager.AssignComponent(ref indices, this);
		}

		// push to stack neighbours
		for (int i = 0; i < Voxel.NeighbourCount; i++)
		{
			Neighbour nDirection = (Neighbour)i;

			Vector3I nIndices;

			if (manager.world.GetNeighbour(ref indices, nDirection, out nIndices) && !manager.world.blocks[nIndices.x].visited[nIndices.y][nIndices.z])
			{
				HandleNeighbour(ref manager.world.blocks[nIndices.x].voxels[nIndices.y][nIndices.z], ref nIndices, searchingForOutlets);
			}
		}
	}

	private void HandleNeighbour(ref Voxel neighbour, ref Vector3I nIndices, bool searchingForOutlets)
	{
		int neighbourPosY = manager.world.GetVoxelWorldPosY(ref nIndices);

		if (searchingForOutlets)
		{
			Vector3I nBottomIndices;
			Voxel bottomNeighbour = manager.world.GetNeighbour(ref nIndices, Neighbour.Bottom, out nBottomIndices) ? manager.world.blocks[nBottomIndices.x].voxels[nBottomIndices.y][nBottomIndices.z] : new Voxel();

			// potential outlet
			if (neighbourPosY <= waterLevel + 1 && neighbourPosY >= startLevel && (neighbour.HasFluid || (bottomNeighbour.HasFluid && bottomNeighbour.IsFull)) && (neighbour.viscosity == 0 || neighbour.viscosity == viscosity))
			{
				manager.searchStack.Push(nIndices);
			}
		}
		else
		{
			// potential voxel of this component
			if (neighbour.settled && neighbour.HasFluid && (neighbour.viscosity == 0 || neighbour.viscosity == viscosity))
			{
				manager.searchStack.Push(nIndices);
			}
		}
	}

	/// <summary>
	/// Find out if this voxel already belongs to another bigger component and merge with it.
	/// </summary>
	private bool HasExistingComponent(ref Vector3I indices)
	{
		FluidComponent other = manager.GetComponent(ref indices);

		if (other != null && other != this && other.voxels.Contains(indices) && other.voxels.Count > voxels.Count && other.viscosity == viscosity)
		{
			// transfer voxels to the other component
			foreach (Vector3I myIndices in voxels)
			{
				Vector3I tmpIndices = myIndices;
				manager.AssignComponent(ref tmpIndices, other);
				other.voxels.Add(myIndices);
			}

			// transfer outlets to the other component
			foreach (Vector3I myIndices in outlets)
			{
				Vector3I tmpIndices = myIndices;
				manager.AssignComponent(ref tmpIndices, other);
				other.outlets.Add(myIndices);
			}

			other.Unsettle();

			// stop the search and this component will get removed
			voxels.Clear();
			outlets.Clear();
			manager.searchStack.Clear();

			return true;
		}

		return false;
	}

	/// <summary>
	/// If the voxel is close to a water surface of some fluid component - teleport all its fluid to component outlets.
	/// Prevents "stairs" effect characteristical in cellular automatas and ensures fluid pressure behaviour.
	/// </summary>
	private void EqualizeOutlets()
	{
		if (outlets.Count == 0 || viscosity == (byte)FlowViscosity.Lava)
			return;

		int sum = 0;
		int minLevel = int.MaxValue;

		// calculate sum of water levels in all outlets
		foreach (Vector3I outletIndices in outlets)
		{
			Vector3I tmpIndices = outletIndices;
			int level = manager.world.GetVoxelWorldPosY(ref tmpIndices) * Voxel.MaxVolume + manager.world.blocks[outletIndices.x].voxels[outletIndices.y][outletIndices.z].CurrentVolume;

			if (minLevel > level)
				minLevel = level;

			sum += level;
		}

		// average water level
		int avgLevel = sum / outlets.Count;

		if (avgLevel == minLevel)
			return;

		// adjust each outlet to this average
		foreach (Vector3I outletIndices in outlets)
		{
			Vector3I tmpIndices = outletIndices;

			Voxel outlet = manager.world.blocks[outletIndices.x].voxels[outletIndices.y][outletIndices.z];

			int exactLevel = manager.world.GetVoxelWorldPosY(ref tmpIndices) * Voxel.MaxVolume + outlet.CurrentVolume;

			// adjust fluid
			byte newFluid = (byte)Mathf.Clamp(avgLevel - (exactLevel - outlet.fluid), 0, Voxel.MaxVolume);
			byte newViscosity = (byte)(newFluid == 0 ? 0 : viscosity);

			manager.world.blocks[outletIndices.x].writeVoxels[outletIndices.y][outletIndices.z].fluid = newFluid;
			manager.world.blocks[outletIndices.x].writeVoxels[outletIndices.y][outletIndices.z].viscosity = newViscosity;

			if (Mathf.Abs(outlet.fluid - newFluid) >= Voxel.MaxVolume / 2)
			{
				manager.world.blocks[outletIndices.x].writeVoxels[outletIndices.y][outletIndices.z].teleporting = true;
			}

			UnsettleAfterEqualize(ref tmpIndices);
		}

		manager.world.UpdateValues();
	}

	private void UnsettleAfterEqualize(ref Vector3I outletIndices)
	{
		Vector3I nIndices;

		// unsettle if fluid grew higher than air neighbours
		if ((manager.world.GetNeighbour(ref outletIndices, Neighbour.Forward, out nIndices) && manager.world.blocks[nIndices.x].voxels[nIndices.y][nIndices.z].IsAir) ||
			(manager.world.GetNeighbour(ref outletIndices, Neighbour.Backward, out nIndices) && manager.world.blocks[nIndices.x].voxels[nIndices.y][nIndices.z].IsAir) ||
			(manager.world.GetNeighbour(ref outletIndices, Neighbour.Left, out nIndices) && manager.world.blocks[nIndices.x].voxels[nIndices.y][nIndices.z].IsAir) ||
			(manager.world.GetNeighbour(ref outletIndices, Neighbour.Right, out nIndices) && manager.world.blocks[nIndices.x].voxels[nIndices.y][nIndices.z].IsAir))
		{
			manager.world.UnsettleChunkAndVoxel(ref outletIndices);
		}

		// unsettle bottom chunk also so that meshes connect up properly
		manager.world.GetNeighbour(ref outletIndices, Neighbour.Bottom, out nIndices);
		manager.world.UnsettleChunk(ref outletIndices);
		manager.world.UnsettleChunk(ref nIndices);
	}

	/// <summary>
	/// Removes invalid outlets and sets the current water level.
	/// </summary>
	private void UpdateOutlets()
	{
		if (outlets.Count == 0)
			return;

		int minLevel = int.MaxValue;

		foreach (Vector3I indices in outlets)
		{
			Vector3I tmpIndices = indices;

			int posY = manager.world.GetVoxelWorldPosY(ref tmpIndices);
			Voxel outlet = manager.world.blocks[indices.x].voxels[indices.y][indices.z];

			// invalid outlet = full below OR empty above water level
			if ((posY <= waterLevel && outlet.IsFull) || (posY > waterLevel && !outlet.HasFluid))
			{
				manager.toRemoveList.Add(indices);

				if (!voxels.Contains(indices))
				{
					manager.RemoveComponent(ref tmpIndices);
				}
			}
			// valid outlet
			else if (posY < minLevel)
			{
				minLevel = posY;
			}
		}

		if (minLevel != int.MaxValue)
		{
			waterLevel = minLevel;
		}

		if (outlets.Count == 0)
		{
			waterLevel++;
		}

		RemoveInListFromSet(outlets);
	}

	/// <summary>
	/// Removes invalid voxels, sets the component's start level and adds valid voxels to search stack.
	/// </summary>
	private void UpdateVoxels()
	{
		manager.world.Unvisit();

		startLevel = int.MaxValue;

		// validate all voxels already in this component
		foreach (Vector3I indices in voxels)
		{
			Vector3I nIndices, tmpIndices = indices;

			int posY = manager.world.GetVoxelWorldPosY(ref tmpIndices);
			Voxel voxel = manager.world.blocks[indices.x].voxels[indices.y][indices.z];
			Voxel bottomNeighbour = manager.world.GetNeighbour(ref tmpIndices, Neighbour.Bottom, out nIndices) ? manager.world.blocks[nIndices.x].voxels[nIndices.y][nIndices.z] : new Voxel();

			// invalid voxel = unsettled/empty voxel OR unsettled/empty bottomNeighbour
			if (!voxel.settled || !voxel.HasFluid || (bottomNeighbour.valid && !bottomNeighbour.IsTerrain && (!bottomNeighbour.settled || !bottomNeighbour.HasFluid)))
			{
				manager.RemoveComponent(ref tmpIndices, true);
				manager.toRemoveList.Add(indices);
			}
			// valid voxel = add to stack and mark as already visited
			else
			{
				manager.world.blocks[indices.x].visited[indices.y][indices.z] = true;
				manager.searchStack.Push(indices);

				if (posY < startLevel)
				{
					startLevel = posY;
				}
			}
		}

		RemoveInListFromSet(voxels);
	}

	/// <summary>
	/// Removes all voxels which are in the other set from the given set.
	/// Basically a GC friendly HashSet.ExceptWith operation.
	/// </summary>
	private void ExceptWith(HashSet<Vector3I> set, HashSet<Vector3I> otherSet)
	{
		foreach (Vector3I indices in set)
		{
			if (otherSet.Contains(indices))
			{
				manager.toRemoveList.Add(indices);
			}
		}

		RemoveInListFromSet(set);
	}

	private void RemoveInListFromSet(HashSet<Vector3I> set)
	{
		if (manager.toRemoveList.Count > 0)
		{
			Unsettle();

			for (int i = 0; i < manager.toRemoveList.Count; i++)
			{
				set.Remove(manager.toRemoveList[i]);
			}
		}

		manager.toRemoveList.Clear();
	}
}
