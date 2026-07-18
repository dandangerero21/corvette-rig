using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UIMinimapTrack : Graphic
{
    [Header("Track Path Source")]
    public Transform pathRoot;
    public bool closedLoop = true;
    
    [Header("Styling")]
    public float lineWidth = 6f;
    public Color trackColor = new Color(0.2f, 0.8f, 1f, 0.8f); // Neon Cyan
    [Range(0f, 0.2f)] public float paddingFactor = 0.1f; // Margin inside the container
    
    private Vector2[] localPoints;
    private Vector2 worldMin;
    private Vector2 worldMax;
    private Vector2 worldCenter;
    private float scale = 1f;
    
    // Cache waypoints positions to avoid GC Alloc
    private Vector3[] worldPoints;
    
    protected override void Awake()
    {
        base.Awake();
        color = trackColor;
        CalculateBoundariesAndPoints();
    }
    
    protected override void Start()
    {
        base.Start();
        CalculateBoundariesAndPoints();
    }
    
    #if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        color = trackColor;
        CalculateBoundariesAndPoints();
        SetAllDirty();
    }
    #endif

    public void CalculateBoundariesAndPoints()
    {
        if (pathRoot == null || pathRoot.childCount == 0) return;
        
        int count = pathRoot.childCount;
        worldPoints = new Vector3[count];
        
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = pathRoot.GetChild(i).position;
            worldPoints[i] = pos;
            
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.z > maxZ) maxZ = pos.z;
        }
        
        worldMin = new Vector2(minX, minZ);
        worldMax = new Vector2(maxX, maxZ);
        worldCenter = (worldMin + worldMax) * 0.5f;
        
        UpdateScale();
    }
    
    private void UpdateScale()
    {
        if (worldPoints == null || worldPoints.Length == 0) return;
        
        float worldWidth = worldMax.x - worldMin.x;
        float worldHeight = worldMax.y - worldMin.y;
        
        if (worldWidth == 0 || worldHeight == 0) return;
        
        float canvasWidth = rectTransform.rect.width;
        float canvasHeight = rectTransform.rect.height;
        
        // Apply padding
        canvasWidth *= (1f - paddingFactor * 2f);
        canvasHeight *= (1f - paddingFactor * 2f);
        
        float scaleX = canvasWidth / worldWidth;
        float scaleY = canvasHeight / worldHeight;
        scale = Mathf.Min(scaleX, scaleY);
        
        // Generate local 2D points for UI drawing
        int count = worldPoints.Length;
        localPoints = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            localPoints[i] = WorldToMinimapPosition(worldPoints[i]);
        }
    }
    
    public Vector2 WorldToMinimapPosition(Vector3 worldPos)
    {
        Vector2 worldPos2D = new Vector2(worldPos.x, worldPos.z);
        Vector2 offset = worldPos2D - worldCenter;
        return offset * scale;
    }
    
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateScale();
        SetAllDirty();
    }
    
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        
        if (localPoints == null || localPoints.Length < 2) return;
        
        int count = localPoints.Length;
        
        // Calculate tangent-based normals for smooth joints
        Vector2[] normals = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            int prevIdx = (i - 1 + count) % count;
            int nextIdx = (i + 1) % count;
            
            Vector2 dir1 = (localPoints[i] - localPoints[prevIdx]).normalized;
            Vector2 dir2 = (localPoints[nextIdx] - localPoints[i]).normalized;
            
            // Average tangent
            Vector2 tangent = (dir1 + dir2).normalized;
            // Normal is perpendicular to tangent
            normals[i] = new Vector2(-tangent.y, tangent.x).normalized;
        }
        
        // Generate vertices
        for (int i = 0; i < count; i++)
        {
            Vector2 pt = localPoints[i];
            Vector2 norm = normals[i];
            
            Vector2 leftPos = pt + norm * (lineWidth * 0.5f);
            Vector2 rightPos = pt - norm * (lineWidth * 0.5f);
            
            vh.AddVert(new Vector3(leftPos.x, leftPos.y, 0f), color, new Vector2(0f, (float)i / count));
            vh.AddVert(new Vector3(rightPos.x, rightPos.y, 0f), color, new Vector2(1f, (float)i / count));
        }
        
        // Generate indices for triangles (quads)
        int loopCount = closedLoop ? count : count - 1;
        for (int i = 0; i < loopCount; i++)
        {
            int currLeft = i * 2;
            int currRight = i * 2 + 1;
            int nextLeft = ((i + 1) % count) * 2;
            int nextRight = ((i + 1) % count) * 2 + 1;
            
            // First triangle
            vh.AddTriangle(currLeft, currRight, nextLeft);
            // Second triangle
            vh.AddTriangle(currRight, nextRight, nextLeft);
        }
    }
}
