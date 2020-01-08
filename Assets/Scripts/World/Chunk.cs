using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class Chunk : MonoBehaviour
{
    public const int Row = WorldGridInfo.kVoxelsPerChunk;
    public const int Column = WorldGridInfo.kVoxelsPerChunk * WorldGridInfo.kVoxelsPerChunk;
    public const int Offset = 1 - WorldGridInfo.kVoxelsPerChunk;

    public Block block;

    public int id;

    public Chunk top;
    public Chunk bottom;
    public Chunk forward;
    public Chunk backward;
    public Chunk right;
    public Chunk left;

    public bool settled;
    public bool hasFluid;

    // gpu rendering data
    public CommandBuffer waterCommandBuffer;
    public CommandBuffer lavaCommandBuffer;
    public ComputeBuffer waterBuffer;
    public ComputeBuffer lavaBuffer;

    public void Initialize(Block block, int id)
    {
        this.transform.parent = block.transform;
        this.block = block;
        this.id = id;

        // cpu rendering data
        GameObject solidMeshGo = new GameObject("solid mesh");
        solidMeshGo.transform.parent = this.transform;
        solidMeshGo.AddComponent<MeshFilter>();   // to be passed into mesh generator
        solidMeshGo.AddComponent<MeshRenderer>();
        solidMeshGo.AddComponent<MeshCollider>(); // used for mouse interaction with the scene (raycasting)

        GameObject lavaMeshGo = new GameObject("lava mesh");
        lavaMeshGo.transform.parent = this.transform;
        lavaMeshGo.AddComponent<MeshFilter>();
        lavaMeshGo.AddComponent<MeshRenderer>();

        GameObject fluidMeshGo = new GameObject("fluid mesh");
        fluidMeshGo.transform.parent = this.transform;
        fluidMeshGo.AddComponent<MeshFilter>();
        fluidMeshGo.AddComponent<MeshRenderer>();
    }

    public bool HasSettledNeighbours()
    {
        return (top == null || top.settled) &&
            (bottom == null || bottom.settled) &&
            (forward == null || forward.settled) &&
            (backward == null || backward.settled) &&
            (right == null || right.settled) &&
            (left == null || left.settled);
    }
}
