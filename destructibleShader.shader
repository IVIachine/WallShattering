// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

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
		LOD 100
		Pass
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma geometry geom
		#pragma target 4.6
		#include "UnityCG.cginc"

		//Starter constants
		float baseSpeed;
		float speedFromDistance;
		float speedRand;
		float removalTime;

		float baseRotationSpeed;
		float rotationSpeedFromDist;
		float rotSpeedRand;

		sampler2D _MainTex;
		sampler2D _EmissionMap;
		sampler2D _DecalTex;
		sampler2D _MetalicMap;
		sampler2D _DetailMap;

		float4 _Color;
		float4 _EmissionColor;

		float _EmissionMultiplier;
		float _MetalicMultiplier;

		int numExplosions;
		int objectIndex;
		int cullObject;

		//compute buffers
		StructuredBuffer<float> explosionTimes;
		StructuredBuffer<float> explosionRadii;
		StructuredBuffer<float3> explosionLocations;

		StructuredBuffer<int> triangleIndices;
		StructuredBuffer<float3> vertexPositions;

		AppendStructuredBuffer<int> triangleRemovalsObj : register(u1);
		AppendStructuredBuffer<int> colliderRemovalsObj : register(u2);

		AppendStructuredBuffer<int> triangleRemovalsIndices : register(u3);
		AppendStructuredBuffer<int> colliderRemovalsIndices : register(u4);

	void vert(inout appdata_full v, uint id: SV_VertexID)
	{
		v.texcoord1.x = id;
		v.vertex = UnityObjectToClipPos(v.vertex);
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

	//Rotate vertex around axis
	static float3 rotatePosition(float3 n, float angle, float3 p)
	{
		float3x3 m = float3x3(
			n.x*n.x * (1.0f - cos(angle)) + cos(angle),       
			n.x*n.y * (1.0f - cos(angle)) + n.z * sin(angle), 
			n.x*n.z * (1.0f - cos(angle)) - n.y * sin(angle), 

			n.y*n.x * (1.0f - cos(angle)) - n.z * sin(angle), 
			n.y*n.y * (1.0f - cos(angle)) + cos(angle),       
			n.y*n.z * (1.0f - cos(angle)) + n.x * sin(angle), 

			n.z*n.x * (1.0f - cos(angle)) + n.y * sin(angle), 
			n.z*n.y * (1.0f - cos(angle)) - n.x * sin(angle), 
			n.z*n.z * (1.0f - cos(angle)) + cos(angle)        
			);

		float3 q = mul(m, p);
		return q;
	}

	//CITE:http://www.reedbeta.com/blog/quick-and-easy-gpu-random-numbers-in-d3d11/
	uint rand_xorshift(uint seed)
	{
		// Xorshift algorithm from George Marsaglia's paper
		seed ^= (seed << 13);
		seed ^= (seed >> 17);
		seed ^= (seed << 5);
		return seed;
	}

		[maxvertexcount(6)]
	void geom(
		triangle appdata_full i[3],
		inout TriangleStream<appdata_full> stream,
		uint id : SV_PrimitiveID
	) {
		float4 p0 = i[0].vertex;
		float4 p1 = i[1].vertex;
		float4 p2 = i[2].vertex;

		if (numExplosions > 0)
		{
			float3 worldOne = vertexPositions[i[0].texcoord1.x];
			float3 worldTwo = vertexPositions[i[1].texcoord1.x];
			float3 worldThree = vertexPositions[i[2].texcoord1.x];

			float maxTime = -1;
			float bestIndex = 1.#INF;

			float dist1 = 0;
			float dist2 = 0;
			float dist3 = 0;

			//iterate through each explosion
			for (int i = 0; i < numExplosions; i++)
			{
				dist1 = distance(worldOne, explosionLocations[i]);
				dist2 = distance(worldTwo, explosionLocations[i]);
				dist3 = distance(worldThree, explosionLocations[i]);

				//if it is close enough
				if (dist1 < explosionRadii[i] || dist2 < explosionRadii[i] || dist3 < explosionRadii[i])
				{
					//only grab the explosion with the longest lifetime
					if (explosionTimes[i] > maxTime)
					{
						maxTime = explosionTimes[i];
						bestIndex = i;
					}
				}
			}

			//if this triangle is close enough to at least one explosion
			if (isinf(bestIndex) == false)
			{
				dist1 = distance(worldOne, explosionLocations[bestIndex]);
				dist2 = distance(worldTwo, explosionLocations[bestIndex]);
				dist3 = distance(worldThree, explosionLocations[bestIndex]);

				if (explosionTimes[bestIndex] == 0)
				{
					colliderRemovalsIndices.Append(triangleIndices[id * 3]);
					colliderRemovalsObj.Append(objectIndex);
				}

				if (explosionTimes[bestIndex] > removalTime)
				{
					triangleRemovalsIndices.Append(id);
					triangleRemovalsObj.Append(objectIndex);
				}

				float3 center = (worldOne + worldTwo + worldThree) / 3;
				float3 dist = distance(center, explosionLocations[bestIndex]);

				float3 dir = normalize((explosionLocations[bestIndex] - center).xyz);
				float3 perpendicular = float3(dir.y, -dir.x, dir.z);

				//apply random rotation
				float rotRand = float(rand_xorshift(id)) * (rotSpeedRand / 4294967296.0);
				float distRotIncrement = rotationSpeedFromDist * (1 / dist);

				worldOne = worldOne - center;
				worldOne = rotatePosition(perpendicular, explosionTimes[bestIndex] * (baseRotationSpeed * distRotIncrement), worldOne) + center;

				worldTwo = worldTwo - center;
				worldTwo = rotatePosition(perpendicular, explosionTimes[bestIndex] * (baseRotationSpeed * distRotIncrement), worldTwo) + center;

				worldThree = worldThree - center;
				worldThree = rotatePosition(perpendicular, explosionTimes[bestIndex] * (baseRotationSpeed * distRotIncrement), worldThree) + center;

				//apply random velocity addition
				float randSpeed = float(rand_xorshift(id.x)) * (speedRand / 4294967296.0);
				float distIncrement = speedFromDistance * (1 / dist);

				//still need to fix direction(why the huge number exists)!
				worldOne += (-dir * (baseSpeed * distIncrement) * explosionTimes[bestIndex]) + float3(1, 1, 0) * randSpeed;
				worldTwo += (-dir * (baseSpeed  * distIncrement) * explosionTimes[bestIndex]) + float3(1, 1, 0) * randSpeed;
				worldThree += (-dir * (baseSpeed * distIncrement) * explosionTimes[bestIndex]) + float3(1, 1, 0) * randSpeed;

				float4 tmp1 = UnityObjectToClipPos(worldOne);
				float4 tmp2 = UnityObjectToClipPos(worldTwo);
				float4 tmp3 = UnityObjectToClipPos(worldThree);

				p0 = tmp1;
				p1 = tmp2;
				p2 = tmp3;
			}
		}

		i[0].vertex = p0;
		i[1].vertex = p1;
		i[2].vertex = p2;

		stream.Append(i[0]);
		stream.Append(i[1]);
		stream.Append(i[2]);

		stream.RestartStrip();

		stream.Append(i[2]);
		stream.Append(i[1]);
		stream.Append(i[0]);
	}
	ENDCG
	}
	}
}