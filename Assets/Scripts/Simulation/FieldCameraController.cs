using UnityEngine;

public class FieldCameraController : MonoBehaviour
{
    [SerializeField] private DashboardUIController dashboard;
    [SerializeField, Min(128)] private int textureWidth = 1280;
    [SerializeField, Min(128)] private int textureHeight = 720;
    [SerializeField, Min(1)] private float framingPadding = 1.15f;
    [SerializeField, Min(100)] private float viewDistance = 10000f;
    [SerializeField] private LayerMask visibleLayers = ~(1 << 5);
    private readonly Camera[] cameras = new Camera[5];
    private readonly RenderTexture[] textures = new RenderTexture[5];
    public static readonly string[] ViewNames = { "Overhead", "Front Left", "Front Right", "Rear Left", "Rear Right" };
    public int SelectedView { get; private set; }
    public RenderTexture OutputTexture { get; private set; }
    private bool renderingEnabled = true;

    public void FrameField(Bounds bounds)
    {
        int width = Mathf.Clamp(textureWidth, 128, SystemInfo.maxTextureSize);
        int height = Mathf.Clamp(textureHeight, 128, SystemInfo.maxTextureSize);
        float aspect = width / (float)height;
        float padding = Mathf.Clamp(framingPadding, 1f, 1.03f);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
            {
                var cameraObject = new GameObject("Field Camera - " + ViewNames[i]);
                cameraObject.transform.SetParent(transform, false);
                cameras[i] = cameraObject.AddComponent<Camera>();
            }
            Camera view = cameras[i];
            view.enabled = false;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.12f, 0.16f, 0.12f);
            view.allowHDR = false;
            view.allowMSAA = false;
            if (textures[i] == null || textures[i].width != width || textures[i].height != height)
            {
                ReleaseTexture(i);
                textures[i] = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Field RenderTexture - " + ViewNames[i],
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                textures[i].Create();
            }
            view.targetTexture = textures[i];
            view.aspect = aspect;
            view.cullingMask = visibleLayers;
            view.nearClipPlane = 0.1f;
            view.orthographic = i == 0;
            if (i == 0)
            {
                float distance = Mathf.Max(10, Mathf.Max(bounds.size.x, bounds.size.z));
                view.transform.SetPositionAndRotation(
                    new Vector3(bounds.center.x, bounds.max.y + distance, bounds.center.z),
                    Quaternion.Euler(90, 0, 0));
                view.orthographicSize = Mathf.Max(1, Mathf.Max(bounds.extents.z, bounds.extents.x / aspect) * padding);
                view.farClipPlane = Mathf.Max(viewDistance, distance + bounds.size.y + 100);
            }
            else
            {
                // Front is -Z; left is -X. Fit every bounds corner to the perspective frustum.
                Vector3 direction = new Vector3(i == 1 || i == 3 ? -1 : 1, 1, i <= 2 ? -1 : 1).normalized;
                Quaternion rotation = Quaternion.LookRotation(-direction, Vector3.up);
                Quaternion inverse = Quaternion.Inverse(rotation);
                view.fieldOfView = 45;
                float tanY = Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
                float tanX = tanY * aspect;
                float distance = 1;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1,
                        (corner & 4) == 0 ? -1 : 1));
                    Vector3 local = inverse * offset;
                    float requiredDepth = Mathf.Max(Mathf.Abs(local.x) / tanX, Mathf.Abs(local.y) / tanY);
                    distance = Mathf.Max(distance, requiredDepth * padding - local.z + 1);
                }
                view.transform.SetPositionAndRotation(bounds.center + direction * distance, rotation);
                view.farClipPlane = Mathf.Max(viewDistance, distance + bounds.extents.magnitude + 100);
            }
        }
        if (dashboard == null)
        {
            var dashboards = FindObjectsByType<DashboardUIController>();
            if (dashboards.Length == 1) dashboard = dashboards[0];
        }
        if (dashboard != null) dashboard.BindFieldCameras(this);
        else Debug.LogWarning("Assign a DashboardUIController to display the field cameras.", this);
        SelectView(SelectedView);
    }

    public void SelectView(int index)
    {
        if (index < 0 || index >= cameras.Length) return;
        SelectedView = index;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null) cameras[i].enabled = renderingEnabled && isActiveAndEnabled && i == index;
        OutputTexture = textures[index];
        if (dashboard != null) dashboard.SetCameraTexture(OutputTexture);
    }

    public void SetRenderingEnabled(bool enabled)
    {
        renderingEnabled = enabled;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null) cameras[i].enabled = enabled && isActiveAndEnabled && i == SelectedView;
    }

    private void OnEnable() => SelectView(SelectedView);

    private void OnDisable()
    {
        foreach (Camera view in cameras)
            if (view != null) view.enabled = false;
    }

    private void ReleaseTexture(int index)
    {
        if (cameras[index] != null) cameras[index].targetTexture = null;
        if (textures[index] == null) return;
        if (dashboard != null) dashboard.ClearCameraTexture(textures[index]);
        textures[index].Release();
        Destroy(textures[index]);
        textures[index] = null;
    }

    private void OnDestroy()
    {
        if (dashboard != null) dashboard.UnbindFieldCameras(this);
        for (int i = 0; i < cameras.Length; i++)
        {
            ReleaseTexture(i);
            if (cameras[i] != null) Destroy(cameras[i].gameObject);
        }
        OutputTexture = null;
    }
}
