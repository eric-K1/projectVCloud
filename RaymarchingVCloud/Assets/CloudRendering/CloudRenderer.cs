using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CloudRendering 
{
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public class CloudRenderer : SceneViewFilter
    {
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

        [System.Serializable]
        public class CloudLayer 
        {
            public float topHeight = 1000;
            public float bottomHeight = 400;
        }

        public CloudLayer cloudLayer;

        [Range(0,1)]
        public float globalCloudCoverage = 0.5f;
        
        [Range(0, 10)]
        public float globalCloudDensity = 1f;

        [System.Serializable]
        public class TextureScaleAndOffset
        {
            public Vector2 offset;
            public float scale = 100;
        }

        public TextureScaleAndOffset weatherMapST;

        public float shapeNoiseScale = 3000.0f;

        public Texture2D WeatherMap;
        public Texture2D BlueNoise
        {
            get
            {
                if(!_blueNoise)
                    _blueNoise = Resources.Load<Texture2D>("CloudRendering/blueNoiseTex");
                
                return _blueNoise;
            }
        }

        private Texture2D _blueNoise;

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
        public float detailNoiseScale = 100.0f;

        [System.Serializable]
        public class LightingParameters
        {
            [Range(0,1)]
            public float cloudInScatter = 0.2f;

            [Range(0,1)]
            public float cloudOutScatter = 0.1f;
            
            [Range(0,300)]
            public float beer = 6;
            
            [Range(0,1)]
            public float inVsOutScatter = 0.5f;
            
            [Range(0,100)]
            public float silverLiningIntensity = 2.5f;
            
            [Range(0,100)]
            public float silverLiningExponent = 2f;
            
            [Range(0,1)]
            public float ambientOutScatter = 0.9f;
            
            [Range(0,1)]
            public float attuentionClampVal = 0.2f;
            
            [Range(0,1)]
            public float minimumAttenuationAmbient = 0.2f;
        }

        public LightingParameters lightingParameters;

        [Range(0,0.5f)]
        public float stepIncreaseRate = 0.01f;

    
        public float stepSizeInsideCloud = 1.5f;
        
    
        public float stepSizeOutsideCloud = 20f; 
    

        public bool gizmosEnabled = false; 

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if(!_shader)
                _shader = Shader.Find("CloudRendering/RaymarchShader");
            
            if(!RaymarchMaterial)
            {
                Graphics.Blit(src, dest);
                return;
            }

            RaymarchMaterial.SetMatrix("_CamFrustum", CalculateCameraFrustum(Cam));
            RaymarchMaterial.SetMatrix("_CamToWorldMatrix", Cam.cameraToWorldMatrix);

            RaymarchMaterial.SetVector("_CloudLayerHeight", new Vector4(cloudLayer.bottomHeight, cloudLayer.topHeight, 0, 0));

            
            RaymarchMaterial.SetFloat("_g_c", globalCloudCoverage);
            RaymarchMaterial.SetFloat("_g_d", globalCloudDensity);

            RaymarchMaterial.SetTexture("_WeatherMap", WeatherMap);
            RaymarchMaterial.SetVector("_WeatherMap_offset", weatherMapST.offset);
            RaymarchMaterial.SetFloat("_WeatherMap_scale", weatherMapST.scale);

            RaymarchMaterial.SetTexture("_BlueNoise", BlueNoise);

            RaymarchMaterial.SetTexture("_ShapeNoise", ShapeNoise);
            RaymarchMaterial.SetFloat("_ShapeNoiseScale", shapeNoiseScale);

            RaymarchMaterial.SetTexture("_DetailNoise", DetailNoise);
            RaymarchMaterial.SetFloat("_DetailNoiseScale", detailNoiseScale);

            // Lighting parameters
            RaymarchMaterial.SetFloat("_cloud_inscatter", lightingParameters.cloudInScatter);
            RaymarchMaterial.SetFloat("_cloud_outscatter", lightingParameters.cloudOutScatter);
            RaymarchMaterial.SetFloat("_cloud_beer", lightingParameters.beer);
            RaymarchMaterial.SetFloat("_cloud_in_vs_outscatter", lightingParameters.inVsOutScatter);
            RaymarchMaterial.SetFloat("_cloud_silver_intensity", lightingParameters.silverLiningIntensity);
            RaymarchMaterial.SetFloat("_cloud_silver_exponent", lightingParameters.silverLiningExponent);
            RaymarchMaterial.SetFloat("_cloud_outscatter_ambient", lightingParameters.ambientOutScatter);
            RaymarchMaterial.SetFloat("_cloud_attuention_clampval", lightingParameters.attuentionClampVal);
            RaymarchMaterial.SetFloat("_cloud_ambient_minimum", lightingParameters.minimumAttenuationAmbient);

            
            RaymarchMaterial.SetFloat("_step_increase_rate", stepIncreaseRate);
            RaymarchMaterial.SetFloat("_step_size_inside_cloud", stepSizeInsideCloud);
            RaymarchMaterial.SetFloat("_step_size_outside_cloud", stepSizeOutsideCloud);

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
            if(!gizmosEnabled)
                return;
            
            Gizmos.DrawLine(new Vector3(0,cloudLayer.bottomHeight, 0), new Vector3(0,cloudLayer.bottomHeight, 1000));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.bottomHeight, 1000), new Vector3(0,cloudLayer.bottomHeight, 1000));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.bottomHeight, 1000), new Vector3(1000,cloudLayer.bottomHeight, 0));
            Gizmos.DrawLine(new Vector3(0,cloudLayer.bottomHeight, 0), new Vector3(1000,cloudLayer.bottomHeight, 0));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.bottomHeight, 1000), new Vector3(0,cloudLayer.bottomHeight, 0));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.bottomHeight, 0), new Vector3(0,cloudLayer.bottomHeight, 1000));

            Gizmos.DrawLine(new Vector3(0,cloudLayer.topHeight, 0), new Vector3(0,cloudLayer.topHeight, 1000));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.topHeight, 1000), new Vector3(0,cloudLayer.topHeight, 1000));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.topHeight, 1000), new Vector3(1000,cloudLayer.topHeight, 0));
            Gizmos.DrawLine(new Vector3(0,cloudLayer.topHeight, 0), new Vector3(1000,cloudLayer.topHeight, 0));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.topHeight, 1000), new Vector3(0,cloudLayer.topHeight, 0));
            Gizmos.DrawLine(new Vector3(1000,cloudLayer.topHeight, 0), new Vector3(0,cloudLayer.topHeight, 1000));
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
}
