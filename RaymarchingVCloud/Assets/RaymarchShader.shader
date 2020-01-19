Shader "VCloud/RaymarchShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or sceneDepth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }    // pass for ambient light and first light source

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"   // needed for light color

            sampler2D _MainTex;
            uniform sampler2D _CameraDepthTexture;
            uniform float4x4 _CamFrustum, _CamToWorldMatrix;
            uniform float2 _offset;

            uniform float2 _CloudLayerHeight;   // x -- bottom height. y -- top height.

            uniform float _g_c;      // global cloud coverage
            uniform float _g_d;      // global cloud density

            sampler2D _WeatherMap;
            uniform float _WeatherMapScale;

            sampler2D _BlueNoise;

            sampler3D _ShapeNoise;
            uniform float _ShapeNoiseScale;

            sampler3D _DetailNoise;
            uniform float _DetailNoiseScale;

            // Lighting parameters
            uniform float _cloud_inscatter;
            uniform float _cloud_outscatter;
            uniform float _cloud_beer;
            uniform float _cloud_in_vs_outscatter;
            uniform float _cloud_silver_intensity;
            uniform float _cloud_silver_exponent;
            uniform float _cloud_outscatter_ambient;
            uniform float _cloud_attuention_clampval;
            uniform float _cloud_ambient_minimum;

            // Function parameters

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 ray : TEXCOORD1;

            };

            v2f vert (appdata v)
            {
                v2f o;

                half index = v.vertex.z;
                v.vertex.z = 0;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                o.ray = _CamFrustum[(int) index].xyz;
                o.ray /= abs(o.ray.z);
                o.ray = mul(_CamToWorldMatrix, o.ray);

                return o;
            }

            inline float remap(float val, float lowOld, float highOld, float lowNew, float highNew)
            {
                return lowNew + (val - lowOld) * (highNew - lowNew) / (highOld - lowOld);
            }

            float sampleDensity(float3 pos)
            {
                float4 wm = tex2D(_WeatherMap, (pos.xz + _offset) / _WeatherMapScale);
                float WMc = max(wm.x, saturate(_g_c - 0.5) * wm.y * 2);

                float ph = saturate(remap(pos.y, _CloudLayerHeight.x, _CloudLayerHeight.y, 0, 1));
                // wh - wm.z; wd - wm.w
                float SRb = saturate(remap(ph, 0, 0.07, 0, 1));
                float SRt = saturate(remap(ph, wm.z * 0.2, wm.z, 1, 0));
                float SA = SRb * SRt;

                float DRb = ph * saturate(remap(ph, 0, 0.15, 0, 1));
                float DRt = saturate(remap(ph, 0.9, 1, 1, 0));
                float DA = _g_d * DRb * DRt * wm.w * 2;

                float4 sn = tex3D(_ShapeNoise, pos / _ShapeNoiseScale);
                float SNsample = remap(sn.r, (sn.g * 0.625 + sn.b * 0.25 + sn.a * 0.125) - 1, 1, 0, 1);

                float4 dn = tex3D(_DetailNoise, pos / _DetailNoiseScale);
                float DNfbm = dn.r * 0.625 + dn.g * 0.25 + dn.b * 0.125;
                float DNmod = 0.35 * exp(-_g_c * 0.75) * lerp(DNfbm, 1 - DNfbm, saturate(ph * 5));

                float SNd = saturate(remap(SNsample * SA, 1 - _g_c * WMc, 1, 0, 1));

                float d = saturate(remap(SNd, DNmod, 1, 0, 1)) * DA;
                
                return d;
            }

            float sampleDensityWithoutNoise(float3 pos)
            {
                float4 wm = tex2D(_WeatherMap, (pos.xz + _offset) / _WeatherMapScale);
                float WMc = max(wm.x, saturate(_g_c - 0.5) * wm.y * 2);
                return WMc;

                float ph = saturate(remap(pos.y, _CloudLayerHeight.x, _CloudLayerHeight.y, 0, 1));
                // wh - wm.z; wd - wm.w
                float SRb = saturate(remap(ph, 0, 0.07, 0, 1));
                float SRt = saturate(remap(ph, wm.z * 0.2, wm.z, 1, 0));
                float SA = SRb * SRt;

                float DRb = ph * saturate(remap(ph, 0, 0.15, 0, 1));
                float DRt = saturate(remap(ph, 0.9, 1, 1, 0));
                float DA = _g_d * DRb * DRt * wm.w * 2;

                float4 sn = tex3D(_ShapeNoise, pos / _ShapeNoiseScale);
                float SNsample = remap(sn.r, (sn.g * 0.625 + sn.b * 0.25 + sn.a * 0.125) - 1, 1, 0, 1);
                float SN = saturate(remap(SNsample * SA, 1 - _g_c * WMc, 1, 0, 1)) * DA;
                
                return SN;
            }

            float HG(float cosTheta, float g)
            {
                float g2 = g * g;
                float val = ((1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5)) / 4 * 3.1415;
                return val;
            }
            
            float InOutScatter(float cosTheta)
            {
                float first_hg = HG(cosTheta, _cloud_inscatter);
                float second_hg = _cloud_silver_intensity * pow(saturate(cosTheta), _cloud_silver_exponent);
                float in_scatter_hg = max(first_hg, second_hg);
                float out_scatter_hg = HG(cosTheta, -_cloud_outscatter);
                
                return lerp(in_scatter_hg, out_scatter_hg, _cloud_in_vs_outscatter);
            }
            
            float Attenuation(float densityTowardsSun, float cosTheta)
            {
                float prim = exp(-_cloud_beer * densityTowardsSun);
                float scnd = exp(-_cloud_beer * _cloud_attuention_clampval) * 0.7;
                //reduce clamping while facing the sun
                float checkval = remap(cosTheta, 0.0,1.0,scnd,scnd * 0.5);
                return max(checkval, prim);
            }
            
            float OutScatterAmbient(float density, float percent_height)
            {
                float depth = _cloud_outscatter_ambient  *  pow(density, remap(percent_height, 0.3,0.9,0.5,1.0));
                float vertical = pow(saturate(remap(percent_height, 0.0,0.3,0.8,1.0)),0.8);
                float out_scatter = depth  *  vertical;
                out_scatter = 1.0 - saturate(out_scatter);
                return out_scatter;
            }


            float3 calculateLighting(float3 pos, float density, float densityTowardsSun, float cosTheta, float blueNoise, float dist_along_ray)
            {
                // return _LightColor0;
                // return fixed4(_WorldSpaceLightPos0.xyz,1);
                // return float3(1,1,1);
                float percent_height = saturate(remap(pos.y, _CloudLayerHeight.x, _CloudLayerHeight.y, 0, 1));

                float attenuation_prob = Attenuation(densityTowardsSun, cosTheta);
                float ambient_out_scatter = OutScatterAmbient(density, percent_height);
                
                //Can be calculated once for each march but gave no/tiny perf improvements.
                const float sun_highlight = InOutScatter(cosTheta);
                float attenuation = attenuation_prob * sun_highlight * ambient_out_scatter;
                
                //Ambient min (dist_along_ray used so that far away regions (huge steps) arent calculated (wrongly))
                attenuation = max(density * _cloud_ambient_minimum * (1 -pow(saturate(dist_along_ray/4000), 2)), attenuation);
                
                //combat banding a bit more
                attenuation += blueNoise * 0.003;
                
                float3 ret_color = attenuation * _LightColor0;
                return ret_color;
            }

            fixed4 raymarching (float3 rayOrigin, float3 rayDirection, float3 sceneColor, float maxDepth, float distFromStart, float blueNoise)
            {
                float distanceTraveled = 0;
                static const int RAYMARCHING_STEPS = 64;
                const float STEP_INCREASE_RATE = 0.01;
                const float STEP_SIZE_OUT_OF_CLOUD = 5 + distFromStart * STEP_INCREASE_RATE;
                const float STEP_SIZE_IN_CLOUD = 0.3 + distFromStart * STEP_INCREASE_RATE;
                
                static const int STEPS_NEEDED_TO_EXIT_CLOUD = 3; 

                static const int STEPS_TOWARDS_SUN = 4;
                const float SUN_STEP_SIZE = 0.5 * (_CloudLayerHeight.y - _CloudLayerHeight.x) / STEPS_TOWARDS_SUN;

                float4 accumulatedColor = float4(sceneColor,0);
                bool inCloud = false;
                int exitedCloud = 1e15;

                [unroll(RAYMARCHING_STEPS)]
                for(int i = 0; i < RAYMARCHING_STEPS; i++)
                {
                    if(distanceTraveled >= maxDepth)
                        break;

                    float3 pos = rayOrigin + rayDirection * distanceTraveled;

                    if(distanceTraveled > 1 && (pos.y < _CloudLayerHeight.x || pos.y > _CloudLayerHeight.y))
                        break;
                    
                    float densitySample = sampleDensityWithoutNoise(pos);
                    
                    if(inCloud && densitySample <= .01 && i - exitedCloud >= STEPS_NEEDED_TO_EXIT_CLOUD)
                    {
                        inCloud = false;
                        exitedCloud = 1e15;
                    }
                    else if(!inCloud && densitySample > .01)
                    {
                        inCloud = true;
                        distanceTraveled -= STEP_SIZE_OUT_OF_CLOUD;
                        distanceTraveled = max(distanceTraveled, 0);
                        continue;
                    }
                    else if(inCloud && densitySample <= .01)
                    {
                        exitedCloud = i;
                    }

                    float cloudDensity = sampleDensity(pos);        // aka alpha

                    float densityTowardsSun = 0;
                    float3 directionToSun = _WorldSpaceLightPos0.xyz;

                    for(int j = 0; j < STEPS_TOWARDS_SUN; j++)
                    {
                        float3 posTowardsSun = pos + directionToSun * SUN_STEP_SIZE * j;
                        densityTowardsSun += sampleDensity(posTowardsSun);
                    }

                    float3 currentRGB = calculateLighting(pos, cloudDensity, densityTowardsSun, dot(directionToSun, rayDirection), blueNoise, distanceTraveled);

                    //Alpha mixing, premultiplied: https://en.wikipedia.org/wiki/Alpha_compositing
                    float4 currentColor = float4(currentRGB * cloudDensity, cloudDensity);

                    accumulatedColor = float4(currentColor.rgb + (1-currentColor.a) * accumulatedColor.rgb,
                                            currentColor.a + (1-currentColor.a) * accumulatedColor.a);

                    if (accumulatedColor.a >= 1)
                        break;

                    // distanceTraveled += inCloud * STEP_SIZE_IN_CLOUD + (1 - inCloud) * STEP_SIZE_OUT_OF_CLOUD;
                    if(inCloud)
                        distanceTraveled += STEP_SIZE_IN_CLOUD;
                    else
                        distanceTraveled += STEP_SIZE_OUT_OF_CLOUD;
                }

                return accumulatedColor;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float sceneDepth = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).r);
                sceneDepth *= length(i.ray);

                fixed3 sceneColor = tex2D(_MainTex, i.uv);
                float3 rayDirection = normalize(i.ray.xyz);
                float3 rayOrigin = _WorldSpaceCameraPos;    // Unity builtin

                // If ray is parallel to clouds and not in cloud layer
                if(rayDirection.y == 0  && (rayOrigin.y > _CloudLayerHeight.y || rayOrigin.y < _CloudLayerHeight.x))
                {
                    return fixed4(sceneColor, 1.0);     // Skip raymarching
                }

                // Points on ray are given by the equation: P = rayOrigin + t * rayDirection (t is a scalar)
                // If t < 0 then ray is going "backwards"
                float cloudBottomT  = (_CloudLayerHeight.x - rayOrigin.y) / rayDirection.y;
                float cloudTopT     = (_CloudLayerHeight.y - rayOrigin.y) / rayDirection.y;

                // If looking away from clouds
                if(cloudBottomT < 0 && cloudTopT < 0 )
                {
                    return fixed4(sceneColor, 1.0);     // Skip raymarching
                }
                
                float chosenT = 0;
                if(rayOrigin.y < _CloudLayerHeight.x)
                {
                    chosenT = cloudBottomT;
                }
                else if(rayOrigin.y > _CloudLayerHeight.y)
                {
                    chosenT = cloudTopT;
                }
                float blueNoiseChange = tex2D(_BlueNoise, i.uv).x;
                rayOrigin += rayDirection * (chosenT - blueNoiseChange);


                sceneDepth -= length(rayDirection * (chosenT - blueNoiseChange));

                fixed4 result = raymarching(rayOrigin, rayDirection, sceneColor, sceneDepth, chosenT - blueNoiseChange, blueNoiseChange);

                if(result.w == 0)
                    return fixed4(sceneColor, 1);

                // Screen blend mode
                //return fixed4(1-(1-sceneColor) * (1-result.xyz), 1.0);

                return fixed4(sceneColor * (1 - result.w) + result.w * result.xyz, 1.0);
            }
            ENDCG
        }
    }
}
