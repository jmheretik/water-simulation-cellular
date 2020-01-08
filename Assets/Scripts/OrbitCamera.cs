using UnityEngine;
using System.Collections;

/// <summary>
/// A simple camera orbiting around the parent game object.
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    public float MouseSensitivity = 4f;
    public float ScrollSensitivity = 2f;
    public float OrbitDampening = 10f;
    public float ScrollDampening = 6f;

    private float distance = 30f;
    private Vector3 localRotation;

    private GameManager gameManager;

    void Awake()
    {
        gameManager = FindObjectOfType<GameManager>();
        gameManager.orbitCamera = this;
    }

    void Start()
    {
        // orbit around the center of generated world
        Vector3 center = new Vector3(gameManager.world.GetWidth() / 2, gameManager.world.GetHeight() / 2, gameManager.world.GetDepth() / 2);
        transform.parent.Translate(center);

        // zoom out from the world
        distance += gameManager.world.GetDepth() / 2;
    }

    public void UpdateCamera()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        float mouseWheel = Input.GetAxis("Mouse ScrollWheel");

        // rotation of the camera based on mouse coordinates
        if (Input.GetMouseButton(0) && (mouseX != 0 || mouseY != 0))
        {
            localRotation.x += mouseX * MouseSensitivity;
            localRotation.y += -mouseY * MouseSensitivity;
        }

        // zooming input from the mouse wheel
        if (mouseWheel != 0)
        {
            float amount = mouseWheel * ScrollSensitivity * distance * 0.3f;
            distance = Mathf.Clamp(distance - amount, 1.5f, 100);
        }

        // actual camera transformations
        Quaternion quaternion = Quaternion.Euler(localRotation.y, localRotation.x, 0);

        transform.parent.rotation = Quaternion.Lerp(transform.parent.rotation, quaternion, Time.deltaTime * OrbitDampening);

        if (transform.localPosition.z != distance * -1)
        {
            transform.localPosition = new Vector3(0, 0, Mathf.Lerp(transform.localPosition.z, distance * -1, Time.deltaTime * ScrollDampening));
        }
    }
}