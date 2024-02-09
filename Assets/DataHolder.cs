using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A data structure to hold the rotation and scale of a cube
/// </summary>
public struct Data
{
    public Vector3 Rotation;
    public float Scale;
        
    public Data(Vector3 rotation, float scale)
    {
        Rotation = rotation;
        Scale = scale;
    }
}

/// <summary>
/// A class to hold the data for the raycaster and the geometry manager
/// Basically it functions as a singleton which handles the rendering of the intersecting cubes
/// Currently the two implementation of the shaders both seem to result in an incorrect result
/// </summary>
public class DataHolder : MonoBehaviour
{
    // The list of data to be rendered
    public readonly List<Data> DataList = new();
    // The compute shader to be used for rendering
    public ComputeShader computeShader;
    
    // The texture to be used for rendering
    public Texture2D texture;
    // The result of the rendering
    private Vector4[] m_Result = new Vector4[256*256];
    
    // The property ids for the compute shader
    private readonly int m_ResultId = Shader.PropertyToID("_Result");
    private readonly int m_DataId = Shader.PropertyToID("_Data");
    
    private readonly int m_ResolutionId = Shader.PropertyToID("_Resolution");
    private readonly int m_CameraPositionId = Shader.PropertyToID("_CameraPos");
    private readonly int m_CameraForwardId = Shader.PropertyToID("_CameraDir");
    private readonly int m_NumCubesId = Shader.PropertyToID("_NumCubes");
    private readonly int m_SmallCubeRotationId = Shader.PropertyToID("_SmallCubeRotation");
    private readonly int m_SmallCubeScaleId = Shader.PropertyToID("_SmallCubeScale");

    // The rotation and scale of the smallest cube
    public Vector3 SmallCubeRotation;
    public float SmallCubeScale;
    
    // The instance of the DataHolder
    private static DataHolder _instance;
    
    // The compute buffer to hold the data
    private ComputeBuffer m_DataBuffer;
    // The compute buffer to hold the result
    private ComputeBuffer m_ResultBuffer;
    
    private AsyncGPUReadbackRequest? m_Request;
    private Camera m_Camera;
    
    private void Start()
    {
        m_Camera = Camera.main;
    }

    private void Awake()
    {
        m_DataBuffer = new ComputeBuffer(72*72*72, sizeof(float) * 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        m_ResultBuffer = new ComputeBuffer(256*256, sizeof(float) * 4, ComputeBufferType.Default);
        _instance = this;
    }
    
    public static DataHolder GetInstance()
    {
        if (_instance == null)
        {
            throw new Exception("DataHolder not initialized");
        }
        return _instance;
    }
    
    private void Update()
    {
        if (m_Request == null || (!m_Request?.done ?? false)) return;
        
        m_Result = m_Request?.GetData<Vector4>().ToArray();
        if (m_Result == null) return;

        for (var i = 0; i < m_Result.Length; i++)
        {
            var x = i % 256;
            var y = i / 256;
            var color = new Color(m_Result[i].x, m_Result[i].y, m_Result[i].z, m_Result[i].w);
            texture.SetPixel(x, y, color);
        }

        texture.Apply();
        
        m_Request = null;
    }

    /// <summary>
    /// Sets up the compute shader and the buffers, then renders the data
    /// </summary>
    private void Render()
    {
        texture = new Texture2D(256, 256, TextureFormat.RGBAFloat, false);
        
        if (DataList.Count > 72*72*72)
        {
            // Throw away elements from the end of the list
            DataList.RemoveRange(72*72*72, DataList.Count - 72*72*72);
        }
        
        m_DataBuffer.SetData(DataList.ToArray());
        
        var kernelId = computeShader.FindKernel("CSMain");
        
        computeShader.SetBuffer(kernelId, m_ResultId, m_ResultBuffer);
        computeShader.SetBuffer(kernelId, m_DataId, m_DataBuffer);
        computeShader.SetVector(m_ResolutionId, new Vector2(256, 256));
        computeShader.SetVector(m_CameraPositionId, m_Camera.transform.position);
        computeShader.SetVector(m_CameraForwardId, m_Camera.transform.forward);
        computeShader.SetInt(m_NumCubesId, DataList.Count);
        computeShader.SetVector(m_SmallCubeRotationId, SmallCubeRotation);
        computeShader.SetFloat(m_SmallCubeScaleId, SmallCubeScale);
        
        computeShader.Dispatch(kernelId, 32, 32, 1);
        
        m_Request = AsyncGPUReadback.Request(m_ResultBuffer);
    }
    
    /// <summary>
    /// Returns the texture, rendering it if force is true
    /// </summary>
    /// <param name="force">If true, the texture will be re-rendered</param>
    /// <returns>The texture</returns>
    public Texture2D GetTexture(bool force = false)
    {
        if (force)
        {
            Render();
        }
        return texture;
    }
    
    /// <summary>
    /// On destroy, release the buffers
    /// </summary>
    private void OnDestroy()
    {
        m_DataBuffer.Release();
        m_DataBuffer = null;
        
        m_ResultBuffer.Release();
        m_ResultBuffer = null;
    }
    
    /// <summary>
    /// Dumps the DataList to a file
    /// It's used to save on the time it takes to calculate the data
    /// </summary>
    public void DumpDataToFile()
    {
        // Dump the DataList to a file in binary format
        var data = new byte[DataList.Count * 16];
        for (var i = 0; i < DataList.Count; i++)
        {
            var bytes = BitConverter.GetBytes(DataList[i].Rotation.x);
            Array.Copy(bytes, 0, data, i * 16, 4);
            bytes = BitConverter.GetBytes(DataList[i].Rotation.y);
            Array.Copy(bytes, 0, data, i * 16 + 4, 4);
            bytes = BitConverter.GetBytes(DataList[i].Rotation.z);
            Array.Copy(bytes, 0, data, i * 16 + 8, 4);
            bytes = BitConverter.GetBytes(DataList[i].Scale);
            Array.Copy(bytes, 0, data, i * 16 + 12, 4);
        }
        System.IO.File.WriteAllBytes("data.bin", data);
    }
    
    /// <summary>
    /// Loads the DataList from a file
    /// </summary>
    public void LoadDataFromFile()
    {
        // Load the DataList from a file in binary format
        var data = System.IO.File.ReadAllBytes("data.bin");
        DataList.Clear();
        for (var i = 0; i < data.Length; i += 16)
        {
            var rotation = new Vector3(
                BitConverter.ToSingle(data, i),
                BitConverter.ToSingle(data, i + 4),
                BitConverter.ToSingle(data, i + 8)
            );
            var scale = BitConverter.ToSingle(data, i + 12);
            DataList.Add(new Data(rotation, scale));
        }
    }
}