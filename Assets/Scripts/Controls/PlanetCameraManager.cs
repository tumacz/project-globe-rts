//using UnityEngine;
//using UnityEngine.InputSystem;

//[RequireComponent(typeof(PlayerInput))]
//public class PlanetCameraManager : MonoBehaviour
//{
//    [Header("References")]
//    public TileGridSpawner tileGridSpawner;
//    public Camera mainCamera;

//    [Header("Zoom Settings")]
//    public float minZoom = 0f;
//    public float maxZoom = 1f;
//    [Range(0f, 1f)] public float currentSimulatedZoom = 0.5f;
//    public float zoomSpeed = 0.5f;
//    public float zoomSmoothTime = 0.25f;

//    [Header("Zoom Response Curves")]
//    public AnimationCurve heightScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
//    public AnimationCurve curvatureCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
//    public AnimationCurve sphereBlendCurve = AnimationCurve.Linear(0, 0, 1, 1);

//    [Header("Height Scale Range")]
//    public float heightScaleFar = 1f;
//    public float heightScaleNear = 5f;

//    [Header("Curvature Range")]
//    public float curvatureDistanceFar = .5f;
//    public float curvatureDistanceNear = 20f;

//    [Header("Manual Camera Offset")]
//    public float manualCameraZOffset = 8.0f;

//    private Vector3 initialCameraPos;

//    // Input Actions
//    private InputAction zoomAction;
//    //private InputAction manualOffsetAction;

//    // Zoom smoothing
//    private float targetZoom;
//    private float zoomVelocity;

//    private void OnEnable()
//    {
//        SetupInput();
//    }

//    private void OnDisable()
//    {
//        zoomAction?.Disable();
//    }

//    private void Start()
//    {
//        if (tileGridSpawner == null)
//            tileGridSpawner = FindObjectOfType<TileGridSpawner>();

//        if (mainCamera == null)
//            mainCamera = Camera.main;

//        initialCameraPos = transform.position;
//        targetZoom = currentSimulatedZoom;
//        ApplyDeformationParameters();
//    }

//    private void SetupInput()
//    {
//        var input = GetComponent<PlayerInput>();
//        var map = input.actions.FindActionMap("Camera");

//        zoomAction = map.FindAction("CameraScroll");
//        zoomAction.Enable();
//    }

//    private void Update()
//    {
//        float scrollInput = zoomAction.ReadValue<float>();
//        if (Mathf.Abs(scrollInput) > 0.001f)
//        {
//            targetZoom = Mathf.Clamp01(targetZoom + scrollInput * zoomSpeed * Time.deltaTime);
//        }

//        currentSimulatedZoom = Mathf.SmoothDamp(currentSimulatedZoom, targetZoom, ref zoomVelocity, zoomSmoothTime);
//        ApplyDeformationParameters();
//    }

//    private void ApplyDeformationParameters()
//    {
//        float heightEval = heightScaleCurve.Evaluate(currentSimulatedZoom);
//        float curvatureEval = curvatureCurve.Evaluate(currentSimulatedZoom);
//        tileGridSpawner.tileHeightScale = Mathf.Lerp(heightScaleFar, heightScaleNear, heightEval);
//        float blendedCurvature = Mathf.Lerp(curvatureDistanceFar, curvatureDistanceNear, curvatureEval);
//        tileGridSpawner.globalSphereDistanceFromGrid = blendedCurvature;
//        tileGridSpawner.globalUseSphericalDeformation = true;
//        tileGridSpawner.UpdateDeformationParameters();
//    }
//}
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
    [Range(0f, 1f)] public float currentSimulatedZoom = 0.5f; // Ten zakres 0-1 bêdzie u¿ywany do interpolacji deformacji
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
    public float manualCameraZOffset = 8.0f; // To pole nadal nieu¿ywane, ale zachowane

    [Header("Map Panning Settings")]
    [SerializeField] private float panSpeedU = 0.005f; // Prêdkoœæ przesuwania w U (poziom)
    [SerializeField] private float panSpeedV = 0.005f; // Prêdkoœæ przesuwania w V (pion)
    [SerializeField] private float panSmoothTime = 0.1f; // Wyg³adzanie ruchu

    private Vector3 initialCameraPos;

    // Input Actions
    private InputAction zoomAction;
    private InputAction panAction; // Akcja do przesuwania mapy

    // Zoom smoothing
    private float targetZoom;
    private float zoomVelocity;

    // Panning variables
    private Vector2 panInput; // Surowy input z osi
    private Vector2 currentPanVelocity; // Prêdkoœæ do SmoothDamp
    private Vector2 currentViewCenterUV; // Aktualny œrodek widoku UV
    private float targetViewUvWidth; // Docelowa szerokoœæ UV (zale¿na od zoomu)
    private float targetViewUvHeight; // Docelowa wysokoœæ UV (zale¿na od zoomu)


    private void OnEnable()
    {
        SetupInput();
    }

    private void OnDisable()
    {
        zoomAction?.Disable();
        panAction?.Disable();
    }

    private void Start()
    {
        if (tileGridSpawner == null)
            tileGridSpawner = FindObjectOfType<TileGridSpawner>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        initialCameraPos = transform.position; // Kamera pozostaje w sta³ej pozycji
        transform.position = initialCameraPos; // Upewniamy siê, ¿e pozycja jest pocz¹tkowa i sta³a

        targetZoom = currentSimulatedZoom;

        // Inicjalizacja œrodka widoku UV na podstawie domyœlnych wartoœci ze spawnera
        currentViewCenterUV = new Vector2(tileGridSpawner.viewCenterU, tileGridSpawner.viewCenterV);
        targetViewUvWidth = tileGridSpawner.viewUvWidth;
        targetViewUvHeight = tileGridSpawner.viewUvHeight;

        ApplyDeformationParameters(); // Ustawienie pocz¹tkowych parametrów deformacji
        UpdateMapAndView(); // Ustawienie pocz¹tkowego widoku mapy
    }

    private void SetupInput()
    {
        var input = GetComponent<PlayerInput>();
        var map = input.actions.FindActionMap("Camera"); // Zak³adamy istnienie mapy akcji "Camera"

        zoomAction = map.FindAction("CameraScroll"); // Akcja przewijania (do zoomu deformacji)
        if (zoomAction != null)
            zoomAction.Enable();
        else
            Debug.LogWarning("Input Action 'CameraScroll' not found in 'Camera' map. Zoom will not work.");

        panAction = map.FindAction("CameraFly"); // Nowa akcja do przesuwania mapy (np. WSAD/strza³ki)
        if (panAction != null)
            panAction.Enable();
        else
            Debug.LogWarning("Input Action 'Pan' not found in 'Camera' map. Panning will not work.");

        // Obs³uga inputu dla przesuwania
        panAction.performed += ctx => panInput = ctx.ReadValue<Vector2>();
        panAction.canceled += ctx => panInput = Vector2.zero;
    }

    private void Update()
    {
        // --- Obs³uga Zoomu (wp³ywa na deformacjê) ---
        float scrollInput = zoomAction.ReadValue<float>();
        if (Mathf.Abs(scrollInput) > 0.001f)
        {
            // Zoom dzia³a tak samo, steruje currentSimulatedZoom
            targetZoom = Mathf.Clamp01(targetZoom + scrollInput * zoomSpeed * Time.deltaTime);
        }
        currentSimulatedZoom = Mathf.SmoothDamp(currentSimulatedZoom, targetZoom, ref zoomVelocity, zoomSmoothTime);

        // --- Obs³uga Przesuwania Mapy (wp³ywa na fragment UV) ---
        // P³ynne czytanie inputu przesuwania
        currentPanVelocity = Vector2.SmoothDamp(currentPanVelocity, panInput * Time.deltaTime, ref currentPanVelocity, panSmoothTime);

        // Aktualizacja œrodka widoku UV
        currentViewCenterUV.x += currentPanVelocity.x * panSpeedU;
        currentViewCenterUV.y += currentPanVelocity.y * panSpeedV;

        // Ograniczanie, aby œrodek UV zawsze by³ w zakresie [0,1]
        currentViewCenterUV.x = currentViewCenterUV.x - Mathf.Floor(currentViewCenterUV.x); // Wrap U (longitudê)
        currentViewCenterUV.y = Mathf.Clamp01(currentViewCenterUV.y); // Clamp V (szerokoœæ geograficzn¹)

        // Aktualizacja parametrów deformacji i widoku mapy
        ApplyDeformationParameters();
        UpdateMapAndView();
    }

    private void ApplyDeformationParameters()
    {
        if (tileGridSpawner == null) return;

        // Ocenianie krzywych dla wysokoœci i krzywizny na podstawie symulowanego zoomu
        float heightEval = heightScaleCurve.Evaluate(currentSimulatedZoom);
        float curvatureEval = curvatureCurve.Evaluate(currentSimulatedZoom);

        // Interpolacja wartoœci dla TileGridSpawner
        tileGridSpawner.tileHeightScale = Mathf.Lerp(heightScaleFar, heightScaleNear, heightEval);
        float blendedCurvature = Mathf.Lerp(curvatureDistanceFar, curvatureDistanceNear, curvatureEval);
        tileGridSpawner.globalSphereDistanceFromGrid = blendedCurvature;
        tileGridSpawner.globalUseSphericalDeformation = true; // Zawsze true, jeœli to "planeta"

        // Wys³anie aktualizacji do spawnera
        tileGridSpawner.UpdateDeformationParameters();
    }

    // Nowa metoda do aktualizacji widoku mapy w TileGridSpawner
    private void UpdateMapAndView()
    {
        if (tileGridSpawner == null) return;

        // Wartoœci viewUvWidth i viewUvHeight mog¹ byæ równie¿ dynamicznie zmieniane z zoomem,
        // jeœli chcesz, aby "pole widzenia" na mapie zmienia³o siê wraz z zoomem deformacji.
        // Na razie pozostawiamy je na wartoœciach z inspektora TileGridSpawner (lub ustawione rêcznie).
        // Mo¿esz dodaæ tu logikê: targetViewUvWidth = Mathf.Lerp(minUVWidth, maxUVWidth, 1 - currentSimulatedZoom);
        // targetViewUvHeight = Mathf.Lerp(minUVHeight, maxUVHeight, 1 - currentSimulatedZoom);

        // Przekazanie aktualnych wartoœci do TileGridSpawner.SetHeightmapView
        tileGridSpawner.SetHeightmapView(currentViewCenterUV.x, currentViewCenterUV.y, tileGridSpawner.viewUvWidth, tileGridSpawner.viewUvHeight);
    }
}