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
            uniform float _maxDistance;
            uniform float2 _CloudLayerHeight;   // x -- bottom height. y -- top height.

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

            float signedDistanceSphere(float3 pos, float r)
            {
                return length(pos) - r;
            }

            float distanceField(float3 position)
            {
                float sphere1 = signedDistanceSphere(position - float3(0,0,0), 1);

                return sphere1;
            }

            fixed4 raymarching (float3 rayOrigin, float3 rayDirection, float maxDepth)
            {
                fixed4 result = fixed4(1,1,1,1);
                const int maxIteration = 64;
                float distanceTraveled = 0.0f;

                

                for(int i = 0; i < maxIteration; i++)
                {
                    if(distanceTraveled > _maxDistance || distanceTraveled >= maxDepth)
                    {
                        result = fixed4(rayDirection, 0);   // w = 0 => ray miss
                        break;
                    }

                    float3 pos = rayOrigin + rayDirection * distanceTraveled;

                    if(i != 0 && (pos.y < _CloudLayerHeight.x || pos.y > _CloudLayerHeight.y))
                    {
                        result = fixed4(rayDirection, 0);   // w = 0 => ray miss
                        break;
                    }

                    float distanceToHit = distanceField(pos);

                    if(distanceToHit < 0.01)
                    {
                        result = fixed4(1,1,1,1);   // w = 1 => ray hit
                        break;
                    }

                    distanceTraveled += distanceToHit;
                }

                return result;
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
                if(cloudBottomT < 0 && cloudTopT < 0)
                {
                    return fixed4(sceneColor, 1.0);     // Skip raymarching
                }
                
                if(rayOrigin.y < _CloudLayerHeight.x)
                {
                    //return fixed4(1,0,0,1);
                    rayOrigin += rayDirection * cloudBottomT;
                    sceneDepth -= length(rayDirection * cloudBottomT);
                }
                else if(rayOrigin.y > _CloudLayerHeight.y)
                {
                    //return fixed4(0,0,1,1);
                    rayOrigin += rayDirection * cloudTopT;
                    sceneDepth -= length(rayDirection * cloudTopT);
                }
                


                fixed4 result = raymarching(rayOrigin, rayDirection, sceneDepth);

                return fixed4(sceneColor * (1.0 - result.w) + result.w * result.xyz, 1.0);
            }
            ENDCG
        }
    }
}
