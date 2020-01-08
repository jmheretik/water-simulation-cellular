using System;
using System.Collections;
using System.Collections.Generic;
using TerrainEngine.Fluid.New;
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
	public FluidProcessor FluidProcessor;
	public TerrainGenerator TerrainGenerator;
	public MarchingCubesMeshGenerator MeshGenerator;
	public World World;
	public WorldApi WorldApi;
	public bool SkipMainMenu = false;

	[Header("UI packages")]
	public GameObject MainMenuUI;
	public GameObject SceneUI, EventSystem;

	[Header("Menu UI elements")]
	public Button StartButton;
	public Toggle GpuFluidRenderingToggle;
	public GameObject RandomShapeSettings;
	public InputField RandomSeedInputField;
	public Text WorldSizeXText, WorldSizeYText, WorldSizeZText, RandomFillPercentText, RandomSmoothStepsText;

	[Header("Scene UI elements")]
	public Text FluidRadiusText;
	public Text FluidValueText, TerrainRadiusText, TerrainValueText, IsoValueText;
	public Button FluidButton, CameraButton, TerrainButton;

	private OrbitCamera _orbitCamera;
	private GameMode _currentMode;
	private GameMode _lastMode;
	private ColorBlock _activeButtonColorBlock;
	private ColorBlock _inactiveButtonColorBlock;
	private bool _worldLoaded = false;
	private bool _disposing = false;

	#region initialization

	void Start()
	{
		// preserve GameManager across different scenes
		DontDestroyOnLoad(this.gameObject);

		// disable gpu fluid rendering and show error if not supported
		if (SystemInfo.graphicsShaderLevel < 45)
		{
			GpuFluidRenderingToggle.interactable = false;
			GpuFluidRenderingToggle.isOn = false;
			GpuFluidRenderingToggle.GetComponentsInChildren<Text>()[1].enabled = true;
			MeshGenerator.GpuFluidRendering = false;
		}

		// scene loaded event
		SceneManager.sceneLoaded += OnSceneFinishedLoading;

		// initial random seed
		if (String.IsNullOrEmpty(TerrainGenerator.RandomSeed))
		{
			TerrainGenerator.RandomSeed = GetRandomText(UnityEngine.Random.Range(1, 15));
			RandomSeedInputField.text = TerrainGenerator.RandomSeed;
		}

		// save inactiveButtonColorBlock for mode button styles
		_activeButtonColorBlock = FluidButton.colors;
		ColorBlock cb = _activeButtonColorBlock;
		cb.normalColor = cb.disabledColor;
		_inactiveButtonColorBlock = cb;

#if UNITY_EDITOR
		//Debug.Log("size of voxel:" + System.Runtime.InteropServices.Marshal.SizeOf(typeof(Voxel)));
		//Application.targetFrameRate = 300;

		if (SkipMainMenu)
		{
			LoadScene("scene");
		}
#endif
	}

	#endregion

	void Update()
	{
		if (!_worldLoaded || _disposing)
			return;

		// update meshes if iso level changed
		if (MeshGenerator.CheckIsoLevel())
		{
			WorldApi.UpdateAllMeshes();
		}

		// toggle camera mode if ALT is held
		CheckPressedAlt();

		if (_currentMode == GameMode.Camera)
		{
			_orbitCamera.UpdateCamera();
		}
		else
		{
			ProcessUserInput();
		}

		// fluid simulation
		UnityEngine.Profiling.Profiler.BeginSample("FluidUpdate");
		FluidProcessor.FluidUpdate();
		UnityEngine.Profiling.Profiler.EndSample();
	}

	/// <summary>
	/// If mouse buttons are clicked - raycast to terrain and modify fluid/terrain at mouse position.
	/// </summary>
	private void ProcessUserInput()
	{
		if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

			if (Physics.Raycast(ray, out RaycastHit hitInfo))
			{
				bool add = Input.GetMouseButton(0);

				FluidProcessor.WaitUntilSimulationComplete();

				if (_currentMode == GameMode.Fluid)
				{
					FluidProcessor.ModifyFluid(hitInfo.point, add);
				}
				else if (_currentMode == GameMode.Terrain)
				{
					if (!add && Input.GetKey(KeyCode.R))
					{
						TerrainGenerator.RemoveTerrain(hitInfo.point);
					}
					else
					{
						TerrainGenerator.ModifyTerrain(hitInfo.point, add);
					}

					// update solid meshes
					WorldApi.UpdateUnsettledMeshes(true);
				}

				// stop the rebuild of components while the terrain is being modified or fluid subtracted
				FluidProcessor.ComponentManager.RebuildEnabled = !(_currentMode == GameMode.Terrain || !add);
			}
		}
		else
		{
			// start the rebuild of components once the modifications are done
			FluidProcessor.ComponentManager.RebuildEnabled = true;
		}
	}

	private void OnDestroy()
	{
		_disposing = true;

		FluidProcessor.Dispose();
		MeshGenerator.Dispose();
		World.Dispose();
	}

	#region button events

	public void LoadScene(string name)
	{
		StartButton.interactable = false;
		StartButton.GetComponentInChildren<Text>().text = "Loading";

		// load new scene
		SceneManager.LoadScene(name);
	}

	public void ChangeMode(int mode)
	{
		_lastMode = _currentMode;
		_currentMode = (GameMode)mode;

		FluidButton.colors = _currentMode == GameMode.Fluid ? _activeButtonColorBlock : _inactiveButtonColorBlock;
		CameraButton.colors = _currentMode == GameMode.Camera ? _activeButtonColorBlock : _inactiveButtonColorBlock;
		TerrainButton.colors = _currentMode == GameMode.Terrain ? _activeButtonColorBlock : _inactiveButtonColorBlock;
	}

	public void GenerateRandomSeedClicked()
	{
		TerrainGenerator.RandomSeed = GetRandomText(UnityEngine.Random.Range(1, 15));
		RandomSeedInputField.text = TerrainGenerator.RandomSeed;
	}

	public void ExitGame()
	{
		Application.Quit();
	}

	#endregion

	#region toggle and dropdown events

	public void FluidRenderingToggled()
	{
		MeshGenerator.GpuFluidRendering = !MeshGenerator.GpuFluidRendering;
	}

	public void TerrainShapeDropdownValueChanged(int option)
	{
		TerrainGenerator.Shape = (TerrainShape)option;

		RandomShapeSettings.SetActive(TerrainGenerator.Shape == TerrainShape.Random);
	}

	public void FluidTypeDropdownValueChanged(int option)
	{
		ChangeMode((int)GameMode.Fluid);
		FluidProcessor.FlowViscosity = option == 0 ? Viscosity.Water : option == 1 ? Viscosity.Lava : 0;
	}

	#endregion

	#region slider events

	// world sliders
	public void WorldSizeXChanged(float value)
	{
		WorldApi.SizeX = (int)value;
		WorldSizeXText.text = value.ToString();
	}
	public void WorldSizeYChanged(float value)
	{
		WorldApi.SizeY = (int)value;
		WorldSizeYText.text = value.ToString();
	}
	public void WorldSizeZChanged(float value)
	{
		WorldApi.SizeZ = (int)value;
		WorldSizeZText.text = value.ToString();
	}

	// terrain sliders
	public void TerrainRadiusChanged(float value)
	{
		TerrainGenerator.TerrainRadius = (int)value;
		TerrainRadiusText.text = value.ToString();
		ChangeMode((int)GameMode.Terrain);
	}
	public void TerrainValueChanged(float value)
	{
		TerrainGenerator.TerrainValue = (byte)value;
		TerrainValueText.text = value.ToString();
		ChangeMode((int)GameMode.Terrain);
	}
	public void RandomFillPercentChanged(float value)
	{
		TerrainGenerator.RandomFillPercent = (int)value;
		RandomFillPercentText.text = value.ToString();
	}
	public void RandomSmoothStepsChanged(float value)
	{
		TerrainGenerator.SmoothSteps = (int)value;
		RandomSmoothStepsText.text = value.ToString();
	}
	public void RandomSeedChanged(string value)
	{
		TerrainGenerator.RandomSeed = value;
	}

	// fluid sliders
	public void FluidRadiusChanged(float value)
	{
		FluidProcessor.FlowRadius = (int)value;
		FluidRadiusText.text = value.ToString();
		ChangeMode((int)GameMode.Fluid);
	}
	public void FluidValueChanged(float value)
	{
		FluidProcessor.FlowValue = (byte)value;
		FluidValueText.text = value.ToString();
		ChangeMode((int)GameMode.Fluid);
	}
	public void IsoValueChanged(float value)
	{
		MeshGenerator.IsoLevel = value;
		IsoValueText.text = value.ToString("0.0");
		ChangeMode((int)GameMode.Fluid);
	}

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
			ChangeMode((int)_lastMode);
		}
	}

	private void OnSceneFinishedLoading(Scene scene, LoadSceneMode mode)
	{
		// switch UI
		MainMenuUI.SetActive(false);
		SceneUI.SetActive(true);

		InitializeWorld();

		_orbitCamera = FindObjectOfType<OrbitCamera>();
		_orbitCamera.Initialize(WorldApi);

		// start new scene in camera mode
		ChangeMode((int)GameMode.Camera);
	}

	private void InitializeWorld()
	{
		FluidProcessor.Initialize();
		TerrainGenerator.Initialize();
		MeshGenerator.Initialize();

		World.Initialize();
		World.LoadTerrain(TerrainGenerator);
		WorldApi.UpdateAllMeshes();

		_worldLoaded = true;
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
