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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            uniform sampler2D _CameraDepthTexture;
            uniform float4x4 _CamFrustum, _CamToWorldMatrix;
            uniform float2 _offset;

            uniform float2 _CloudLayerHeight;   // x -- bottom height. y -- top height.
            uniform float _WeatherMapScale;

            uniform float _g_c;      // global cloud coverage
            uniform float _g_d;      // global cloud density
            sampler2D _WeatherMap;
            sampler2D _BlueNoise;
            sampler3D _ShapeNoise;

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

            float calculateCloudProbability(float3 pos, float4 wm, float WMc)
            {
                float ph = saturate(remap(pos.y, _CloudLayerHeight.x, _CloudLayerHeight.y, 0, 1));
                // wh - wm.z; wd - wm.w
                float SRb = saturate(remap(ph, 0, 0.07, 0, 1));
                float SRt = saturate(remap(ph, wm.z * 0.2, wm.z, 1, 0));
                float SA = SRb * SRt;

                float DRb = ph * saturate(remap(ph, 0, 0.15, 0, 1));
                float DRt = saturate(remap(ph, 0.9, 1, 1, 0));
                float DA = _g_d * DRb * DRt * wm.w * 2;

                float4 sn = tex3D(_ShapeNoise, pos);

                float SNsample = remap(sn.r, (sn.g * 0.625 + sn.b * 0.25 + sn.a * 0.125) - 1, 1, 0, 1);

                return saturate(remap(SNsample * SA, 1 - _g_c * WMc, 1, 0, 1)) * DA;
                // return WMc;
            }

            fixed4 raymarching (float3 rayOrigin, float3 rayDirection, float maxDepth, float distFromStart)
            {
                float distanceTraveled = 0;
                static const int RAYMARCHING_STEPS = 128;
                const float STEP_INCREASE_RATE = 0.01;
                const float STEP_SIZE_OUT_OF_CLOUD = 3 + distFromStart * STEP_INCREASE_RATE;
                const float STEP_SIZE_IN_CLOUD = 0.1 + distFromStart * STEP_INCREASE_RATE;

                float accumulatedCloud = 0;
                bool inCloud = false;
                int exitedCloud = 1e20;

                [unroll(RAYMARCHING_STEPS)]
                for(int i = 0; i < RAYMARCHING_STEPS; i++)
                {
                    if(distanceTraveled >= maxDepth)
                    {
                        break; return fixed4(rayDirection, 0);   // w = 0 => ray miss
                    }

                    float3 pos = rayOrigin + rayDirection * distanceTraveled;

                    if(distanceTraveled > 1 && (pos.y < _CloudLayerHeight.x || pos.y > _CloudLayerHeight.y))
                    {
                        break; return fixed4(rayDirection, 0);   // w = 0 => ray miss
                    }
                    
                    float4 wm = tex2D(_WeatherMap, (pos.xz + _offset) / _WeatherMapScale);
                    float WMc = max(wm.x, saturate(_g_c - 0.5) * wm.y * 2);
                    
                    if(inCloud && WMc <= .01 && distanceTraveled - exitedCloud >= STEP_SIZE_OUT_OF_CLOUD)
                    {
                        inCloud = false;
                        exitedCloud = 1e20;
                    }
                    else if(!inCloud && WMc > .01)
                    {
                        inCloud = true;
                        distanceTraveled -= STEP_SIZE_OUT_OF_CLOUD;
                        distanceTraveled = max(distanceTraveled, 0);
                        continue;
                    }
                    else if(inCloud && WMc <= .01)
                    {
                        exitedCloud = distanceTraveled;
                    }

                    accumulatedCloud += calculateCloudProbability(pos, wm, WMc);

                    if (accumulatedCloud >= 1)
                        break;

                    // distanceTraveled += inCloud * STEP_SIZE_IN_CLOUD + (1 - inCloud) * STEP_SIZE_OUT_OF_CLOUD;
                    if(inCloud)
                        distanceTraveled += STEP_SIZE_IN_CLOUD;
                    else
                        distanceTraveled += STEP_SIZE_OUT_OF_CLOUD;
                }
                accumulatedCloud = clamp(accumulatedCloud, 0, 1);
                
                return fixed4(accumulatedCloud,accumulatedCloud,accumulatedCloud, 1);
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
                    // return fixed4(1,0,0,1);
                    return fixed4(sceneColor, 1.0);     // Skip raymarching
                }
                
                float chosenT = 0;
                if(rayOrigin.y < _CloudLayerHeight.x)
                {
                    //return fixed4(1,1,0,1);
                    chosenT = cloudBottomT;
                }
                else if(rayOrigin.y > _CloudLayerHeight.y)
                {
                    //return fixed4(0,0,1,1);
                    chosenT = cloudTopT;
                }
                float blueNoiseChange = tex2D(_BlueNoise, i.uv).x;
                rayOrigin += rayDirection * (chosenT - blueNoiseChange);

                sceneDepth -= length(rayDirection * (chosenT - blueNoiseChange));

                fixed4 result = raymarching(rayOrigin, rayDirection, sceneDepth, chosenT - blueNoiseChange);

                if(result.w == 0)
                    return fixed4(sceneColor, 1);

                return fixed4(1-(1-sceneColor) * (1-result.xyz), 1.0);

                //return fixed4(sceneColor * (1 - result.w) + result.w * result.xyz, 1.0);
            }
            ENDCG
        }
    }
}
