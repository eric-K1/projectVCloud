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

    public float shapeNoiseScale = 1.0f;

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

    public Texture3D DetailNoise
    {
        get
        {
            if(!_detailNoise)
                _detailNoise = GenerateDetailNoise();
            
            return _detailNoise;
        }
    }

    private Texture3D _detailNoise;
    public float detailNoiseScale = 1.0f;

    // Lighting parameters
    public float lightingCloudInScatter = 0.2f;
    public float lightingCloudOutScatter = 0.1f;
    public float lightingBeer = 6;
    public float lightingInVsOutScatter = 0.5f;
    public float lightingSilverLiningIntensity = 2.5f;
    public float lightingSilverLiningExponent = 2f;
    public float lightingAmbientOutScatter = 0.9f;
    public float lightingAttuentionClampVal = 0.2f;
    public float lightingMinimumAttenuationAmbient = 0.2f;

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
        RaymarchMaterial.SetFloat("_ShapeNoiseScale", shapeNoiseScale);

        RaymarchMaterial.SetTexture("_DetailNoise", DetailNoise);
        RaymarchMaterial.SetFloat("_DetailNoiseScale", detailNoiseScale);

        // Lighting parameters
        RaymarchMaterial.SetFloat("_cloud_inscatter", lightingCloudInScatter);
        RaymarchMaterial.SetFloat("_cloud_outscatter", lightingCloudOutScatter);
        RaymarchMaterial.SetFloat("_cloud_beer", lightingBeer);
        RaymarchMaterial.SetFloat("_cloud_in_vs_outscatter", lightingInVsOutScatter);
        RaymarchMaterial.SetFloat("_cloud_silver_intensity", lightingSilverLiningIntensity);
        RaymarchMaterial.SetFloat("_cloud_silver_exponent", lightingSilverLiningExponent);
        RaymarchMaterial.SetFloat("_cloud_outscatter_ambient", lightingAmbientOutScatter);
        RaymarchMaterial.SetFloat("_cloud_attuention_clampval", lightingAttuentionClampVal);
        RaymarchMaterial.SetFloat("_cloud_ambient_minimum", lightingMinimumAttenuationAmbient);

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

    public static CloudNoiseGen.NoiseSettings perlinNoiseSettings;
    public static CloudNoiseGen.NoiseSettings worleyNoiseSettings;

    private static Texture3D GenerateShapeNoise()
    {
        const int SN_SIZE = 128;
        Color[] colorArray = new Color[SN_SIZE * SN_SIZE * SN_SIZE];
        Texture3D texture = new Texture3D (SN_SIZE, SN_SIZE, SN_SIZE, TextureFormat.RGBA32, true);
        Texture3D texture1 = new Texture3D (SN_SIZE, SN_SIZE, SN_SIZE, TextureFormat.RGBA32, true);
        Texture3D texture2 = new Texture3D (SN_SIZE, SN_SIZE, SN_SIZE, TextureFormat.RGBA32, true);
        Texture3D texture3 = new Texture3D (SN_SIZE, SN_SIZE, SN_SIZE, TextureFormat.RGBA32, true);
        Texture3D texture4 = new Texture3D (SN_SIZE, SN_SIZE, SN_SIZE, TextureFormat.RGBA32, true);

        perlinNoiseSettings.octaves = 8;
        perlinNoiseSettings.brightness = 1.1f;
        perlinNoiseSettings.periods = 5;
        perlinNoiseSettings.contrast = 1.5f;

        worleyNoiseSettings.octaves = 8;
        worleyNoiseSettings.brightness = 0.5f;
        worleyNoiseSettings.periods = 2;
        worleyNoiseSettings.contrast = 1.5f;
        CloudNoiseGen.perlin = perlinNoiseSettings;
        CloudNoiseGen.worley = worleyNoiseSettings;
        CloudNoiseGen.InitializeNoise(ref texture1, "NoiseMapR", 128, CloudNoiseGen.Mode.LoadAvailableElseGenerate, CloudNoiseGen.NoiseMode.Mix);


        worleyNoiseSettings.octaves = 8;
        worleyNoiseSettings.brightness = 1.2f;
        worleyNoiseSettings.periods = 5;
        worleyNoiseSettings.contrast = 1.5f;
        CloudNoiseGen.perlin = perlinNoiseSettings;
        CloudNoiseGen.worley = worleyNoiseSettings;
        CloudNoiseGen.InitializeNoise(ref texture2, "NoiseMapG", 128, CloudNoiseGen.Mode.LoadAvailableElseGenerate, CloudNoiseGen.NoiseMode.WorleyOnly);


        worleyNoiseSettings.octaves = 8;
        worleyNoiseSettings.brightness = 1.2f;
        worleyNoiseSettings.periods = 10;
        worleyNoiseSettings.contrast = 1.5f;
        CloudNoiseGen.perlin = perlinNoiseSettings;
        CloudNoiseGen.worley = worleyNoiseSettings;
        CloudNoiseGen.InitializeNoise(ref texture3, "NoiseMapB", 128, CloudNoiseGen.Mode.LoadAvailableElseGenerate, CloudNoiseGen.NoiseMode.WorleyOnly);


        worleyNoiseSettings.octaves = 8;
        worleyNoiseSettings.brightness = 1.2f;
        worleyNoiseSettings.periods = 16;
        worleyNoiseSettings.contrast = 1.5f;
        CloudNoiseGen.perlin = perlinNoiseSettings;
        CloudNoiseGen.worley = worleyNoiseSettings;
        CloudNoiseGen.InitializeNoise(ref texture4, "NoiseMapA", 128, CloudNoiseGen.Mode.LoadAvailableElseGenerate, CloudNoiseGen.NoiseMode.WorleyOnly);
        
        for (int x = 0; x < SN_SIZE; x++) {
            for (int y = 0; y < SN_SIZE; y++) {
                for (int z = 0; z < SN_SIZE; z++) {
                    Color c = new Color();
                    c.r = texture1.GetPixel(x, y, z).r;
                    c.g = texture2.GetPixel(x, y, z).r;
                    c.b = texture3.GetPixel(x, y, z).r;
                    c.a = texture4.GetPixel(x, y, z).r;

                    colorArray[x + (y * SN_SIZE) + (z * SN_SIZE * SN_SIZE)] = c;
                }
            }
        }

        texture.SetPixels(colorArray);
        texture.Apply();

        // PerlinNoise perlin = new PerlinNoise(1, 20);
        // WorleyNoise mediumWorley = new WorleyNoise(1, 20, 1.0f);
        // WorleyNoise highWorley = new WorleyNoise(1, 40, 1.0f);
        // WorleyNoise highestWorley = new WorleyNoise(1, 60, 1.0f);

        // for (int x = 0; x < SN_SIZE; x++) {
        //     for (int y = 0; y < SN_SIZE; y++) {
        //         for (int z = 0; z < SN_SIZE; z++) {
        //             Color c = new Color();
        //             c.r = Perlin.Noise(x, y, z);//perlin.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);
        //             c.g = mediumWorley.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);
        //             c.b = highWorley.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);
        //             c.a = highestWorley.Sample3D((float) x/SN_SIZE, (float) y/SN_SIZE, (float) z/SN_SIZE);

        //             colorArray[x + (y * SN_SIZE) + (z * SN_SIZE * SN_SIZE)] = c;
        //         }
        //     }
        // }

        // texture.SetPixels(colorArray);
        // texture.Apply();
        return texture;
    }

    
    private static Texture3D GenerateDetailNoise()
    {
        const int DN_SIZE = 128;
        Color[] colorArray = new Color[DN_SIZE * DN_SIZE * DN_SIZE];
        Texture3D texture = new Texture3D (DN_SIZE, DN_SIZE, DN_SIZE, TextureFormat.RGBA32, true);
        Texture3D texture1 = new Texture3D (DN_SIZE, DN_SIZE, DN_SIZE, TextureFormat.RGBA32, true);
        Texture3D texture2 = new Texture3D (DN_SIZE, DN_SIZE, DN_SIZE, TextureFormat.RGBA32, true);
        Texture3D texture3 = new Texture3D (DN_SIZE, DN_SIZE, DN_SIZE, TextureFormat.RGBA32, true);

        worleyNoiseSettings.octaves = 8;
        worleyNoiseSettings.brightness = 1.2f;
        worleyNoiseSettings.periods = 5;
        worleyNoiseSettings.contrast = 1.5f;
        CloudNoiseGen.perlin = perlinNoiseSettings;
        CloudNoiseGen.worley = worleyNoiseSettings;
        CloudNoiseGen.InitializeNoise(ref texture1, "DetailNoiseR", 32, CloudNoiseGen.Mode.LoadAvailableElseGenerate, CloudNoiseGen.NoiseMode.WorleyOnly);


        worleyNoiseSettings.octaves = 8;
        worleyNoiseSettings.brightness = 1.2f;
        worleyNoiseSettings.periods = 12;
        worleyNoiseSettings.contrast = 1.5f;
        CloudNoiseGen.perlin = perlinNoiseSettings;
        CloudNoiseGen.worley = worleyNoiseSettings;
        CloudNoiseGen.InitializeNoise(ref texture2, "DetailNoiseG", 32, CloudNoiseGen.Mode.LoadAvailableElseGenerate, CloudNoiseGen.NoiseMode.WorleyOnly);


        worleyNoiseSettings.octaves = 8;
        worleyNoiseSettings.brightness = 1.2f;
        worleyNoiseSettings.periods = 16;
        worleyNoiseSettings.contrast = 1.7f;
        CloudNoiseGen.perlin = perlinNoiseSettings;
        CloudNoiseGen.worley = worleyNoiseSettings;
        CloudNoiseGen.InitializeNoise(ref texture3, "DetailNoiseB", 32, CloudNoiseGen.Mode.LoadAvailableElseGenerate, CloudNoiseGen.NoiseMode.WorleyOnly);
        
        for (int x = 0; x < DN_SIZE; x++) {
            for (int y = 0; y < DN_SIZE; y++) {
                for (int z = 0; z < DN_SIZE; z++) {
                    Color c = new Color();
                    c.r = texture1.GetPixel(x, y, z).r;
                    c.g = texture2.GetPixel(x, y, z).r;
                    c.b = texture3.GetPixel(x, y, z).r;

                    colorArray[x + (y * DN_SIZE) + (z * DN_SIZE * DN_SIZE)] = c;
                }
            }
        }

        texture.SetPixels(colorArray);
        texture.Apply();
        return texture;
    }
}
