Shader "CustomPost/RenderDepth"
{
    Properties
    {
        _MainTex ("Color Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline"}
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
			#include "../PostProcessIncludes/Math.cginc"
			#include "../PostProcessIncludes/Triplanar.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 viewVector : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
                // (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return o;
            }

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            float3 center;
            float radius;

            float depthMultiplier;
            float alphaMultiplier;
            float4 shallowColor;
            float4 deepColor;

            float3 dirToSun;
            float smoothness;
            float ambientLigting;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 originalColor = tex2D(_MainTex, i.uv);
                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float sceneDepth = LinearEyeDepth(nonLinearDepth) * length(i.viewVector);

                float3 rayDir = normalize(i.viewVector);
                float2 hitInfo = raySphere(center, radius/2, _WorldSpaceCameraPos, rayDir);

                float oceanViweDepth = min(hitInfo.y, sceneDepth - hitInfo.x);

                if(oceanViweDepth > 0)
                {
                    float opticalDepth01 = 1 - exp(-oceanViweDepth * depthMultiplier);
                    float alpha = 1 - exp(-oceanViweDepth * alphaMultiplier);

                    float3 oceanNormal = normalize((_WorldSpaceCameraPos + rayDir * hitInfo.x) - center);

                    float specularHighlight = 0;
                    if(hitInfo.x > 0)
                    {
                        //float mask = dot(dirToSun, oceanNormal);
                        //mask = (mask > 0) + smoothstep(mask);

                        float specularAngle = acos(dot(normalize(dirToSun - rayDir), oceanNormal));
                        float specularExponent = specularAngle / (1 - smoothness);
                        specularHighlight = exp(-specularExponent * specularExponent);// * mask;
                    }

                    float diffuseLighting = saturate(dot(oceanNormal, dirToSun));

                    diffuseLighting = clamp(diffuseLighting + ambientLigting, 0, 1);

                    float4 oceanCol = lerp(shallowColor, deepColor, opticalDepth01) * diffuseLighting + specularHighlight;
                    return lerp(originalColor, oceanCol, alpha);
                }
                
                return originalColor;
            }
            ENDCG
        }
    }
}