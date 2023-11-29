#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct DrawVertex
{
    float3 positionWS;
    float2 uv;
    float3 diffuseColor;
};

struct DrawTriangle
{
    float3 normalOS;
    DrawVertex vertices[3];
};

StructuredBuffer<DrawTriangle> _DrawTriangles;

struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float3 diffuseColor : COLOR;
    float4 shadowCoord : TEXCOORD6;
    //LIGHTING_COORDS(3, 4)
    //float fogCoord : TEXCOORD0; //UNITY_FOG_COORDS(5)
};

// 프로퍼티
float4 _TopTint;
float4 _BottomTint;
float _AmbientStrength;
float _Fade;

struct unityTransferVertexToFragmentSucksHack
{
    float3 vertex : POSITION;
};

v2f vert(uint vertexID : SV_VertexID)
{
    v2f output = (v2f) 0;

    // 버퍼가 삼각형으로 구성되어 있으므로 vertexID를 3으로 나누기
    // 삼각형을 구한 다음 %3을 하여 삼각형의 꼭짓점을 구함
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex input = tri.vertices[vertexID % 3];

    output.pos = TransformObjectToHClip(input.positionWS);
    output.positionWS = input.positionWS;

    // float3 faceNormal = GetMainLight().direction * tri.normalOS;
    float3 faceNormal = tri.normalOS;
    // output.normalWS = TransformObjectToWorldNormal(faceNormal, true);
    output.normalWS = faceNormal;

    output.uv = input.uv;

    output.diffuseColor = input.diffuseColor;

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionWS.xyz);
    output.shadowCoord = GetShadowCoord(vertexInput);

    // making pointlights work requires v.vertex
    //unityTransferVertexToFragmentSucksHack v;
    //v.vertex = output.pos;

    //TRANSFER_VERTEX_TO_FRAGMENT(output);
    //UNITY_TRANSFER_FOG(output, output.pos);

    return output;
}