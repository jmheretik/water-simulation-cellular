using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Holds and manages data required for rendering terrain and fluid in a chunk.
	/// </summary>
	public class ChunkRenderData : IDisposable
	{
		public bool FluidNeedsRebuild = false;

		/// <summary>
		/// Data required for rendering of each fluid type in this chunk.
		/// </summary>
		public Dictionary<Viscosity, (bool visited, object[] data)> Fluid;

		/// <summary>
		/// Data required for rendering the terrain in this chunk.
		/// </summary>
		public GameObject Terrain;

		private Chunk _chunk;

		public ChunkRenderData(Chunk chunk)
		{
			_chunk = chunk;

			Fluid = new Dictionary<Viscosity, (bool, object[])>(FluidProcessor.Types.Count);
		}

		/// <summary>
		/// Determines if the fluid mesh needs to be rebuilt.
		/// </summary>
		public bool HasFluidData()
		{
			return _chunk.Block.SimData.UnsettledChunks.Contains(_chunk.Id) || FluidNeedsRebuild;
		}

		/// <summary>
		/// Ensures and manages existence of data required for terrain rendering.
		/// </summary>
		public void CheckTerrain()
		{
			for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
			{
				ref readonly Voxel voxel = ref _chunk.Block.Voxels.Get(_chunk.Id, voxelId);

				if (voxel.Solid > 0)
				{
					if (Terrain == null)
					{
						Terrain = new GameObject("terrain mesh");
						Terrain.transform.parent = _chunk.transform;
						Terrain.AddComponent<MeshFilter>();   // to be passed into mesh generator
						Terrain.AddComponent<MeshRenderer>();
						Terrain.AddComponent<MeshCollider>(); // used for mouse interaction with the scene (raycasting)
					}

					return;
				}
			}

			if (Terrain != null)
			{
				GameObject.Destroy(Terrain);
			}
		}

		/// <summary>
		/// Ensures and manages existence of data required for fluid rendering.
		/// Returns false if there is no fluid in the chunk.
		/// </summary>
		public bool CheckFluid(bool gpuFluidRendering)
		{
			foreach (Viscosity viscosity in FluidProcessor.Types.Keys)
			{
				if (Fluid.TryGetValue(viscosity, out var value))
				{
					value.visited = false;
				}
			}

			for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
			{
				ref readonly Voxel voxel = ref _chunk.Block.Voxels.Get(_chunk.Id, voxelId);

				if (voxel.HasFluid)
				{
					Viscosity viscosity = (Viscosity)voxel.Viscosity;

					if (Fluid.TryGetValue(viscosity, out var value))
					{
						value.visited = true;
					}
					else
					{
						CreateFluidData(viscosity, gpuFluidRendering);
					}
				}
			}

			DestroyFluidData(true);

			return Fluid.Count > 0;
		}

		private void CreateFluidData(Viscosity viscosity, bool gpuFluidRendering)
		{
			if (gpuFluidRendering)
			{
				Fluid.Add(viscosity, (true, new object[] { new CommandBuffer(), new ComputeBuffer(Chunk.kTotalVoxelsInBordered, sizeof(float), ComputeBufferType.Default) }));
			}
			else
			{
				GameObject fluidMeshGo = new GameObject(viscosity.ToString() + " fluid mesh");
				fluidMeshGo.transform.parent = _chunk.transform;
				fluidMeshGo.AddComponent<MeshFilter>();
				fluidMeshGo.AddComponent<MeshRenderer>();

				Fluid.Add(viscosity, (true, new object[] { fluidMeshGo }));
			}
		}

		private void DestroyFluidData(bool onlyUnvisited)
		{
			foreach (Viscosity viscosity in FluidProcessor.Types.Keys)
			{
				if (Fluid.TryGetValue(viscosity, out var value) && (!onlyUnvisited || !value.visited))
				{
					if (value.data.Length > 1)
					{
						((CommandBuffer)value.data[0]).Release();
						((ComputeBuffer)value.data[1]).Release();
					}
					else
					{
						GameObject.Destroy((GameObject)value.data[0]);
					}
				}
			}
		}

		public void Dispose()
		{
			DestroyFluidData(false);
		}
	}
}