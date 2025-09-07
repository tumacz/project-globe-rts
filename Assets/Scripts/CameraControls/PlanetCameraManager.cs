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

    [Header("Map Panning Settings")]
    [SerializeField] private float panSpeedU = 0.005f;
    [SerializeField] private float panSpeedV = 0.005f;
    [SerializeField] private float panSmoothTime = 0.1f;

    private Vector3 initialCameraPos;

    // Input Actions
    private InputAction zoomAction;
    private InputAction panAction;

    // Zoom smoothing
    private float targetZoom;
    private float zoomVelocity;

    // Panning (smoothed input + separate velocity ref for SmoothDamp)
    private Vector2 panInput;
    private Vector2 panSmoothed;
    private Vector2 panVelocityRef;

    private Vector2 currentViewCenterUV;
    private float targetViewUvWidth;   // kept for future use
    private float targetViewUvHeight;  // kept for future use

    private void OnEnable()
    {
        SetupInput();
    }

    private void OnDisable()
    {
        zoomAction?.Disable();

        if (panAction != null)
        {
            panAction.performed -= OnPanPerformed;
            panAction.canceled -= OnPanCanceled;
            panAction.Disable();
        }
    }

    private void Start()
    {
        // Replace deprecated FindObjectOfType with FindFirstObjectByType (Unity 6+)
        if (tileGridSpawner == null)
            tileGridSpawner = Object.FindFirstObjectByType<TileGridSpawner>();

        if (mainCamera == null)
            mainCamera = Camera.main; // acceptable fallback; assign explicitly in inspector if possible

        initialCameraPos = transform.position;
        transform.position = initialCameraPos;

        targetZoom = Mathf.Clamp01(currentSimulatedZoom);

        if (tileGridSpawner != null)
        {
            currentViewCenterUV = new Vector2(tileGridSpawner.viewCenterU, tileGridSpawner.viewCenterV);
            targetViewUvWidth = tileGridSpawner.viewUvWidth;
            targetViewUvHeight = tileGridSpawner.viewUvHeight;
        }

        ApplyDeformationParameters();
        UpdateMapAndView();
    }

    private void SetupInput()
    {
        if (!TryGetComponent(out PlayerInput input) || input == null)
        {
            Debug.LogWarning("[PlanetCameraManager] PlayerInput component not found.");
            return;
        }

        var actions = input.actions;
        if (actions == null)
        {
            Debug.LogWarning("[PlanetCameraManager] No InputActionAsset assigned on PlayerInput.");
            return;
        }

        var map = actions.FindActionMap("Camera", throwIfNotFound: false);
        if (map == null)
        {
            Debug.LogWarning("[PlanetCameraManager] Action map 'Camera' not found.");
            return;
        }

        zoomAction = map.FindAction("CameraScroll", throwIfNotFound: false);
        if (zoomAction != null) zoomAction.Enable();
        else Debug.LogWarning("[PlanetCameraManager] Action 'CameraScroll' not found in 'Camera' map. Zoom will not work.");

        panAction = map.FindAction("CameraFly", throwIfNotFound: false);
        if (panAction != null)
        {
            panAction.performed += OnPanPerformed;
            panAction.canceled += OnPanCanceled;
            panAction.Enable();
        }
        else
        {
            Debug.LogWarning("[PlanetCameraManager] Action 'CameraFly' not found in 'Camera' map. Panning will not work.");
        }
    }

    private void OnPanPerformed(InputAction.CallbackContext ctx) => panInput = ctx.ReadValue<Vector2>();
    private void OnPanCanceled(InputAction.CallbackContext ctx) => panInput = Vector2.zero;

    private void Update()
    {
        // Zoom
        float scrollInput = (zoomAction != null) ? zoomAction.ReadValue<float>() : 0f;
        if (Mathf.Abs(scrollInput) > 0.001f)
        {
            targetZoom = Mathf.Clamp(targetZoom + scrollInput * zoomSpeed * Time.deltaTime, minZoom, maxZoom);
        }
        currentSimulatedZoom = Mathf.SmoothDamp(currentSimulatedZoom, targetZoom, ref zoomVelocity, zoomSmoothTime);

        // Pan: smooth input, then integrate into UV center
        panSmoothed = Vector2.SmoothDamp(panSmoothed, panInput, ref panVelocityRef, panSmoothTime);
        currentViewCenterUV.x += panSmoothed.x * panSpeedU * Time.deltaTime;
        currentViewCenterUV.y += panSmoothed.y * panSpeedV * Time.deltaTime;

        // Wrap/Clamp UV
        currentViewCenterUV.x -= Mathf.Floor(currentViewCenterUV.x);
        currentViewCenterUV.y = Mathf.Clamp01(currentViewCenterUV.y);

        ApplyDeformationParameters();
        UpdateMapAndView();
    }

    private void ApplyDeformationParameters()
    {
        if (tileGridSpawner == null) return;

        float heightEval = heightScaleCurve.Evaluate(currentSimulatedZoom);
        float curvatureEval = curvatureCurve.Evaluate(currentSimulatedZoom);

        tileGridSpawner.tileHeightScale = Mathf.Lerp(heightScaleFar, heightScaleNear, heightEval);
        float blendedCurvature = Mathf.Lerp(curvatureDistanceFar, curvatureDistanceNear, curvatureEval);
        tileGridSpawner.globalSphereDistanceFromGrid = blendedCurvature;
        tileGridSpawner.globalUseSphericalDeformation = true;

        tileGridSpawner.UpdateDeformationParameters();
    }

    private void UpdateMapAndView()
    {
        if (tileGridSpawner == null) return;

        tileGridSpawner.SetHeightmapView(
            currentViewCenterUV.x,
            currentViewCenterUV.y,
            tileGridSpawner.viewUvWidth,
            tileGridSpawner.viewUvHeight
        );
    }
}
