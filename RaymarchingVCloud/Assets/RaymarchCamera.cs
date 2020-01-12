using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProceduralNoise;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class RaymarchCamera : SceneViewFilter
{
    [SerializeField]
    private Shader _shader;

    public Material RaymarchMaterial
    {
        get
        {
            if(!_raymarchMat && _shader)
            {
                _raymarchMat = new Material(_shader);
                _raymarchMat.hideFlags = HideFlags.HideAndDontSave;
            }

            return _raymarchMat;
        }
    }

    private Material _raymarchMat;

    public Camera Cam
    {
        get
        {
            if(!_cam)
            {
                _cam = GetComponent<Camera>();
            }
            return _cam;
        }
    }

    private Camera _cam;

    public float cloudLayerBottomHeight;
    public float cloudLayerTopHeight;

    [Range(0,1)]
    public float globalCloudCoverage;
    
    [Range(0, 100)]
    public float globalCloudDensity;
    
    [Range(0.1f, 10000)]
    public float weatherMapScale = 1.0f;
    public float offsetX = 1.0f;
    public float offsetY = 1.0f;

    public Texture2D WeatherMap;
    public Texture2D BlueNoise;

    public Texture3D ShapeNoise
    {
        get
        {
            if(!_shapeNoise)
                _shapeNoise = GenerateShapeNoise();
            
            return _shapeNoise;
        }
    }

    private Texture3D _shapeNoise;

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if(!RaymarchMaterial)
        {
            Graphics.Blit(src, dest);
            return;
        }

        RaymarchMaterial.SetMatrix("_CamFrustum", CalculateCameraFrustum(Cam));
        RaymarchMaterial.SetMatrix("_CamToWorldMatrix", Cam.cameraToWorldMatrix);

        RaymarchMaterial.SetVector("_CloudLayerHeight", new Vector4(cloudLayerBottomHeight, cloudLayerTopHeight, 0, 0));
        RaymarchMaterial.SetVector("_offset", new Vector4(offsetX, offsetY, 0, 0));
        
        RaymarchMaterial.SetFloat("_g_c", globalCloudCoverage);
        RaymarchMaterial.SetFloat("_g_d", globalCloudDensity);
        RaymarchMaterial.SetTexture("_WeatherMap", WeatherMap);
        RaymarchMaterial.SetFloat("_WeatherMapScale", weatherMapScale);
        RaymarchMaterial.SetTexture("_BlueNoise", BlueNoise);

        RaymarchMaterial.SetTexture("_ShapeNoise", ShapeNoise);

        RenderTexture.active = dest;
        RaymarchMaterial.SetTexture("_MainTex", src);

        GL.PushMatrix();
        GL.LoadOrtho();
        RaymarchMaterial.SetPass(0);

        GL.Begin(GL.QUADS);
        
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f);
        
        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f);
        
        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f);
        
        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f);

        GL.End();
        GL.PopMatrix();
    }

    private void OnDrawGizmos() {
        Gizmos.DrawLine(new Vector3(0,cloudLayerBottomHeight, 0), new Vector3(0,cloudLayerBottomHeight, 1000));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerBottomHeight, 1000), new Vector3(0,cloudLayerBottomHeight, 1000));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerBottomHeight, 1000), new Vector3(1000,cloudLayerBottomHeight, 0));
        Gizmos.DrawLine(new Vector3(0,cloudLayerBottomHeight, 0), new Vector3(1000,cloudLayerBottomHeight, 0));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerBottomHeight, 1000), new Vector3(0,cloudLayerBottomHeight, 0));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerBottomHeight, 0), new Vector3(0,cloudLayerBottomHeight, 1000));

        Gizmos.DrawLine(new Vector3(0,cloudLayerTopHeight, 0), new Vector3(0,cloudLayerTopHeight, 1000));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerTopHeight, 1000), new Vector3(0,cloudLayerTopHeight, 1000));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerTopHeight, 1000), new Vector3(1000,cloudLayerTopHeight, 0));
        Gizmos.DrawLine(new Vector3(0,cloudLayerTopHeight, 0), new Vector3(1000,cloudLayerTopHeight, 0));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerTopHeight, 1000), new Vector3(0,cloudLayerTopHeight, 0));
        Gizmos.DrawLine(new Vector3(1000,cloudLayerTopHeight, 0), new Vector3(0,cloudLayerTopHeight, 1000));
    }

    private static Matrix4x4 CalculateCameraFrustum(Camera camera)
    {
        Matrix4x4 frustum = Matrix4x4.identity;
        float fov = Mathf.Tan( (camera.fieldOfView / 2.0f) * Mathf.Deg2Rad );
        
        Vector3 up = Vector3.up * fov;
        Vector3 right = Vector3.right * fov * camera.aspect;

        Vector3 topLeftCorner =     -Vector3.forward + up - right;
        Vector3 topRightCorner =    -Vector3.forward + up + right;
        Vector3 bottomLeftCorner =  -Vector3.forward - up - right;
        Vector3 bottomRightCorner = -Vector3.forward - up + right;

        frustum.SetRow(0, topLeftCorner);
        frustum.SetRow(1, topRightCorner);
        frustum.SetRow(2, bottomRightCorner);
        frustum.SetRow(3, bottomLeftCorner);

        return frustum;
    }

    private static Texture3D GenerateShapeNoise()
    {
        const int SN_SIZE = 256;
        Color[] colorArray = new Color[SN_SIZE * SN_SIZE * SN_SIZE];
        Texture3D texture = new Texture3D (SN_SIZE, SN_SIZE, SN_SIZE, TextureFormat.RGBA32, true);
        
        PerlinNoise perlin = new PerlinNoise(1, 10);
        WorleyNoise mediumWorley = new WorleyNoise(1, 20, 1.0f);
        WorleyNoise highWorley = new WorleyNoise(1, 40, 1.0f);
        WorleyNoise highestWorley = new WorleyNoise(1, 60, 1.0f);

        for (int x = 0; x < SN_SIZE; x++) {
            for (int y = 0; y < SN_SIZE; y++) {
                for (int z = 0; z < SN_SIZE; z++) {
                    Color c = new Color();
                    c.r = perlin.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);
                    c.g = mediumWorley.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);
                    c.b = highWorley.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);
                    c.a = highestWorley.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);

                    colorArray[x + (y * SN_SIZE) + (z * SN_SIZE * SN_SIZE)] = c;
                }
            }
        }

        texture.SetPixels(colorArray);
        texture.Apply();
        return texture;
    }
}
