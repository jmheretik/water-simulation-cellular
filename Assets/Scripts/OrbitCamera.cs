using UnityEngine;
using System.Collections;
using TerrainEngine.Fluid.New;

/// <summary>
/// A simple camera orbiting around the parent game object.
/// </summary>
public class OrbitCamera : MonoBehaviour
{
	public float MouseSensitivity = 4f;
	public float ScrollSensitivity = 2f;
	public float OrbitDampening = 10f;
	public float ScrollDampening = 6f;

	private float _distance = 30f;
	private Vector3 _localRotation;

	public void Initialize(WorldApi worldApi)
	{
		// orbit around the center of generated world
		Vector3 center = new Vector3(worldApi.GetWidth() * 0.5f, worldApi.GetHeight() * 0.5f, worldApi.GetDepth() * 0.5f);
		transform.parent.Translate(center);

		// zoom out from the world
		_distance += worldApi.GetDepth() * 0.5f;
	}

	public void UpdateCamera()
	{
		float mouseX = Input.GetAxis("Mouse X");
		float mouseY = Input.GetAxis("Mouse Y");
		float mouseWheel = Input.GetAxis("Mouse ScrollWheel");

		// rotation of the camera based on mouse coordinates
		if (Input.GetMouseButton(0) && (mouseX != 0 || mouseY != 0))
		{
			_localRotation.x += mouseX * MouseSensitivity;
			_localRotation.y += -mouseY * MouseSensitivity;
		}

		// zooming input from the mouse wheel
		if (mouseWheel != 0)
		{
			float amount = mouseWheel * ScrollSensitivity * _distance * 0.3f;
			_distance = Mathf.Clamp(_distance - amount, 1.5f, 100);
		}

		// actual camera transformations
		Quaternion quaternion = Quaternion.Euler(_localRotation.y, _localRotation.x, 0);

		transform.parent.rotation = Quaternion.Lerp(transform.parent.rotation, quaternion, Time.deltaTime * OrbitDampening);

		if (transform.localPosition.z != _distance * -1)
		{
			transform.localPosition = new Vector3(0, 0, Mathf.Lerp(transform.localPosition.z, _distance * -1, Time.deltaTime * ScrollDampening));
		}
	}
}