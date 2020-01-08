using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Contains methods to get and update parts of the world.
	/// To be called from outside of _world (without direct access to _blocks).
	/// </summary>
	public class WorldApi : MonoBehaviour
	{
		[Header("Debug grids")]
		public bool DebugBlockGrid = false;
		public bool DebugChunkGrid = false;
		public bool DebugVoxelGrid = false;
		public bool DebugVoxelLabels = false;
		public bool DebugComponents = false;

		[Header("World generation")]
		public int SizeX = 1;
		public int SizeY = 1;
		public int SizeZ = 1;

		private Block[] _blocks;
		private FluidProcessor _fluidProcessor;
		private MarchingCubesMeshGenerator _meshGenerator;

		/// <summary>
		/// Holds pending writes to voxels which will be executed right after the next simulation iteration.
		/// </summary>
		private Dictionary<VectorI3, Voxel> _pendingChanges;

		/// <summary>
		/// Initializes references.
		/// </summary>
		public void Initialize(Block[] blocks)
		{
			_fluidProcessor = GetComponent<FluidProcessor>();
			_meshGenerator = GetComponent<MarchingCubesMeshGenerator>();

			_blocks = blocks;

			_pendingChanges = new Dictionary<VectorI3, Voxel>();
		}

		#region update

		/// <summary>
		/// Marks chunk as unsettled to notify the simulation of changes in this chunk and also so that its mesh gets updated.
		/// </summary>
		public void UnsettleChunk(in VectorI3 indices)
		{
			_blocks[indices.x].Chunks[indices.y].Unsettle();
		}

		/// <summary>
		/// Updates solid or fluid meshes of unsettled chunks.
		/// </summary>
		public void UpdateUnsettledMeshes(bool solid)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Rendering");

			for (int blockId = 0; blockId < _blocks.Length; blockId++)
			{
				for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
				{
					Chunk chunk = _blocks[blockId].Chunks[chunkId];

					if (solid || chunk.RenderData.HasFluidData())
					{
						_meshGenerator.UpdateMesh(chunk, solid);
					}
				}
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Forces update of all meshes.
		/// </summary>
		public void UpdateAllMeshes()
		{
			for (int blockId = 0; blockId < _blocks.Length; blockId++)
			{
				for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
				{
					_meshGenerator.UpdateMesh(_blocks[blockId].Chunks[chunkId], true);
					_meshGenerator.UpdateMesh(_blocks[blockId].Chunks[chunkId], false);
				}
			}
		}

		/// <summary>
		/// Process pending writes to voxels caused by fluid or terrain modification.
		/// </summary>
		public void ProcessPendingChanges()
		{
			UnityEngine.Profiling.Profiler.BeginSample("ProcessPendingChanges");

			foreach (var change in _pendingChanges)
			{
				VectorI3 indices = change.Key;
				_blocks[indices.x].Voxels.GetWritable(indices.y, indices.z) = change.Value;
			}

			_pendingChanges.Clear();

			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// Writes given voxel to the given indices and also unsettles chunk it belongs to right after the next simulation iteration.
		/// </summary>
		public void SetVoxelAfterSim(in VectorI3 indices, in Voxel voxelCopy)
		{
			_pendingChanges[indices] = voxelCopy;
		}

		#endregion

		#region get

		public Block[] GetBlocks()
		{
			return _blocks;
		}

		/// <summary>
		/// Returns a writable reference (or just a copy) of a voxel at given indices.
		/// </summary>
		public ref Voxel GetVoxelWritable(in VectorI3 indices)
		{
			return ref _blocks[indices.x].Voxels.GetWritable(indices.y, indices.z);
		}

		/// <summary>
		/// Returns a readonly reference (or just a copy) of a voxel at given indices.
		/// </summary>
		public ref readonly Voxel GetVoxel(in VectorI3 indices)
		{
			return ref _blocks[indices.x].Voxels.Get(indices.y, indices.z);
		}

		/// <summary>
		/// Try to return a readonly reference (or just a copy) of a voxel and its indices from world position.
		/// </summary>
		public ref readonly Voxel TryGetVoxel(in Vector3 worldPosition, out VectorI3 indices)
		{
			indices = default;

			// border or non-existent block
			if (IsBorder(in worldPosition) || !BlockToBlockId(WorldGridInfo.WorldToBlock(worldPosition), out int blockId))
				return ref Voxel.Invalid;

			VectorI3 blockVoxelPos = WorldGridInfo.WorldToBlockVoxel(worldPosition);
			indices.x = blockId;
			indices.y = WorldGridInfo.BlockVoxelToChunkId(blockVoxelPos.x, blockVoxelPos.y, blockVoxelPos.z);
			indices.z = WorldGridInfo.BlockVoxelToVoxelId(blockVoxelPos.x, blockVoxelPos.y, blockVoxelPos.z);

			return ref GetVoxel(in indices);
		}

		/// <summary>
		/// Try to return a readonly reference (or just a copy) of a neighbouring voxel and its indices from voxel's indices.
		/// </summary>
		public ref readonly Voxel TryGetNeighbour(in VectorI3 indices, Neighbour neighbour, out VectorI3 nIndices)
		{
			// adjust nIndices
			nIndices.x = !WorldGridInfoHelper.GetNeighbour(indices.y, indices.z, neighbour, out nIndices.y, out nIndices.z) ? indices.x : _blocks[indices.x].GetNeighbour(neighbour)?.Id ?? -1;

			if (nIndices.x > -1)
			{
				return ref GetVoxel(in nIndices);
			}
			else
			{
				// non-existent neighbour voxel
				return ref Voxel.Invalid;
			}
		}

		/// <summary>
		/// Is given worldPosition at world's borders?
		/// </summary>
		public bool IsBorder(in Vector3 worldPosition)
		{
			return worldPosition.x < WorldGridInfo.kVoxelSize || worldPosition.x >= GetWidth() || worldPosition.y < WorldGridInfo.kVoxelSize || worldPosition.y >= GetHeight() || worldPosition.z < WorldGridInfo.kVoxelSize || worldPosition.z >= GetDepth();
		}

		public float GetWidth()
		{
			return WorldGridInfo.kBlockSize * SizeX - WorldGridInfo.kVoxelSize;
		}

		public float GetHeight()
		{
			return WorldGridInfo.kBlockSize * SizeY - WorldGridInfo.kVoxelSize;
		}

		public float GetDepth()
		{
			return WorldGridInfo.kBlockSize * SizeZ - WorldGridInfo.kVoxelSize;
		}

		/// <summary>
		/// Returns world position of block's origin from its id.
		/// </summary>
		public void GetBlockWorldPos(int blockId, out Vector3 worldPosition)
		{
			worldPosition.x = _blocks[blockId].Coords.x * WorldGridInfo.kBlockSize;
			worldPosition.y = _blocks[blockId].Coords.y * WorldGridInfo.kBlockSize;
			worldPosition.z = _blocks[blockId].Coords.z * WorldGridInfo.kBlockSize;
		}

		/// <summary>
		/// Returns world position of chunk's origin from its indices.
		/// </summary>
		public void GetChunkWorldPos(int blockId, int chunkId, out Vector3 worldPosition)
		{
			GetBlockWorldPos(blockId, out worldPosition);

			int localZ = chunkId & (WorldGridInfo.kChunksPerBlock - 1);
			int localY = (chunkId >> WorldGridInfo.kChunksPerBlockLog2) & (WorldGridInfo.kChunksPerBlock - 1);
			int localX = (chunkId >> (WorldGridInfo.kChunksPerBlockLog2 * 2)) & (WorldGridInfo.kChunksPerBlock - 1);

			worldPosition.x += localX * WorldGridInfo.kChunkSize;
			worldPosition.y += localY * WorldGridInfo.kChunkSize;
			worldPosition.z += localZ * WorldGridInfo.kChunkSize;
		}

		/// <summary>
		/// Returns world position of voxel's origin from its indices.
		/// </summary>
		public void GetVoxelWorldPos(in VectorI3 indices, out Vector3 worldPosition)
		{
			worldPosition = _blocks[indices.x].Chunks[indices.y].WorldPos;

			int localZ = indices.z & (WorldGridInfo.kVoxelsPerChunk - 1);
			int localY = (indices.z >> WorldGridInfo.kVoxelsPerChunkLog2) & (WorldGridInfo.kVoxelsPerChunk - 1);
			int localX = (indices.z >> (WorldGridInfo.kVoxelsPerChunkLog2 * 2)) & (WorldGridInfo.kVoxelsPerChunk - 1);

			worldPosition.x += localX * WorldGridInfo.kVoxelSize;
			worldPosition.y += localY * WorldGridInfo.kVoxelSize;
			worldPosition.z += localZ * WorldGridInfo.kVoxelSize;
		}

		/// <summary>
		/// Returns Y world coordinate of voxel's origin from its indices.
		/// </summary>
		public float GetVoxelWorldPosY(in VectorI3 indices)
		{
			int voxelLocalY = (indices.z >> WorldGridInfo.kVoxelsPerChunkLog2) & (WorldGridInfo.kVoxelsPerChunk - 1);
			int chunkLocalY = (indices.y >> WorldGridInfo.kChunksPerBlockLog2) & (WorldGridInfo.kChunksPerBlock - 1);

			return _blocks[indices.x].Coords.y * WorldGridInfo.kBlockSize + chunkLocalY * WorldGridInfo.kChunkSize + voxelLocalY * WorldGridInfo.kVoxelSize;
		}

		/// <summary>
		/// Fills outChunk array with voxel data from given chunk and a border of 1 voxel filled from neighbouring chunks.
		/// </summary>
		public void GetBorderedChunk(Chunk chunk, ref NativeArray<Voxel> outChunk)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Get bordered chunk");

			int voxelId = 0;
			VectorI3 startIndices = new VectorI3(chunk.Block.Id, chunk.Id, 0);
			FindStartVoxel(ref startIndices, out bool leftSkipped, out bool bottomSkipped, out bool backSkipped);

			VectorI3 currIndices, currColumnIndices, currRowIndices;
			currIndices = currColumnIndices = currRowIndices = startIndices;

			Voxel neighbour = _blocks[currIndices.x].Voxels.Get(currIndices.y, currIndices.z);

			// no left neighbour chunk - skip a column
			if (leftSkipped)
				for (int i = 0; i < Chunk.kColumnBordered; i++)
					outChunk[voxelId++] = default;

			// fill the array
			while (voxelId < Chunk.kTotalVoxelsInBordered)
			{
				// just crossed a column and no bottom neighbour chunk - skip a row
				if (bottomSkipped && voxelId % Chunk.kColumnBordered == 0)
				{
					for (int i = 0; i < Chunk.kRowBordered; i++)
						outChunk[voxelId++] = default;
				}

				// just crossed a row and no back neighbour chunk - skip a voxel
				if (backSkipped && voxelId % Chunk.kRowBordered == 0)
				{
					outChunk[voxelId++] = default;
				}

				if (neighbour.Valid)
				{
					// the start of row in the original chunk
					if ((voxelId - 1) % Chunk.kRowBordered == 0 && currIndices.x == chunk.Block.Id && currIndices.y == chunk.Id)
					{
						// copy whole row
						NativeSlice<Voxel> srcSlice = chunk.Block.Voxels.Slice(WorldGridInfoHelper.ChunkToBlockVoxelId(currIndices.y, currIndices.z), Chunk.kRow);
						NativeSlice<Voxel> dstSlice = outChunk.Slice(voxelId, Chunk.kRow);
						dstSlice.CopyFrom(srcSlice);

						voxelId += Chunk.kRow;
						currIndices.z += Chunk.kRow - 1;
					}
					else
					{
						// copy a single voxel
						outChunk[voxelId++] = neighbour;
					}
				}
				else
				{
					outChunk[voxelId++] = default;
				}

				// one row till the top and no top neighbouring chunk - skip row
				if (((voxelId + Chunk.kRowBordered) % Chunk.kColumnBordered == 0) && chunk.Top == null)
				{
					for (int i = 0; i < Chunk.kRowBordered; i++)
						outChunk[voxelId++] = default;
				}

				AdvanceToNextNeighbour(voxelId, ref neighbour, ref currIndices, ref currRowIndices, ref currColumnIndices);
			}

			UnityEngine.Profiling.Profiler.EndSample();
		}

		#endregion

		#region private methods

		/// <summary>
		/// Try to return block's id from its coordinates.
		/// </summary>
		private bool BlockToBlockId(VectorI3 blockCoords, out int blockId)
		{
			blockId = blockCoords.x * SizeZ * SizeY + blockCoords.y * SizeZ + blockCoords.z;

			return 0 <= blockId && blockId < _blocks.Length;
		}

		/// <summary>
		/// Tries to find indices of a starting voxel of the bordered chunk = voxel 1 left, 1 bottom and 1 back from the first voxel in the original chunk.
		/// </summary>
		private void FindStartVoxel(ref VectorI3 startIndices, out bool leftSkipped, out bool bottomSkipped, out bool backSkipped)
		{
			leftSkipped = bottomSkipped = backSkipped = false;

			// move 1 column left or skip it
			if (TryGetNeighbour(in startIndices, Neighbour.Left, out VectorI3 nIndices).Valid)
				startIndices = nIndices;
			else
				leftSkipped = true;

			// move 1 row down or skip it
			if (TryGetNeighbour(in startIndices, Neighbour.Bottom, out nIndices).Valid)
				startIndices = nIndices;
			else
				bottomSkipped = true;

			// move 1 voxel back or skip it
			if (TryGetNeighbour(in startIndices, Neighbour.Backward, out nIndices).Valid)
				startIndices = nIndices;
			else
				backSkipped = true;
		}

		private void AdvanceToNextNeighbour(int voxelId, ref Voxel neighbour, ref VectorI3 currIndices, ref VectorI3 currRowIndices, ref VectorI3 currColumnIndices)
		{
			VectorI3 nIndices;

			// just crossed a column - advance right
			if (voxelId % Chunk.kColumnBordered == 0)
			{
				neighbour = TryGetNeighbour(in currColumnIndices, Neighbour.Right, out nIndices);
				if (neighbour.Valid)
					currIndices = currColumnIndices = currRowIndices = nIndices;
			}
			// just crossed a row - advance top
			else if (voxelId % Chunk.kRowBordered == 0)
			{
				neighbour = TryGetNeighbour(in currRowIndices, Neighbour.Top, out nIndices);
				if (neighbour.Valid)
					currIndices = currRowIndices = nIndices;
			}
			// advance forward
			else
			{
				neighbour = TryGetNeighbour(in currIndices, Neighbour.Forward, out nIndices);
				if (neighbour.Valid)
					currIndices = nIndices;
			}
		}

		#endregion

#if UNITY_EDITOR

		void OnDrawGizmos()
		{
			if (_blocks != null && (DebugBlockGrid || DebugChunkGrid || DebugVoxelGrid))
				DrawDebugWorld();

			if (_fluidProcessor?.ComponentManager != null && DebugComponents && _fluidProcessor.ComponentManager.Components.Count > 0)
				DrawDebugFluidComponents();
		}

		private void DrawDebugWorld()
		{
			for (int blockId = 0; blockId < _blocks.Length; blockId++)
			{
				if (DebugBlockGrid)
				{
					Vector3 worldPos = _blocks[blockId].WorldPos;
					DrawDebugCube(new Color(1f, 0f, 0f, 0.3f), WorldGridInfo.GetBlockSizeV3(), ref worldPos);
				}

				if (!DebugChunkGrid && !DebugVoxelGrid)
					continue;

				for (int chunkId = 0; chunkId < WorldGridInfo.kTotalChunksInBlock; chunkId++)
				{
					if (DebugChunkGrid)
					{
						Color color = !_blocks[blockId].SimData.UnsettledChunks.Contains(chunkId) ? new Color(0f, 1f, 0f, 0.1f) : new Color(1f, 0f, 0f, 0.1f);
						Vector3 worldPos = _blocks[blockId].Chunks[chunkId].WorldPos;
						DrawDebugCube(color, WorldGridInfo.GetChunkSizeV3(), ref worldPos);
					}

					if (!DebugVoxelGrid)
						continue;

					for (int voxelId = 0; voxelId < WorldGridInfo.kTotalVoxelsInChunk; voxelId++)
					{
						VectorI3 indices = new VectorI3(blockId, chunkId, voxelId);

						ref readonly Voxel voxel = ref GetVoxel(in indices);

						// draw only voxels with fluid
						if (!voxel.Settled && voxel.HasFluid)
						{
							GetVoxelWorldPos(in indices, out Vector3 voxelWorldPos);
							DrawDebugCube(new Color(0f, 0f, 1f, 0.05f), WorldGridInfoHelper.GetVoxelSizeV3, ref voxelWorldPos, voxel.Fluid * Voxel.kByteToFloat, DebugVoxelLabels, voxel.Fluid.ToString());
						}
					}
				}
			}
		}

		private void DrawDebugFluidComponents()
		{
			_fluidProcessor.WaitUntilSimulationComplete();

			Debug.Log("components: " + _fluidProcessor.ComponentManager.Components.Count);

			for (int i = 0; i < _fluidProcessor.ComponentManager.Components.Count; i++)
			{
				FluidComponent component = _fluidProcessor.ComponentManager.Components[i];

				// bounds
				DrawDebugBoundingBox(!component.Settled ? component.DebugColor : Color.grey, component.Bounds);

				// water level
				Vector3 center = new Vector3(component.Bounds.center.x, component.WaterLevel + 0.5f * WorldGridInfo.kVoxelSize, component.Bounds.center.z);
				Vector3 size = new Vector3(component.Bounds.size.x, WorldGridInfo.kVoxelSize, component.Bounds.size.z);
				DrawDebugBoundingBox(Color.red, new Bounds(center, size));

				// segments
				foreach (var segments in component.AllSegments)
				{
					Vector2 row = segments.Key;

					for (int j = 0; j < segments.Value.Count; j++)
					{
						FluidSegment segment = segments.Value[j];

						DrawDebugBoundingBox(component.DebugColor, segment.GetBounds(in row));

						//// voxels
						//foreach (VectorI3 indices in segment.GetIndices(in row, this))
						//{
						//	ref readonly Voxel voxel = ref GetVoxel(in indices, true);

						//	GetVoxelWorldPos(in indices, out Vector3 voxelWorldPos);
						//	DrawDebugCube(component.DebugColor, WorldGridInfoHelper.GetVoxelSizeV3, ref voxelWorldPos, voxel.Fluid * Voxel.kByteToFloat);
						//}
					}
				}

				// outlets
				if (component.Outlets != null)
				{
					foreach (VectorI3 indices in component.Outlets)
					{
						GetVoxelWorldPos(in indices, out Vector3 voxelWorldPos);
						DrawDebugCube(new Color(0.5f, 0f, 0f, 0.1f), WorldGridInfoHelper.GetVoxelSizeV3, ref voxelWorldPos);
					}
				}
			}
		}

		private void DrawDebugCube(Color color, Vector3 size, ref Vector3 worldPos, float fluid = 0, bool labels = false, string label = null)
		{
			// align with meshes
			worldPos -= 0.5f * WorldGridInfoHelper.GetVoxelSizeV3;

			// center
			worldPos += 0.5f * size;

			if (labels)
				UnityEditor.Handles.Label(worldPos, label);

			if (fluid != 0)
			{
				// shift not full voxel's center and size
				worldPos.y -= (1 - fluid) * WorldGridInfo.kVoxelSize * 0.5f;
				size.y = fluid * WorldGridInfo.kVoxelSize;
			}

			Gizmos.color = color;
			Gizmos.DrawCube(worldPos, size);
		}

		private void DrawDebugBoundingBox(Color color, Bounds box)
		{
			box.center -= 0.5f * WorldGridInfoHelper.GetVoxelSizeV3;

			Gizmos.color = color;
			Gizmos.DrawWireCube(box.center, box.size);
		}

#endif

	}
}