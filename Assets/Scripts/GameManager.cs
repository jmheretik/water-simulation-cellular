using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameMode
{
    Fluid,
    Camera,
    Terrain
}

/// <summary>
/// Starts world initialization, handles user input from UI, decides actions and manages other components (processors and generators).
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Required references")]
    public TerrainGenerator terrainGenerator;
    public FluidProcessor fluidProcessor;
    public MarchingCubesMeshGenerator meshGenerator;

    [Header("UI packages")]
    public GameObject mainMenuUI;
    public GameObject sceneUI, eventSystem, canvas;

    [Header("Menu UI elements")]
    public Button startButton;
    public Toggle gpuFluidRenderingToggle;
    public GameObject randomShapeSettings;
    public InputField randomSeedInputField;
    public Text worldSizeXText, worldSizeYText, worldSizeZText, randomFillPercentText, randomSmoothStepsText;

    [Header("Scene UI elements")]
    public Text fluidRadiusText;
    public Text fluidValueText, terrainRadiusText, terrainValueText, isoValueText, componentUpdateIntervalText;
    public Button fluidButton, cameraButton, terrainButton;

    [Header("World generation")]
    public int worldSizeX = 1;
    public int worldSizeY = 1;
    public int worldSizeZ = 1;
    public World world;

    [HideInInspector]
    public OrbitCamera orbitCamera;

    [HideInInspector]
    public GameMode currentMode;
    private GameMode lastMode;
    private ColorBlock activeButtonColorBlock;
    private ColorBlock inactiveButtonColorBlock;

    void Awake()
    {
        // preserve GameManager and UI across different scenes
        DontDestroyOnLoad(this.gameObject);
        DontDestroyOnLoad(eventSystem.gameObject);
        DontDestroyOnLoad(canvas.gameObject);
    }

    void Start()
    {
        terrainGenerator = GetComponent<TerrainGenerator>();
        fluidProcessor = GetComponent<FluidProcessor>();
        meshGenerator = GetComponent<MarchingCubesMeshGenerator>();

        // disable gpu fluid rendering and show error if not supported
        if (SystemInfo.graphicsShaderLevel < 45)
        {
            gpuFluidRenderingToggle.interactable = false;
            gpuFluidRenderingToggle.isOn = false;
            gpuFluidRenderingToggle.GetComponentsInChildren<Text>()[1].enabled = true;
            meshGenerator.gpuFluidRendering = false;
        }

        // scene loaded event
        SceneManager.sceneLoaded += OnSceneFinishedLoading;

        // initial random seed
        terrainGenerator.randomSeed = GetRandomText(UnityEngine.Random.Range(1, 15));
        randomSeedInputField.text = terrainGenerator.randomSeed;

        // save inactiveButtonColorBlock for mode button styles
        activeButtonColorBlock = fluidButton.colors;
        ColorBlock cb = activeButtonColorBlock;
        cb.normalColor = cb.disabledColor;
        inactiveButtonColorBlock = cb;
    }

    void Update()
    {
        if (world == null || !world.terrainLoaded)
            return;

        // update meshes if iso level changed
        if (meshGenerator.CheckIsoLevel())
        {
            world.UpdateAllMeshes();
        }

        // toggle camera mode if ALT is held
        CheckPressedAlt();

        if (currentMode == GameMode.Camera)
        {
            orbitCamera.UpdateCamera();
        }
        else if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            // raycast to terrain and modify fluid/terrain at mouse position 
            HandleUserInput();
        }

        // fluid simulation
        fluidProcessor.FluidUpdate();

        world.TrySettleChunks();

        // update fluid meshes
        if (!world.debugVoxelGrid)
        {
            world.UpdateMeshes(false, false);
            world.UpdateMeshes(false, true);
        }
    }

    private void HandleUserInput()
    {
        bool add = Input.GetMouseButton(0);

        RaycastHit hitInfo;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hitInfo))
        {
            if (currentMode == GameMode.Fluid)
            {
                fluidProcessor.ModifyFluid(hitInfo.point, add);
            }
            else if (currentMode == GameMode.Terrain)
            {
                if (!add && Input.GetKey(KeyCode.R))
                {
                    terrainGenerator.RemoveTerrain(hitInfo.point, fluidProcessor.componentManager);
                }
                else
                {
                    terrainGenerator.ModifyTerrain(hitInfo.point, add, fluidProcessor.componentManager);
                }
            }

            world.UpdateValues();

            if (currentMode == GameMode.Terrain)
            {
                world.UpdateMeshes(true);
            }
        }
    }

    #region button events

    public void LoadScene(string name)
    {
        startButton.interactable = false;
        startButton.GetComponentInChildren<Text>().text = "Loading";

        // load new scene
        SceneManager.LoadScene(name);
    }

    public void ChangeMode(int mode)
    {
        lastMode = currentMode;
        currentMode = (GameMode)mode;

        fluidButton.colors = currentMode == GameMode.Fluid ? activeButtonColorBlock : inactiveButtonColorBlock;
        cameraButton.colors = currentMode == GameMode.Camera ? activeButtonColorBlock : inactiveButtonColorBlock;
        terrainButton.colors = currentMode == GameMode.Terrain ? activeButtonColorBlock : inactiveButtonColorBlock;
    }

    public void GenerateRandomSeedClicked()
    {
        terrainGenerator.randomSeed = GetRandomText(UnityEngine.Random.Range(1, 15));
        randomSeedInputField.text = terrainGenerator.randomSeed;
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    #endregion

    #region toggle and dropdown events

    public void FluidRenderingToggled()
    {
        meshGenerator.gpuFluidRendering = !meshGenerator.gpuFluidRendering;
    }

    public void TerrainShapeDropdownValueChanged(int option)
    {
        terrainGenerator.shape = (TerrainShape)option;

        randomShapeSettings.SetActive(terrainGenerator.shape == TerrainShape.Random);
    }

    public void FluidTypeDropdownValueChanged(int option)
    {
        ChangeMode((int)GameMode.Fluid);
        fluidProcessor.flowViscosity = option == 0 ? FlowViscosity.Water : option == 1 ? FlowViscosity.Lava : 0;
    }

    #endregion

    #region slider events

    // world sliders
    public void WorldSizeXChanged(float value) { worldSizeX = (int)value; worldSizeXText.text = value.ToString(); }
    public void WorldSizeYChanged(float value) { worldSizeY = (int)value; worldSizeYText.text = value.ToString(); }
    public void WorldSizeZChanged(float value) { worldSizeZ = (int)value; worldSizeZText.text = value.ToString(); }

    // terrain sliders
    public void TerrainRadiusChanged(float value) { terrainGenerator.terrainRadius = (int)value; terrainRadiusText.text = value.ToString(); ChangeMode((int)GameMode.Terrain); }
    public void TerrainValueChanged(float value) { terrainGenerator.terrainValue = (byte)value; terrainValueText.text = value.ToString(); ChangeMode((int)GameMode.Terrain); }
    public void RandomFillPercentChanged(float value) { terrainGenerator.randomFillPercent = (int)value; randomFillPercentText.text = value.ToString(); }
    public void RandomSmoothStepsChanged(float value) { terrainGenerator.smoothSteps = (int)value; randomSmoothStepsText.text = value.ToString(); }
    public void RandomSeedChanged(string value) { terrainGenerator.randomSeed = value; }

    // fluid sliders
    public void FluidRadiusChanged(float value) { fluidProcessor.flowRadius = (int)value; fluidRadiusText.text = value.ToString(); ChangeMode((int)GameMode.Fluid); }
    public void FluidValueChanged(float value) { fluidProcessor.flowValue = (byte)value; fluidValueText.text = value.ToString(); ChangeMode((int)GameMode.Fluid); }
    public void IsoValueChanged(float value) { meshGenerator.isoLevel = value; isoValueText.text = value.ToString("0.0"); ChangeMode((int)GameMode.Fluid); }
    public void ComponentUpdateIntervalChanged(float value) { fluidProcessor.componentsUpdateInterval = value; componentUpdateIntervalText.text = value.ToString("0.0"); ChangeMode((int)GameMode.Fluid); }

    #endregion

    #region private methods

    private void CheckPressedAlt()
    {
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            ChangeMode((int)GameMode.Camera);
        }

        if (Input.GetKeyUp(KeyCode.LeftAlt))
        {
            ChangeMode((int)lastMode);
        }
    }

    private void OnSceneFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        // switch UI
        mainMenuUI.SetActive(false);
        sceneUI.SetActive(true);

        // start new scene in camera mode
        ChangeMode((int)GameMode.Camera);
    }

    private string GetRandomText(int length)
    {
        string seed = "";
        string glyphs = "abcdefghijklmnopqrstuvwxyz0123456789";

        for (int i = 0; i < length; i++)
        {
            seed += glyphs[UnityEngine.Random.Range(0, glyphs.Length)];
        }

        return seed;
    }

    #endregion
}
