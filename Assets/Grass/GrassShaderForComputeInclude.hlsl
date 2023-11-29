#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

//
struct DrawVertex
{
    float3 positionWS; // The position in world space 
    float2 uv;
    float3 diffuseColor;
};

// A triangle on the generated mesh
struct DrawTriangle
{
    float3 normalOS;
    DrawVertex vertices[3]; // The three points on the triangle
};

StructuredBuffer<DrawTriangle> _DrawTriangles;

struct v2f
{
    float4 pos : SV_POSITION; // Position in clip space
    float2 uv : TEXCOORD0;          // The height of this vertex on the grass blade
    float3 positionWS : TEXCOORD1; // Position in world space
    float3 normalWS : TEXCOORD2;   // Normal vector in world space
    float3 diffuseColor : COLOR;
    //LIGHTING_COORDS(3, 4)
    //float fogCoord : TEXCOORD0; //UNITY_FOG_COORDS(5)
};

// Properties
float4 _TopTint;
float4 _BottomTint;
float _AmbientStrength;
float _Fade;

// Vertex function
struct unityTransferVertexToFragmentSucksHack
{
    float3 vertex : POSITION;
};

// -- retrieve data generated from compute shader
v2f vert(uint vertexID : SV_VertexID)
{
    // Initialize the output struct
    v2f output = (v2f)0;

    // Get the vertex from the buffer
    // Since the buffer is structured in triangles, we need to divide the vertexID by three
    // to get the triangle, and then modulo by 3 to get the vertex on the triangle
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

    // making pointlights work requires v.vertex
    //unityTransferVertexToFragmentSucksHack v;
    //v.vertex = output.pos;

    //TRANSFER_VERTEX_TO_FRAGMENT(output);
    //UNITY_TRANSFER_FOG(output, output.pos);

    return output;
}