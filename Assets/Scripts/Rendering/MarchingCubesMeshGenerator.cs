using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates triangle mesh from a given voxel grid using Marching Cubes algorithm on CPU or GPU.
/// </summary>
public partial class MarchingCubesMeshGenerator : MonoBehaviour
{
    [Range(1.0f * Voxel.Epsilon / Voxel.MaxVolume, Voxel.MaxVolume / Voxel.MaxVolume)]
    public float isoLevel = 0.5f;

    public Material lavaMaterial;
    public Material waterMaterial;
    public Material terrainMaterial;

    private float lastIsoLevel;

    private World world;

    public void Initialize(World world)
    {
        this.world = world;

        lastIsoLevel = isoLevel;

        InitializeCpuRendering();

        if (gpuFluidRendering)
        {
            InitializeGpuRendering();

            gpuRenderingInitialized = true;
        }
    }

    public bool CheckIsoLevel()
    {
        // isoLevel changed
        if (lastIsoLevel != isoLevel)
        {
            lastIsoLevel = isoLevel;

            if (gpuRenderingInitialized)
            {
                ReinitializeCommandBuffers();
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
