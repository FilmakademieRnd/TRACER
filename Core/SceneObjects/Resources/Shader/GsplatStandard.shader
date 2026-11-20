
Shader "Gsplat/Standard"
{
    Properties {}
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            ZWrite Off
            Blend One OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5 // Targets high-performance WebGPU, Vulkan and Metal compilation structures
            #pragma multi_compile SH_BANDS_0 SH_BANDS_1 SH_BANDS_2 SH_BANDS_3 SH_BANDS_4

            #include "UnityCG.cginc"
            #include "Gsplat.hlsl"

            // --- SPARK COMPRESSED GPU UNMANAGED CHANNELS ---
            StructuredBuffer<uint4> _PositionBuffer;     // Remapped to hold the packed uint4 geometry records
            StructuredBuffer<uint>  _OrderBuffer;        // Sorted GPU indices array
            
            #ifndef SH_BANDS_0
            StructuredBuffer<uint> _ScaleBuffer;            // 16-bit cluster label index per splat
            StructuredBuffer<float4> _SHCentroidsBuffer;    // Flattened SH coefficients [cluster * coeffsPerSplat + k]
            #endif

            uint _SplatCount;
            float4x4 _MATRIX_M;
            float _Brightness;
            float _ScaleFactor;
            int _SHDegree;
            bool _GammaToLinear;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 DecodeQuatSogV2(uint4 packedData)
            {
                float qR = float((packedData.z >> 16u) & 0xFFu) / 255.0;
                float qG = float((packedData.z >> 24u) & 0xFFu) / 255.0;
                float qB = float((packedData.w >> 24u) & 0x3Fu) / 63.0;
                uint mode = (packedData.w >> 30u) & 0x03u;

                const float invSqrt2 = 0.70710678118;
                float a = (qR * 2.0 - 1.0) * invSqrt2;
                float b = (qG * 2.0 - 1.0) * invSqrt2;
                float c = (qB * 2.0 - 1.0) * invSqrt2;

                float d = sqrt(max(0.0, 1.0 - (a * a + b * b + c * c)));

                float qw, qx, qy, qz;
                if (mode == 0u)      { qw = d; qx = a; qy = b; qz = c; }
                else if (mode == 1u) { qw = a; qx = d; qy = b; qz = c; }
                else if (mode == 2u) { qw = a; qx = b; qy = d; qz = c; }
                else                 { qw = a; qx = b; qy = c; qz = d; }

                return float4(qw, -qx, -qy, qz);
            }

            void UnpackSplat(uint4 packedData, out float4 color, out float3 modelCenter, out float3 scale, out float4 quat)
            {
                color = float4(packedData.x & 0xFFU, (packedData.x >> 8u) & 0xFFU, (packedData.x >> 16u) & 0xFFU, (packedData.x >> 24u) & 0xFFU) / 255.0;
                modelCenter = float3(f16tof32(packedData.y & 0xFFFFU), f16tof32((packedData.y >> 16u) & 0xFFFFU), f16tof32(packedData.z & 0xFFFFU));

                uint3 uScale = uint3(packedData.w & 0xFFU, (packedData.w >> 8u) & 0xFFU, (packedData.w >> 16u) & 0xFFU);
                float lnScaleScale = (9.0 - (-12.0)) / 254.0;
                scale = float3(
                    (uScale.x == 0u) ? 0.0 : exp(-12.0 + float(uScale.x - 1u) * lnScaleScale),
                    (uScale.y == 0u) ? 0.0 : exp(-12.0 + float(uScale.y - 1u) * lnScaleScale),
                    (uScale.z == 0u) ? 0.0 : exp(-12.0 + float(uScale.z - 1u) * lnScaleScale)
                );

                quat = DecodeQuatSogV2(packedData);
            }

                        v2f vert(appdata v)
            {
                v2f o;
                o.vertex = float4(0.0, 0.0, 2.0, 1.0);
                o.uv = float2(0, 0);
                o.color = float4(0, 0, 0, 0);

                uint subInstanceId = (uint)round(v.vertex.z);
                uint globalSplatOrder = v.instanceID * 128U + subInstanceId;

                if (globalSplatOrder >= _SplatCount) 
                    return o;

                uint splatId = _OrderBuffer[globalSplatOrder];
                uint4 packedSplat = _PositionBuffer[splatId];
    
                float3 modelCenter, scale;
                float4 quat, color;
                UnpackSplat(packedSplat, color, modelCenter, scale, quat);

                // Early alpha cull before any matrix or covariance math
                if (color.a < (1.0 / 255.0))
                    return o;

                float4x4 modelView = mul(UNITY_MATRIX_V, _MATRIX_M);
                SplatCenter center;
                if (!InitCenter(modelView, modelCenter, center)) 
                    return o;

                // --- EARLY FRUSTUM CULLING (Skips Covariance & 15x SH3 Buffer Lookups!) ---
                // Guard-band of 1.25 prevents large splats near screen edges from popping
                float guardW = center.proj.w * 1.25;
                if (center.proj.w <= 0.0 ||
                    abs(center.proj.x) > guardW ||
                    abs(center.proj.y) > guardW)
                {
                    return o;
                }

                SplatCovariance cov = CalcCovariance(quat, scale);
                SplatCorner corner;
                SplatSource src;
                src.cornerUV = v.vertex.xy * _ScaleFactor;

                if (!InitCorner(src, cov, center, false, corner)) 
                    return o;

            #ifndef SH_BANDS_0
                // Original Yize Wu view-direction transform (transposed mul flips sign natively)
                float3 dir = normalize(mul(center.view, (float3x3)center.modelView));
                dir.z = -dir.z;

                uint labelIdx = _ScaleBuffer[splatId];
                uint coeffsPerSplat = (_SHDegree == 1) ? 3u : ((_SHDegree == 2) ? 8u : 15u);
                uint baseOffset = labelIdx * coeffsPerSplat;

                float3 sh[SH_COEFFS];
                [unroll]
                for (uint k = 0u; k < SH_COEFFS; ++k)
                {
                    sh[k] = (k < coeffsPerSplat) ? _SHCentroidsBuffer[baseOffset + k].xyz : float3(0.0, 0.0, 0.0);
                }

                color.rgb = max(0.0, color.rgb + EvalSH(sh, dir, _SHDegree));
            #endif

                ClipCorner(corner, color.a);

                o.vertex = center.proj + float4(corner.offset.x, _ProjectionParams.x * corner.offset.y, 0, 0);
                o.color = color;
                o.uv = corner.uv;
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {
                float A = dot(i.uv, i.uv);
                if (A > 1.0) discard;

                float2 absUV = abs(i.uv);
                float maxUV = max(absUV.x, absUV.y);
                float falloff = -exp((maxUV - _ScaleFactor * 1.16) * 25 * _ScaleFactor);
                float alpha = (exp(-A * 4.0) + falloff) * i.color.a;

                if (alpha < 1.0 / 255.0) discard;

                float3 color = _GammaToLinear ? GammaToLinearSpace(i.color.rgb) : i.color.rgb;
                return float4(color * alpha * _Brightness, alpha);
            }
            ENDHLSL
        }
    }
}
