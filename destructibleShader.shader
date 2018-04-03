Shader "Unlit/destructibleShader"
{
	Properties
	{
		_MainTex("Sprite Texture", 2D) = "white" {}
		_DecalTex("Decal Texture", 2D) = "white" {}
		_Color("Tint", Color) = (1,1,1,1)

		_EmissionColor("Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}
		_EmissionMultiplier("Multiplier", Float) = 0

		_MetalicMap("Metalic", 2D) = "white" {}
		_MetalicMultiplier("Multiplier", Float) = 0

		_DetailMap("Detail", 2D) = "white" {}
	}

		SubShader
	{
		Tags{ "RenderType" = "Opaque" "DisableBatching" = "true" }
		Cull Off
		Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.5
		#include "UnityCG.cginc"

		sampler2D _MainTex;
		sampler2D _EmissionMap;
		sampler2D _DecalTex;
		sampler2D _MetalicMap;
		sampler2D _DetailMap;

		float4 _Color;
		float4 _EmissionColor;

		float _EmissionMultiplier;
		float _MetalicMultiplier;

		StructuredBuffer<float3> vertexPositions;

		int playing = 0;

	void vert(inout appdata_full v, uint id: SV_VertexID)
	{
		if (playing == 1)
		{
			v.vertex = UnityObjectToClipPos(vertexPositions[id]);
		}
		else
		{
			v.vertex = UnityObjectToClipPos(v.vertex);
		}
	}

	fixed4 frag(appdata_full IN) : SV_Target
	{
		// sample the texture
		fixed4 col = tex2D(_MainTex, IN.texcoord);
		col.rgb = col.rgb * _Color;

		half4 emission = tex2D(_EmissionMap, IN.texcoord) * (_EmissionColor * _EmissionMultiplier);
		col.rgb += emission.rgb;

		half4 metalic = tex2D(_MetalicMap, IN.texcoord) * _MetalicMultiplier;
		col.rgb *= metalic.r;

		half4 detail = tex2D(_DetailMap, IN.texcoord);
		col.rgb *= detail.r / 5;

		return col;
	}

	ENDCG
	}
	}
}