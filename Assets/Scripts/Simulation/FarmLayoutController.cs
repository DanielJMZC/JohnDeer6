using System.Collections.Generic;
using UnityEngine;

public class FarmLayoutController : MonoBehaviour
{
    [SerializeField] private Transform buildingsRoot;
    [SerializeField, Min(0)] private float buildingGap = 48f;
    [SerializeField, Min(0)] private float fencePadding = 54f;
    [SerializeField, Min(0)] private float placementEdgePadding = 12f;
    [SerializeField, Min(0)] private float unloadSiloGap = 32f;
    [SerializeField, Min(.1f)] private float fenceThickness = 1.5f;
    [SerializeField, Min(.1f)] private float fenceHeight = 7f;
    private readonly Dictionary<Transform, Vector3> originalScales = new();
    private GameObject fenceRoot;
    private Material fenceMaterial;
    private Transform placementPlane;
    private Bounds placementBounds;
    private float sideGateZ;

    //Posiciona los edificios dentro del campo y genera la cerca alrededor del campo y los edificios. Devuelve el Bounds que contiene todo.
    public Bounds Layout(Bounds field, Vector3? unloadPosition = null)
    {
        sideGateZ = 0;
        if (buildingsRoot == null)
        {
            GameObject found = GameObject.Find("Buildings");
            if (found != null) buildingsRoot = found.transform;
        }
        placementPlane = buildingsRoot != null ? buildingsRoot.Find("Plane") : null;
        if (placementPlane != null && placementPlane.GetComponentInChildren<Renderer>() != null)
            placementBounds = RendererBounds(placementPlane);
        Bounds enclosure = field;
        if (buildingsRoot != null) enclosure.Encapsulate(LayoutBuildings(field, unloadPosition));
        GenerateFence(enclosure);
        enclosure.Expand(fencePadding * 2f + fenceThickness);
        return enclosure;
    }

    private float GroundHeight(Vector3 fallback) => placementPlane != null ? placementBounds.max.y : fallback.y;

    private Bounds LayoutBuildings(Bounds field, Vector3? unloadPosition)
    {
        var children = new List<Transform>();
        foreach (Transform child in buildingsRoot)
        {
            if (child == placementPlane) continue;
            if (!child.gameObject.activeSelf || child.GetComponentInChildren<Renderer>() == null) continue;
            children.Add(child);
            if (!originalScales.ContainsKey(child)) originalScales.Add(child, child.localScale);
            child.localScale = originalScales[child];
        }
        if (children.Count == 0) return field;

        // Turn the structures toward the field and vary their angle slightly like a farmyard.
        float[] facingOffsets = { -8f, 5f, -4f, 8f, 0f };
        for (int i = 0; i < children.Count; i++)
            children[i].rotation = Quaternion.Euler(0, 180f + facingOffsets[i % facingOffsets.Length], 0);

        float planeMinX = placementPlane != null ? placementBounds.min.x + placementEdgePadding : field.min.x - field.size.x;
        float planeMaxX = placementPlane != null ? placementBounds.max.x - placementEdgePadding : field.max.x + field.size.x;
        float planeMinZ = placementPlane != null ? placementBounds.min.z + placementEdgePadding : field.min.z - field.size.z;
        float planeMaxZ = placementPlane != null ? placementBounds.max.z - placementEdgePadding : field.max.z + field.size.z;
        float farmLeft = planeMinX;
        float farmRight = Mathf.Max(farmLeft + 1, field.min.x - buildingGap);
        float farmWidth = farmRight - farmLeft;
        foreach (Transform child in children)
        {
            if (!child.name.ToLowerInvariant().Contains("bigbarn")) continue;
            float barnWidth = Mathf.Max(1, RendererBounds(child).size.x);
            child.localScale = originalScales[child] * Mathf.Min(1, farmWidth / barnWidth);
        }

        Bounds combined = field;
        for (int i = 0; i < children.Count; i++)
        {
            Bounds before = RendererBounds(children[i]);
            string itemName = children[i].name.ToLowerInvariant();
            bool behindField = itemName.Contains("silo") || itemName.Contains("windmill");
            bool isSilo = itemName.Contains("silo");
            bool isBarn = itemName.Contains("bigbarn");
            bool isWell = itemName.Contains("well");
            float x = isSilo && unloadPosition.HasValue
                ? unloadPosition.Value.x - 20f
                : behindField
                ? Mathf.Lerp(field.min.x, field.max.x, itemName.Contains("silo") ? .18f : .72f)
                : isBarn ? field.min.x - buildingGap - before.extents.x : field.min.x - buildingGap - before.extents.x * 1.4f;
            float z = isSilo && unloadPosition.HasValue
                ? unloadPosition.Value.z - unloadSiloGap - before.extents.z
                : behindField
                ? field.max.z + buildingGap + before.extents.z
                : Mathf.Lerp(field.min.z, field.max.z, isBarn ? .62f : .30f);
            x = Mathf.Clamp(x, planeMinX + before.extents.x, planeMaxX - before.extents.x);
            z = Mathf.Clamp(z, planeMinZ + before.extents.z, planeMaxZ - before.extents.z);
            Vector3 center = new(x, before.center.y, z);
            children[i].position += center - before.center;
            Bounds after = RendererBounds(children[i]);
            children[i].position += Vector3.up * (GroundHeight(center) - after.min.y);
            combined.Encapsulate(RendererBounds(children[i]));
            if (isWell) sideGateZ = z;
        }
        if (unloadPosition.HasValue) sideGateZ = unloadPosition.Value.z;
        else if (sideGateZ == 0) sideGateZ = Mathf.Lerp(field.min.z, field.max.z, .2f);
        return combined;
    }

    private void GenerateFence(Bounds content)
    {
        if (fenceRoot != null) Destroy(fenceRoot);
        fenceRoot = new GameObject("Generated Farm Fence");
        fenceRoot.transform.SetParent(transform, false);
        if (fenceMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            fenceMaterial = new Material(shader) { color = new Color(.32f, .18f, .08f) };
        }
        float minX = content.min.x - fencePadding, maxX = content.max.x + fencePadding;
        float minZ = content.min.z - fencePadding, maxZ = content.max.z + fencePadding;
        if (placementPlane != null)
        {
            minX = Mathf.Max(minX, placementBounds.min.x + fenceThickness);
            maxX = Mathf.Min(maxX, placementBounds.max.x - fenceThickness);
            minZ = Mathf.Max(minZ, placementBounds.min.z + fenceThickness);
            maxZ = Mathf.Min(maxZ, placementBounds.max.z - fenceThickness);
        }
        float ground = GroundHeight(new Vector3(minX, 0, sideGateZ)) + .35f;
        float halfGate = Mathf.Clamp(content.size.z * .075f, 9, 21);
        float gateLow = Mathf.Clamp(sideGateZ - halfGate, minZ, maxZ);
        float gateHigh = Mathf.Clamp(sideGateZ + halfGate, minZ, maxZ);
        FenceSide(new(minX, ground, minZ), new(maxX, ground, minZ));
        FenceSide(new(minX, ground, maxZ), new(maxX, ground, maxZ));
        FenceSide(new(minX, ground, minZ), new(minX, ground, gateLow));
        FenceSide(new(minX, ground, gateHigh), new(minX, ground, maxZ));
        FenceSide(new(maxX, ground, minZ), new(maxX, ground, maxZ));
    }

    private void FenceSide(Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length < fenceThickness) return;
        Vector3 middle = (start + end) * .5f;
        bool xAxis = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
        Vector3 rail = xAxis ? new(length, fenceThickness * .55f, fenceThickness) : new(fenceThickness, fenceThickness * .55f, length);
        Part(middle + Vector3.up * (fenceHeight * .28f - rail.y * .5f), rail);
        Part(middle + Vector3.up * (fenceHeight * .72f - rail.y * .5f), rail);
        int posts = Mathf.Max(2, Mathf.CeilToInt(length / 18) + 1);
        for (int i = 0; i < posts; i++) Part(Vector3.Lerp(start, end, i / (float)(posts - 1)), new(fenceThickness, fenceHeight, fenceThickness));
    }

    private void Part(Vector3 basePosition, Vector3 size)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = "Fence Part";
        part.transform.SetParent(fenceRoot.transform, false);
        part.transform.position = basePosition + Vector3.up * size.y * .5f;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = fenceMaterial;
    }

    private static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void OnDestroy() { if (fenceMaterial != null) Destroy(fenceMaterial); }

    public void SetBuildingsVisible(bool visible)
    {
        if (buildingsRoot == null) return;
        foreach (Transform child in buildingsRoot)
            if (child != placementPlane && child.name != "Plane") child.gameObject.SetActive(visible);
    }

    public void SetFenceVisible(bool visible)
    {
        if (fenceRoot != null) fenceRoot.SetActive(visible);
    }
}
