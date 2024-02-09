using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A data structure to hold the vertices and the normal of a side
/// </summary>
public class Side
{
    public readonly Vector3[] Vertices;
    public readonly Vector3 Normal;
    
    public Side(Vector3[] vertices, Vector3 normal)
    {
        Vertices = vertices;
        Normal = normal;
    }
}

/// <summary>
/// A class to manage the geometry of the mesh
/// </summary>
public class GeometryManager : MonoBehaviour
{
    // The mesh to be used for the geometry
    public MeshFilter mesh;
    
    // The list of the centers of the sides
    public List<Vector3> sideCenters = new();
    // The list of the normals of the sides
    public List<Vector3> sideNormals = new();
    
    // The scale of the colliders that will be created on each side
    public float colliderScale = 2.0f;
    // The game object to hold the colliders
    public GameObject colliderHolder;
    
    void Start()
    {
        if (sideCenters.Count == 0)
        {
            CalculateSideCenters();
        }

        for (var index = 0; index < sideCenters.Count; index++)
        {
            var sideCenter = sideCenters[index];
            
            var boxCollider = colliderHolder.AddComponent<BoxCollider>();
            boxCollider.center = sideNormals[index] * (colliderScale * 0.5f) + sideCenter;
            boxCollider.size = new Vector3(colliderScale, colliderScale, colliderScale);
        }
    }

    private void OnDrawGizmos()
    {
        if (mesh == null || mesh.sharedMesh == null)
        {
            return;
        }
        
        if (sideCenters.Count > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.matrix = transform.localToWorldMatrix;
            foreach (var center in sideCenters)
            {
                Gizmos.DrawSphere(center, 0.1f);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {
            CalculateSideCenters();
        }
    }

    /// <summary>
    /// This method calculates the centers of the sides of the mesh.
    /// It works as follows:
    /// <ul>
    /// <li> Get the triangles of the mesh </li>
    /// <li> Calculate the normals of the triangles </li>
    /// <li> Group the triangles by their normals </li>
    /// <li> Calculate the center of the vertices of the triangles </li>
    /// <li> Merge the centers of the triangles based on the previously created groups </li>
    /// <li> Add the centers and the normals to the lists </li>
    /// </ul>
    /// </summary>
    private void CalculateSideCenters()
    {
        var currentMesh = mesh.sharedMesh;
            
        var triangles = currentMesh.GetTriangles(0);

        // Calculate the normals of the triangles
        var normals = new Vector3[triangles.Length / 3];
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var normal = Vector3.Cross(
                currentMesh.vertices[triangles[i + 1]] - currentMesh.vertices[triangles[i]],
                currentMesh.vertices[triangles[i + 2]] - currentMesh.vertices[triangles[i]]
            ).normalized;
            normals[i / 3] = normal;
        }
            
        var triangleTriplets = new List<Vector3[]>();
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var triangle = new Vector3[3];
            for (var j = 0; j < 3; j++)
            {
                triangle[j] = currentMesh.vertices[triangles[i + j]];
            }
            triangleTriplets.Add(triangle);
        }
            
        var triangleSides = new List<Side>();
        for (var i = 0; i < triangleTriplets.Count; i++)
        {
            var triangle = triangleTriplets[i];
            var normal = normals[i];

            var side = new Side(triangle, normal);
                
            triangleSides.Add(side);
        }
            
        var sameNormal = triangleSides.GroupBy(side => side.Normal).ToList();
        foreach (var group in sameNormal)
        {
            var vertices = group.SelectMany(side => side.Vertices).Distinct().ToArray();
            var center = vertices.Aggregate(Vector3.zero, (acc, vertex) => acc + vertex) / vertices.Length;
                
            sideCenters.Add(center);
            sideNormals.Add(group.Key);
        }
    }
}