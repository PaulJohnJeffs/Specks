Shader "Unlit/Liquid"
{
    Properties
    {
		_col ("Colour", Color) = (1, 1, 1, 1)
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

			float4 _col;

			struct Speck
			{
				float3 Pos;
				float3 Vel;
			};

			StructuredBuffer<Speck> Specks;
			StructuredBuffer<uint2> PartIndices;
			float SpeckRadius;
			uint PartsPerDim;

            struct appdata
            {
				uint instanceID : SV_InstanceID;
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
				uint instanceID : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
				UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

				float4x4 modelMatrix = float4x4(
					SpeckRadius, 0, 0, 0,
					0, SpeckRadius, 0, 0,
					0, 0, SpeckRadius, 0,
					0, 0, 0, 1);

				modelMatrix._m03_m13_m23_m33 += float4(Specks[v.instanceID].Pos, 0);

				float4x4 pv = mul(UNITY_MATRIX_P, UNITY_MATRIX_V);
				float4x4 mvp = mul(pv, modelMatrix);
				o.vertex = mul(mvp, v.vertex);
				o.instanceID = v.instanceID;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				uint idx = PartIndices[i.instanceID].x;

				uint x = idx % PartsPerDim;
				uint y = (idx / PartsPerDim) % PartsPerDim;
				uint z = (idx / (PartsPerDim * PartsPerDim));

				float3 rgb = float3(x, y, z) / PartsPerDim;

				return float4(rgb, 1);
            }
            ENDCG
        }
    }
}
