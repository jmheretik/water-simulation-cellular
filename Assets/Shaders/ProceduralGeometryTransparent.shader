// A simple shader drawing triangles which are already on GPU in a given transparent color.

Shader "Procedural Geometry/Marching Cubes/Transparent"
{
	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent"}

		Cull Back
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			float _voxelSize;
			float4 _color;
			uniform float4x4 _model;

			struct Vertex
			{
				float3 vPosition;
				float3 vNormal;
			};

			struct Triangle
			{
				Vertex v[3];
			};

			uniform StructuredBuffer<Triangle> triangleBuffer;

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
			};

			v2f vert(uint id : SV_VertexID)
			{
				uint pid = id / 3;
				uint vid = id % 3;

				v2f o;
				o.vertex = UnityObjectToClipPos(triangleBuffer[pid].v[vid].vPosition * _voxelSize);
				o.normal = mul(unity_ObjectToWorld, triangleBuffer[pid].v[vid].vNormal);
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float d = max(dot(normalize(_WorldSpaceLightPos0.xyz), i.normal), 0);

				return float4(d * _color.rgb, _color.a);
			}
			ENDCG
		}
	}
}
