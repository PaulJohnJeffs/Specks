Shader "Unlit/Liquid"
{
    Properties
    {
		_colA ("Colour A", Color) = (1, 1, 1, 1)
		_colB ("Colour B", Color) = (1, 1, 1, 1)
		_fog ("Fog", Color) = (1, 1, 1, 1)

		_fogMaxDist ("Fog Max Dist", float) = 1000
		_velMax ("Vel Max", float) = 10
		_densityMax ("Density Max", int) = 50
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile_instancing

            #include "UnityCG.cginc"

			float4 _colA;
			float4 _colB;
			float4 _fog;
			float _fogMaxDist;
			float _velMax;
			int _densityMax;

			struct Speck
			{
				float3 Pos;
				float3 LastPos;
				float3 Vel;
				int D;
			};

			StructuredBuffer<Speck> Specks;
			StructuredBuffer<uint2> PartIndices;
			float SpeckRadius;
			uint PartsPerDim;

            struct appdata
            {
				uint instanceID : SV_InstanceID;
                float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				uint instanceID : TEXCOORD1;
				float distToCam : TEXCOORD2;
				float vel : TEXCOORD3;
				int density : TEXCOORD4;
            };

            v2f vert (appdata v)
            {
				UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
				float3 z = -normalize(_WorldSpaceCameraPos - Specks[v.instanceID].Pos) * SpeckRadius;
				float3 y = float3(0, 0, 1);
				float3 x = normalize(cross(z, y)) * SpeckRadius;
				y = normalize(cross(x, z)) * SpeckRadius;

				float4x4 modelMatrix = float4x4(
					x.x, y.x, z.x, 0,
					x.y, y.y, z.y, 0,
					x.z, y.z, z.z, 0,
					  0,   0,   0, 1);

				modelMatrix._m03_m13_m23_m33 += float4(Specks[v.instanceID].Pos, 0);

				float4x4 pv = mul(UNITY_MATRIX_P, UNITY_MATRIX_V);
				float4x4 mvp = mul(pv, modelMatrix);
				o.vertex = mul(mvp, v.vertex);
				o.instanceID = v.instanceID;
				o.uv = v.vertex;
				o.distToCam = length(_WorldSpaceCameraPos - Specks[v.instanceID].Pos);
				o.vel = length(Specks[v.instanceID].Vel);
				o.density = Specks[v.instanceID].D;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				float2 radVec = i.uv;;
				float rad = length(radVec);

				clip((rad > 0.3) * -1);
				
				float tVel = saturate(i.vel / _velMax);
				float tFog = saturate(i.distToCam / _fogMaxDist);
				float tDensity = saturate((float)i.density / _densityMax);

				float4 speedCol = lerp(_colA, _colB, tVel);
				speedCol = lerp(speedCol, _colB, 1 - tDensity);
				return lerp(speedCol, _fog, i.distToCam / _fogMaxDist);
            }
            ENDCG
        }
    }
}
