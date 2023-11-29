Shader "Custom/GrassInstancing"
{
    Properties
    {
        _MainTexture("_MainTexture", 2D) = "white" {}
        _WindTexture("_WindTexture", 2D) = "white" {}
        _WindSpeed("_WindSpeed", float) = 1.5
        _HeightFactor("Height Factor", float) = 1.0
        _HeightCutoff("Height Cutoff", float) = 0.5
    }

        SubShader
        {
            Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline"}
            Cull Off //use default culling because this shader is billboard 
            
            HLSLINCLUDE
            #define UNITY_PI            3.14159265359f
            struct SourceVertex
            {
                float4 positionOS; // (xyz -> position) (w -> scale)
            };

            float rand(float3 co)
            {
                return frac(
                    sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
            }

            float Random(float seed) {
                seed = (seed * 279470273.0 + 1.0) - floor(seed * 279470273.0 + 1.0);
                return seed;
            }

            float3x3 GetRandomYRotationMatrix(float angle) {
                float cosA = cos(angle);
                float sinA = sin(angle);
                return float3x3(
                    cosA, 0, sinA,
                    0, 1, 0,
                    -sinA, 0, cosA
                    );
            }

            // A function to compute an rotation matrix which rotates a point
            // by angle radians around the given axis
            // By Keijiro Takahashi
            float3x3 AngleAxis3x3(float angle, float3 axis)
            {
                float c, s;
                sincos(angle, s, c);

                float t = 1 - c;
                float x = axis.x;
                float y = axis.y;
                float z = axis.z;

                return float3x3(
                    t * x * x + c, t * x * y - s * z, t * x * z + s * y,
                    t * x * y + s * z, t * y * y + c, t * y * z - s * x,
                    t * x * z - s * y, t * y * z + s * x, t * z * z + c);
            }
            ENDHLSL

            Pass
            {
                Name "Forward"
                Tags { "LightMode" = "UniversalForward" }

                Blend One Zero , One Zero
                ZWrite On
                ZTest LEqual
                Offset 0 , 0
                ColorMask RGBA


                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                // -------------------------------------
                // Universal Render Pipeline keywords
                // When doing custom shaders you most often want to copy and paste these #pragmas
                // These multi_compile variants are stripped from the build depending on:
                // 1) Settings in the URP Asset assigned in the GraphicsSettings at build time
                // e.g If you disabled AdditionalLights in the asset then all _ADDITIONA_LIGHTS variants
                // will be stripped from build
                // 2) Invalid combinations are stripped. e.g variants with _MAIN_LIGHT_SHADOWS_CASCADE
                // but not _MAIN_LIGHT_SHADOWS are invalid and therefore stripped.
                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
                #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
                #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
                #pragma multi_compile _ _SHADOWS_SOFT
                // -------------------------------------
                // Unity defined keywords
                #pragma multi_compile_fog
                // -------------------------------------

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

                struct Attributes
                {
                    float4 positionOS   : POSITION;
                    float4 texcoord     : TEXCOORD0;
                };

                struct Varyings
                {
                    float4 positionCS   : SV_POSITION;
                    float3 color        : COLOR;
                    float4 texcoord     : TEXCOORD0;
                    float4 shadowCoord  : TEXCOORD2;
                    float3 positionWS   : TEXCOORD1; // Position in world space
                    float3 samplePos    : TEXCOORD3; // Position in world space
                };

                sampler2D _MainTexture;
                // Local to world matrix
                float4x4 _LocalToWorld;

                sampler2D _WindTexture;
                float _WindSpeed;
                float _WorldSize;
                float _WindSize;

                float _HeightFactor;
                float _HeightCutoff;

                CBUFFER_START(UnityPerMaterial)
                    StructuredBuffer<SourceVertex> positionBuffer;
                CBUFFER_END

                Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
                {
                    Varyings OUT = (Varyings)0;
                    
                    float randomValue = rand(positionBuffer[instanceID].positionOS.xyz) * TWO_PI;
                    float3x3 rotationMatrix = GetRandomYRotationMatrix(randomValue);
                    float3 rotatedPosition = float3(mul(rotationMatrix, IN.positionOS.xyz));

                    float scale = positionBuffer[instanceID].positionOS.w;

                    // 랜덤한 로테이션값 구하기
                    float3 pos = (rotatedPosition * scale) + positionBuffer[instanceID].positionOS.xyz;

                    // 바람 2
                    // 정점 세계 위치를 얻습니다.
                    // 월드 크기에 따라 위치를 정규화합니다.
                    float2 _Size = float2(_WorldSize / _WindSize, _WorldSize / _WindSize);
                    float2 SamplePos = pos.xz / _Size;
                    // 바람 텍스처 샘플
                    SamplePos.x += (_Time.x * _WindSpeed);
                    float windSample = tex2Dlod(_WindTexture, float4(SamplePos, 0, 0)).r;

                    OUT.samplePos = float3(windSample,windSample,windSample);

                    // 바람
                    // 0 아래 애니메이션 _HeightCutoff
                    float heightFactor = IN.positionOS.y > _HeightCutoff;
                    // 높이에 따라 애니메이션을 더 강하게 만든다
                    heightFactor = heightFactor * pow(abs(IN.positionOS.y), _HeightFactor);
                    // 그냥 제자리에서 뱅글뱅글 도는 애니 적용
                    pos.z += sin(_Time.y)  * heightFactor * 0.2;
                    pos.x += cos(_Time.y)  * heightFactor * 0.2;
                    // 웨이브 애니메이션 적용
                    pos.z += windSample * heightFactor * 0.5;
                    pos.x += windSample * heightFactor * 0.5;

                    // 월드 행렬 곱
                    float3 posworld = mul(_LocalToWorld, float4(pos, 1)).xyz;
                    float4 transform6 = float4(posworld, 1);

                    float3 positionVS = TransformWorldToView(transform6.xyz);
                    float4 positionCS = TransformWorldToHClip(transform6.xyz);
                    OUT.positionCS = positionCS;
                    OUT.texcoord = IN.texcoord;

                    VertexPositionInputs vertexInput = (VertexPositionInputs)0;
                    vertexInput.positionWS = transform6.xyz;
                    vertexInput.positionCS = positionCS;
                    OUT.shadowCoord = GetShadowCoord(vertexInput);
                    OUT.positionWS = transform6.xyz;
                    return OUT;
                }

                half4 frag(Varyings IN) : SV_Target
                {
                    half4 mainColor = tex2D(_MainTexture, IN.texcoord.xy);
                    clip(mainColor.a - 0.3f);

                    // take shadow data
                    Light mainLight = GetMainLight();			//! Lighting.hlsl 함수로 라이트 정보 및 쉐도우 데이터 구조체 얻어옴
                    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();	//! 쉐도우 감쇠값    
                    half4 shadowParams = GetMainLightShadowParams();
                    float shadowAtten = SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture), TransformWorldToShadowCoord(IN.positionWS), shadowSamplingData, shadowParams, false);

                    // base color by lerping 2 colors over the UVs
                    float4 baseColor = mainColor;
                    // multiply with lighting color
                    float4 litColor = (baseColor * _MainLightColor);
                    // multiply with vertex color, and shadows
                    float4 final = litColor;
                    final.rgb = litColor.rgb * shadowAtten;
                    // add in baseColor when lights turned off
                    final += saturate((1 - shadowAtten) * baseColor * 0.2);
                    // add in ambient color
                    final += (unity_AmbientSky * baseColor * 1.0f);
                    
                    float heightFactor = IN.texcoord.y > 0.7;
                    // 높이에 따라 애니메이션을 더 강하게 만든다
                    heightFactor = heightFactor * pow(abs(IN.samplePos.x), _HeightFactor);
                    return final - (final * heightFactor * 0.5);
                    return final;
                }
                ENDHLSL
            }

			Pass
			{

				Name "ShadowCaster"
				Tags { "LightMode" = "ShadowCaster" }

				ZWrite On
				ZTest LEqual

				HLSLPROGRAM
				#pragma multi_compile_instancing
				#pragma multi_compile _ LOD_FADE_CROSSFADE
				#pragma multi_compile_fog
				#pragma prefer_hlslcc gles

				#pragma vertex vert
				#pragma fragment frag

				#define SHADERPASS_SHADOWCASTER

				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
				#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

                struct Attributes
                {
                    float4 positionOS   : POSITION;
                    float4 texcoord     : TEXCOORD0;
                };

                struct Varyings
                {
                    float4 positionCS   : SV_POSITION;
                    half3 color         : COLOR;
                    float4 texcoord     : TEXCOORD0;
                };

                sampler2D _MainTexture;
                float4x4 _LocalToWorld;

                sampler2D _WindTexture;
                float _WindSpeed;
                float _WorldSize;
                float _WindSize;

                float _HeightFactor;
                float _HeightCutoff;

                CBUFFER_START(UnityPerMaterial)
                    StructuredBuffer<SourceVertex> positionBuffer;
                CBUFFER_END

                Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
                {
                    Varyings OUT = (Varyings)0;

                    float randomValue = rand(positionBuffer[instanceID].positionOS.xyz) * TWO_PI;
                    float3x3 rotationMatrix = GetRandomYRotationMatrix(randomValue);
                    float3 rotatedPosition = float3(mul(rotationMatrix, IN.positionOS.xyz));

                    float scale = positionBuffer[instanceID].positionOS.w;

                    // 랜덤한 로테이션값 구하기
                    float3 pos = (rotatedPosition * scale) + positionBuffer[instanceID].positionOS.xyz;

                    // 바람 2
                    // 정점 세계 위치를 얻습니다.
                    // 월드 크기에 따라 위치를 정규화합니다.
                    float2 _Size = float2(_WorldSize / _WindSize, _WorldSize / _WindSize);
                    float2 SamplePos = pos.xz / _Size;
                    // 바람 텍스처 샘플
                    SamplePos.x += (_Time.x * _WindSpeed);
                    float windSample = tex2Dlod(_WindTexture, float4(SamplePos, 0, 0)).r;

                    // 바람
                    // 0 아래 애니메이션 _HeightCutoff
                    float heightFactor = IN.positionOS.y > _HeightCutoff;
                    // 높이에 따라 애니메이션을 더 강하게 만든다
                    heightFactor = heightFactor * pow(abs(IN.positionOS.y), _HeightFactor);
                    // 그냥 제자리에서 뱅글뱅글 도는 애니 적용
                    pos.z += sin(_Time.y)  * heightFactor * 0.2;
                    pos.x += cos(_Time.y)  * heightFactor * 0.2;
                    // 웨이브 애니메이션 적용
                    pos.z += windSample * heightFactor * 0.5;
                    pos.x += windSample * heightFactor * 0.5;

                    // 월드 행렬 곱
                    float3 posworld = mul(_LocalToWorld, float4(pos, 1)).xyz;
                    float4 transform6 = float4(posworld, 1);

                    float3 positionVS = TransformWorldToView(transform6.xyz);
                    float4 positionCS = TransformWorldToHClip(transform6.xyz);
                    OUT.positionCS = positionCS;
                    OUT.texcoord = IN.texcoord;
                    return OUT;
                }

                half4 frag(Varyings IN) : SV_Target
                {
                    half4 mainColor = tex2D(_MainTexture, IN.texcoord.xy);
                    clip(mainColor.a - 0.3f);
                    return mainColor;
                }
				ENDHLSL
			}
        }
}
