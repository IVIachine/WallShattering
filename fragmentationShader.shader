//CITE: Help from http://catlikecoding.com/unity/tutorials/advanced-rendering/tessellation/
Shader "Custom/fragmentationShader" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
	}
		SubShader{
		Tags{ "DisableBatching" = "True" }


			Pass
		{
			CGPROGRAM
			#pragma vertex MyTessellationVertexProgram
			#pragma fragment frag
			#pragma hull myHullProgram
			#pragma domain MyDomainProgram
			#pragma geometry MyGeometryProgram
			#pragma target 4.6


			sampler2D _MainTex;
			int numExplosions;

			//compute buffers
			StructuredBuffer<float> explosionTimes;
			StructuredBuffer<float> explosionRadii;
			StructuredBuffer<float3> explosionLocations;

			//starter constants
			float baseSpeed;
			float speedFromDistance;
			float speedRand;
			float removalTime;

			float baseRotationSpeed;
			float rotationSpeedFromDist;
			float rotSpeedRand;

			int Subdivisions;
			int inGame = 0;

			struct TessellationFactors {
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			fixed4 _Color;
			struct Vertex
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				uint uv2 : TEXCOORD2;
				uint id : TEXCOORD3;
			};

			struct TessellationControlPoint {
				float4 vertex : INTERNALTESSPOS;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				uint uv2 : TEXCOORD2;
			};

			struct Fragment
			{
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				uint uv2 : TEXCOORD2;
				uint id : TEXCOORD3;
			};

			struct InterpolatorsGeometry {
				Fragment data;
				float3 barycentricCoordinates : TEXCOORD9;
			};

			TessellationFactors MyPatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch) {
				TessellationFactors f;
				f.edge[0] = Subdivisions;
				f.edge[1] = Subdivisions;
				f.edge[2] = Subdivisions;
				f.inside = Subdivisions;
				return f;
			}

			Fragment vert(inout Vertex v)
			{
				Fragment o;

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.normal = v.normal;
				o.tangent = v.tangent;
				o.uv = v.uv;
				o.uv1 = v.vertex;
				o.uv2 = v.uv2;
				o.id = v.id;
				return o;
			}

			float3 GetAlbedo(Fragment i) {
				float3 albedo =
					tex2D(_MainTex, i.uv.xy).rgb * _Color.rgb;
				return albedo;
			}

			float3 frag(InterpolatorsGeometry IN, uint id : SV_SampleIndex) : COLOR
			{
				float3 albedo = GetAlbedo(IN.data);
				if (inGame == 0)
				{
					float3 barys;
					barys.xy = IN.barycentricCoordinates;
					barys.z = 1 - barys.x - barys.y;
					float minBary = min(barys.x, min(barys.y, barys.z));
					return albedo * minBary;
				}

				return albedo;
			}

				[UNITY_domain("tri")]
				[UNITY_outputcontrolpoints(3)]
				[UNITY_outputtopology("triangle_cw")]
				[UNITY_partitioning("integer")]
				[UNITY_patchconstantfunc("MyPatchConstantFunction")]
				TessellationControlPoint  myHullProgram(
						InputPatch<TessellationControlPoint, 3> patch,
						uint id : SV_OutputControlPointID
					)
				{
					return patch[id];
				}

				[UNITY_domain("tri")]
				Fragment MyDomainProgram(
					TessellationFactors factors,
					OutputPatch<TessellationControlPoint, 3> patch,
					uint id: SV_PrimitiveID,
					float3 barycentricCoordinates : SV_DomainLocation
				) {
					Vertex data;
					#define MY_DOMAIN_PROGRAM_INTERPOLATE(fieldName) data.fieldName = \
					patch[0].fieldName * barycentricCoordinates.x + \
					patch[1].fieldName * barycentricCoordinates.y + \
					patch[2].fieldName * barycentricCoordinates.z;


					MY_DOMAIN_PROGRAM_INTERPOLATE(vertex)
					MY_DOMAIN_PROGRAM_INTERPOLATE(normal)
					MY_DOMAIN_PROGRAM_INTERPOLATE(tangent)
					MY_DOMAIN_PROGRAM_INTERPOLATE(uv)
					MY_DOMAIN_PROGRAM_INTERPOLATE(uv1)
					MY_DOMAIN_PROGRAM_INTERPOLATE(uv2)

					data.id = id;
					return vert(data);
				}

				//pass data
				TessellationControlPoint MyTessellationVertexProgram(Vertex v) {
					TessellationControlPoint p;
					p.vertex = v.vertex;
					p.normal = v.normal;
					p.tangent = v.tangent;
					p.uv = v.uv;
					p.uv1 = v.uv1;
					p.uv2 = v.uv2;
					return p;
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

				//Rotate vertex around axis
				float3 rotatePosition(float3 n, float angle, float3 p)
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

					float3 q = mul(m,p);
					return q;
				}

				[maxvertexcount(6)]
				void MyGeometryProgram(
					triangle Fragment i[3],
					inout TriangleStream<InterpolatorsGeometry> stream,
					uint id : SV_PrimitiveID
				) {

					float3 p0 = i[0].vertex.xyz;
					float3 p1 = i[1].vertex.xyz;
					float3 p2 = i[2].vertex.xyz;
					float3 triangleNormal = normalize(cross(p1 - p0, p2 - p0));

					i[0].normal = triangleNormal;
					i[1].normal = triangleNormal;
					i[2].normal = triangleNormal;

					InterpolatorsGeometry g0, g1, g2;
					g0.data = i[0];
					g1.data = i[1];
					g2.data = i[2];

					if (numExplosions > 0)
					{
						float3 worldOne = g0.data.uv1;
						float3 worldTwo = g1.data.uv1;
						float3 worldThree = g2.data.uv1;

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

							g0.data.vertex = UnityObjectToClipPos(worldOne);
							g1.data.vertex = UnityObjectToClipPos(worldTwo);
							g2.data.vertex = UnityObjectToClipPos(worldThree);
						}
					}

					g0.barycentricCoordinates = float3(1, 0, 0);
					g1.barycentricCoordinates = float3(0, 1, 0);
					g2.barycentricCoordinates = float3(0, 0, 1);

					stream.Append(g0);
					stream.Append(g1);
					stream.Append(g2);

					stream.RestartStrip();

					stream.Append(g2);
					stream.Append(g1);
					stream.Append(g0);
				}

				ENDCG
		}
	}
}
