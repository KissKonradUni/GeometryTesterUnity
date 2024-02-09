using System;
using System.Linq;
using Cinemachine;
using UnityEngine;

public class Raycaster : MonoBehaviour
{
    // Different variables to control the how the raycaster works
    private bool m_Started;
    private bool m_AutoStep;
    private bool m_FastStep;
    private bool m_MoveCamera;

    // The mesh, which is used as in inner boundary for the raycaster
    public MeshFilter mesh;
    // The cube that is used as the outer boundary for the raycaster
    public Transform cube;
    // The cube that is used to visualize the last tested cube's ideal scale
    public Transform visCube;

    // The last ray that was used to calculate the scale
    public Vector3 lastRay;
    // The last distance that was used to calculate the scale
    public float lastDistance;
    // The last scale that was calculated
    public float lastScale;

    // The indices for the rotation of the cube
    private int m_XIndex;
    private int m_YIndex;
    private int m_ZIndex;

    // The smallest scale and the indices for the smallest scale
    private Vector3Int m_SmallestIndex;
    private float m_SmallestScale = float.MaxValue;
    
    private DataHolder m_DataHolder;
    
    private Camera m_Camera;
    private CinemachineBrain m_CinemachineBrain;

    private void Start()
    {
        m_DataHolder = DataHolder.GetInstance();
        
        m_Camera = Camera.main;
        if (m_Camera == null)
            throw new Exception("No main camera found");
        m_CinemachineBrain = m_Camera.gameObject.GetComponent<CinemachineBrain>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            m_MoveCamera = !m_MoveCamera;
            m_CinemachineBrain.enabled = m_MoveCamera;
        }
        
        if (m_AutoStep && m_FastStep)
            for (var i = 0; i < 250; i++)
                UpdateLogic();
        else
            UpdateLogic();
    }

    private void UpdateLogic()
    {
        if (!m_Started)
        {
            return;
        }

        // Rotate the cube
        var rotX = m_XIndex * 5.0f;
        var rotY = m_YIndex * 5.0f;
        var rotZ = m_ZIndex * 5.0f;
        cube.localEulerAngles = new Vector3(rotX, rotY, rotZ);
        visCube.localEulerAngles = new Vector3(rotX, rotY, rotZ);
        
        // Allow unity's physics to recalculate the colliders
        Physics.Simulate(0.1f);
        
        // Calculate the scale of the cube
        var scale = CalculateCubeScale();
        visCube.localScale = new Vector3(scale, scale, scale);
        
        // Check if the scale is the smallest
        if (scale < m_SmallestScale)
        {
            m_SmallestScale = scale;
            m_SmallestIndex = new Vector3Int(m_XIndex, m_YIndex, m_ZIndex);
            
            m_DataHolder.SmallCubeScale = scale;
            m_DataHolder.SmallCubeRotation = new Vector3(rotX, rotY, rotZ);
        }
        
        // Update the indices
        m_ZIndex++;
        if (m_ZIndex > 72) // 360 / 5
        {
            m_ZIndex = 0;
            m_YIndex++;
        }
        if (m_YIndex > 72) // 360 / 5
        {
            m_YIndex = 0;
            m_XIndex++;
        }
        if (m_XIndex > 72) // 360 / 5
        {
            m_XIndex = 0;
            m_Started = false;
        }
        
        // Add the data to the data holder
        m_DataHolder.DataList.Add(new Data(new Vector3(rotX, rotY, rotZ), scale));
        
        if (m_AutoStep) return;
        m_Started = false;
    }

    private void OnDrawGizmos()
    {
        if (mesh == null || mesh.sharedMesh == null)
        {
            return;
        }

        Gizmos.color = Color.blue;
        Gizmos.matrix = transform.localToWorldMatrix;
        foreach (var vert in mesh.sharedMesh.vertices)
        {
            Gizmos.DrawSphere(vert, 0.1f);
        }
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(new Vector3(), 0.1f);
        Gizmos.matrix = Matrix4x4.identity;
        
        Gizmos.DrawLine(new Vector3(), lastRay * lastDistance);
        Gizmos.DrawSphere(lastRay * lastDistance, 0.1f);
    }

    private void OnGUI()
    {
        // WindowRect
        var wr = new Rect(20.0f, 20.0f, 200.0f, 475.0f);
        GUI.Box(wr, "Cube-Tetrahedron project");
        GUILayout.BeginArea(new Rect(wr.x + 5.0f, wr.y + 30.0f, wr.width - 10.0f, wr.height - 35.0f));
        
        if (GUILayout.Button("Step"))
        {
            m_Started = true;
        }
        
        GUILayout.BeginHorizontal();
        m_AutoStep = GUILayout.Toggle(m_AutoStep, "Auto Step");
        m_FastStep = GUILayout.Toggle(m_FastStep, "Fast Step");
        GUILayout.EndHorizontal();
        
        m_MoveCamera = GUILayout.Toggle(m_MoveCamera, "Move Camera");
        m_CinemachineBrain.enabled = m_MoveCamera;
        
        GUILayout.Label($"Step: {m_XIndex}, {m_YIndex}, {m_ZIndex}");
        GUILayout.Label($"Last scale: {lastScale}");
        
        GUILayout.Space(5.0f);
        
        GUILayout.Label($"Smallest scale: {m_SmallestScale}");
        GUILayout.Label($"Smallest index: {m_SmallestIndex}");
        
        if (GUILayout.Button("Show smallest"))
        {
            visCube.localEulerAngles = new Vector3(m_SmallestIndex.x * 5.0f, m_SmallestIndex.y * 5.0f, m_SmallestIndex.z * 5.0f);
            visCube.localScale = new Vector3(m_SmallestScale, m_SmallestScale, m_SmallestScale);
        }

        if (GUILayout.Button("Render"))
        {
            m_DataHolder.GetTexture(true);
        }
        
        var texture = m_DataHolder.GetTexture();
        if (texture != null)
            GUILayout.Box(texture, GUILayout.Width(190.0f), GUILayout.Height(190.0f));
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save"))
        {
            m_DataHolder.DumpDataToFile();
        }
        
        if (GUILayout.Button("Load"))
        {
            m_DataHolder.LoadDataFromFile();
        }
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }

    /// <summary>
    /// Calculates the scale of the cube
    /// The method it works is as follows:
    /// <ul>
    /// <li>It gets all the vertices of the mesh</li>
    /// <li>It transforms the vertices to world space</li>
    /// <li>It shoots a ray from the origin towards each vertex</li>
    /// <li>It gets the distance to the first collider it hits</li>
    /// <li>It chooses the vertex that has the smallest distance from the cube</li>
    /// <li>It calculates the scale of the cube where it would fit the vertex</li>
    /// <li>It returns the scale</li>
    /// </ul>
    /// </summary>
    /// <returns>The scale of the cube</returns>
    private float CalculateCubeScale()
    {
        var vertices = mesh.sharedMesh.vertices;
        vertices = vertices.Select(v => transform.TransformPoint(v)).ToArray();

        var min = float.MaxValue;
        var minVert = Vector3.zero;
        foreach (var v in vertices)
        {
            Physics.Raycast(Vector3.zero, v.normalized, out var hit);
            
            if (hit.collider == null) continue;
            
            var distance = hit.distance;
            if (!(distance < min)) continue;
            
            min = distance;
            minVert = v;
        }
        
        lastRay = minVert;
        lastDistance = min;
        
        var vertLength = minVert.magnitude;
        lastScale = vertLength / min;
        
        return lastScale;
    }
}
