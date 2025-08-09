using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlanetCameraManager : MonoBehaviour
{
    [Header("References")]
    public TileGridSpawner tileGridSpawner;
    public Camera mainCamera;

    [Header("Zoom Settings")]
    public float minZoom = 0f;
    public float maxZoom = 1f;
    [Range(0f, 1f)] public float currentSimulatedZoom = 0.5f;
    public float zoomSpeed = 0.5f;
    public float zoomSmoothTime = 0.25f;

    [Header("Zoom Response Curves")]
    public AnimationCurve heightScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve curvatureCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve sphereBlendCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Height Scale Range")]
    public float heightScaleFar = 1f;
    public float heightScaleNear = 5f;

    [Header("Curvature Range")]
    public float curvatureDistanceFar = .5f;
    public float curvatureDistanceNear = 20f;

    [Header("Manual Camera Offset")]
    public float manualCameraZOffset = 8.0f;

    private Vector3 initialCameraPos;

    // Input Actions
    private InputAction zoomAction;
    private InputAction manualOffsetAction;

    // Zoom smoothing
    private float targetZoom;
    private float zoomVelocity;

    private void OnEnable()
    {
        SetupInput();
    }

    private void OnDisable()
    {
        zoomAction?.Disable();
        manualOffsetAction?.Disable();
    }

    private void Start()
    {
        if (tileGridSpawner == null)
            tileGridSpawner = FindObjectOfType<TileGridSpawner>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        initialCameraPos = transform.position;
        targetZoom = currentSimulatedZoom;
        ApplyDeformationParameters();
    }

    private void SetupInput()
    {
        var input = GetComponent<PlayerInput>();
        var map = input.actions.FindActionMap("Camera");

        zoomAction = map.FindAction("CameraScroll");
        manualOffsetAction = map.FindAction("CameraFly");

        zoomAction.Enable();
        manualOffsetAction.Enable();
    }

    private void Update()
    {
        float scrollInput = zoomAction.ReadValue<float>();
        if (Mathf.Abs(scrollInput) > 0.001f)
        {
            targetZoom = Mathf.Clamp01(targetZoom + scrollInput * zoomSpeed * Time.deltaTime);
        }

        currentSimulatedZoom = Mathf.SmoothDamp(currentSimulatedZoom, targetZoom, ref zoomVelocity, zoomSmoothTime);

        Vector2 moveInput = manualOffsetAction.ReadValue<Vector2>();
        if (Mathf.Abs(moveInput.y) > 0.001f)
        {
            manualCameraZOffset += moveInput.y * Time.deltaTime;
        }

        transform.position = initialCameraPos + transform.forward * manualCameraZOffset;

        ApplyDeformationParameters();
    }

    private void ApplyDeformationParameters()
    {
        float heightEval = heightScaleCurve.Evaluate(currentSimulatedZoom);
        float curvatureEval = curvatureCurve.Evaluate(currentSimulatedZoom);
        tileGridSpawner.tileHeightScale = Mathf.Lerp(heightScaleFar, heightScaleNear, heightEval);
        float blendedCurvature = Mathf.Lerp(curvatureDistanceFar, curvatureDistanceNear, curvatureEval);
        tileGridSpawner.globalSphereDistanceFromGrid = blendedCurvature;
        tileGridSpawner.globalUseSphericalDeformation = true;
        tileGridSpawner.UpdateDeformationParameters();
    }
}