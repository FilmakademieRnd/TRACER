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
            StructuredBuffer<uint4> _PositionBuffer;        // Remapped to hold the packed uint4 geometry records
            StructuredBuffer<uint>  _OrderBuffer;           // Sorted GPU indices array
            
            StructuredBuffer<uint4> _ProjectedSplatBuffer;  // Compact 32-byte (2 x uint4) pre-projected splat buffer
            
            #ifndef SH_BANDS_0
            StructuredBuffer<uint>  _EvaluatedColorBuffer;  // Pre-evaluated 10-10-10 HDR RGB color from CalcDepth
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
                uint baseIdx = splatId * 2u;

                float4 centerClip = asfloat(_ProjectedSplatBuffer[baseIdx]);
                if (centerClip.w <= 0.0)
                    return o;

                uint4 axesAndColor = _ProjectedSplatBuffer[baseIdx + 1u];

                float2 axis1 = float2(f16tof32(axesAndColor.x & 0xFFFFu), f16tof32(axesAndColor.x >> 16u));
                float2 axis2 = float2(f16tof32(axesAndColor.y & 0xFFFFu), f16tof32(axesAndColor.y >> 16u));
                float uvScale = f16tof32(axesAndColor.z & 0xFFFFu);

                float2 offset = v.vertex.x * axis1 + v.vertex.y * axis2;
                o.vertex = centerClip + float4(offset.x, _ProjectionParams.x * offset.y, 0.0, 0.0);
                o.uv = v.vertex.xy * uvScale;

                uint c = axesAndColor.w;
                o.color = float4(
                    (c & 0xFFu),
                    ((c >> 8u) & 0xFFu),
                    ((c >> 16u) & 0xFFu),
                    ((c >> 24u) & 0xFFu)
                ) * (1.0 / 255.0);

                return o;
            }


            // float4 frag(v2f i) : SV_Target
            // {
            //     float A = dot(i.uv, i.uv);
            //     if (A > 1.0) discard;

            //     float2 absUV = abs(i.uv);
            //     float maxUV = max(absUV.x, absUV.y);
            //     float falloff = -exp((maxUV - _ScaleFactor * 1.16) * 25 * _ScaleFactor);
            //     float alpha = (exp(-A * 4.0) + falloff) * i.color.a;

            //     if (alpha < 1.0 / 255.0) discard;

            //     float3 color = _GammaToLinear ? GammaToLinearSpace(i.color.rgb) : i.color.rgb;
            //     return float4(color * alpha * _Brightness, alpha);
            // }

           half4 frag(v2f i) : SV_Target
            {
                half2 uv = (half2)i.uv;
                half A = dot(uv, uv);

                // Pure FP16 Gaussian approximation (zero exp() SFU stalls, zero discard TBDR penalties)
                half t = saturate((half)1.0 - A);
                half t2 = t * t;
                half gaussian = t2 * t2;

                half alpha = gaussian * (half)i.color.a;
                return half4((half3)i.color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
        
        // ====================================================================
        // PASS 2: SHADOW CASTER (Toggleable via CastShadows in GsplatRenderer)
        // ====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma target 4.5

            #include "Gsplat.hlsl"
            #include "UnityCG.cginc"
            #include "Gsplat.hlsl"

            uint _SplatCount;
            float _ScaleFactor;
            float4x4 _MATRIX_M;

            StructuredBuffer<uint> _OrderBuffer;
            StructuredBuffer<uint4> _PositionBuffer;

            struct appdataShadow
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2fShadow
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD1;
                float alpha : TEXCOORD2;
            };

            float4 DecodeQuatShadow(uint4 packedData)
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

            v2fShadow vertShadow(appdataShadow v)
            {
                v2fShadow o;
                o.pos = float4(0.0, 0.0, 2.0, 1.0);
                o.uv = float2(0.0, 0.0);
                o.alpha = 0.0;

                uint subInstanceId = (uint)round(v.vertex.z);
                uint globalSplatOrder = v.instanceID * 128U + subInstanceId;
                if (globalSplatOrder >= _SplatCount)
                    return o;

                uint splatId = _OrderBuffer[globalSplatOrder];
                uint4 packedData = _PositionBuffer[splatId];

                float alpha = float((packedData.x >> 24u) & 0xFFu) * (1.0 / 255.0);
                if (alpha < 0.25) // Skip faint splats in shadow pass for crisp, fast shadows
                    return o;

                float3 modelCenter = float3(
                    f16tof32(packedData.y & 0xFFFFu),
                    f16tof32((packedData.y >> 16u) & 0xFFFFu),
                    f16tof32(packedData.z & 0xFFFFu)
                );

                float4x4 modelView = mul(UNITY_MATRIX_V, _MATRIX_M);
                SplatCenter center;
                if (!InitCenter(modelView, modelCenter, center))
                    return o;

                uint3 uScale = uint3(packedData.w & 0xFFu, (packedData.w >> 8u) & 0xFFu, (packedData.w >> 16u) & 0xFFu);
                const float lnScaleScale = (9.0 - (-12.0)) / 254.0;
                float3 scale = float3(
                    (uScale.x == 0u) ? 0.0 : exp(-12.0 + float(uScale.x - 1u) * lnScaleScale),
                    (uScale.y == 0u) ? 0.0 : exp(-12.0 + float(uScale.y - 1u) * lnScaleScale),
                    (uScale.z == 0u) ? 0.0 : exp(-12.0 + float(uScale.z - 1u) * lnScaleScale)
                );
                float4 quat = DecodeQuatShadow(packedData);

                SplatCovariance cov = CalcCovariance(quat, scale);
                SplatCorner corner;
                SplatSource src;
                src.cornerUV = v.vertex.xy * _ScaleFactor;

                if (!InitCorner(src, cov, center, false, corner))
                    return o;

                ClipCorner(corner, alpha);

                float4 clipPos = center.proj + float4(corner.offset.x, _ProjectionParams.x * corner.offset.y, 0.0, 0.0);
                o.pos = UnityApplyLinearShadowBias(clipPos);
                o.uv = corner.uv;
                o.alpha = alpha;
                return o;
            }

            fixed4 fragShadow(v2fShadow i) : SV_Target
            {
                float A = dot(i.uv, i.uv);
                float gaussian = exp(-A * 4.0);
                clip(gaussian * i.alpha - 0.35);
                return 0;
            }
            ENDCG
        }
        // ====================================================================
        // PASS 3: SELF-SHADOW DEPTH (Writes splat depth into _SplatShadowMap)
        // ====================================================================
        Pass
        {
            Name "SplatShadowDepth"
            Tags { "LightMode" = "GsplatSelfShadow" }
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vertSelfShadow
            #pragma fragment fragSelfShadow
            #pragma target 4.5

            #include "UnityCG.cginc"
            #define SH_BANDS_0
            #include "Gsplat.hlsl"

            uint _SplatCount;
            float _ScaleFactor;
            float2 _ShadowMapSize;
            float4x4 _MATRIX_M;
            float4x4 _ShadowViewMatrix;
            float4x4 _ShadowProjMatrix;

            StructuredBuffer<uint> _OrderBuffer;
            StructuredBuffer<uint4> _PositionBuffer;

            struct appdataSelfShadow
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2fSelfShadow
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                float depth : TEXCOORD2;
            };

            float4 DecodeQuatSelfShadow(uint4 packedData)
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

            v2fSelfShadow vertSelfShadow(appdataSelfShadow v)
            {
                v2fSelfShadow o;
                o.pos = float4(0.0, 0.0, 2.0, 1.0);
                o.uv = float2(0.0, 0.0);
                o.alpha = 0.0;
                o.depth = 0.0;

                uint subInstanceId = (uint)round(v.vertex.z);
                uint globalSplatOrder = v.instanceID * 128U + subInstanceId;
                if (globalSplatOrder >= _SplatCount)
                    return o;

                uint splatId = _OrderBuffer[globalSplatOrder];
                uint4 packedData = _PositionBuffer[splatId];

                float alpha = float((packedData.x >> 24u) & 0xFFu) * (1.0 / 255.0);
                if (alpha < 0.35) // Only solid core splats cast self-shadows (avoids noisy fog shadows)
                    return o;

                float3 modelCenter = float3(
                    f16tof32(packedData.y & 0xFFFFu),
                    f16tof32((packedData.y >> 16u) & 0xFFFFu),
                    f16tof32(packedData.z & 0xFFFFu)
                );

                float4x4 modelView = mul(_ShadowViewMatrix, _MATRIX_M);
                float4 centerView = mul(modelView, float4(modelCenter, 1.0));
                if (centerView.z > 0.0)
                    return o;

                float4 centerProj = mul(_ShadowProjMatrix, centerView);
                centerProj.z = clamp(centerProj.z, -abs(centerProj.w), abs(centerProj.w));

                SplatCenter center;
                center.view = centerView.xyz / centerView.w;
                center.proj = centerProj;
                center.projMat00 = _ShadowProjMatrix[0][0];
                center.modelView = modelView;

                uint3 uScale = uint3(packedData.w & 0xFFu, (packedData.w >> 8u) & 0xFFu, (packedData.w >> 16u) & 0xFFu);
                const float lnScaleScale = (9.0 - (-12.0)) / 254.0;
                float3 scale = float3(
                    (uScale.x == 0u) ? 0.0 : exp(-12.0 + float(uScale.x - 1u) * lnScaleScale),
                    (uScale.y == 0u) ? 0.0 : exp(-12.0 + float(uScale.y - 1u) * lnScaleScale),
                    (uScale.z == 0u) ? 0.0 : exp(-12.0 + float(uScale.z - 1u) * lnScaleScale)
                );
                float4 quat = DecodeQuatSelfShadow(packedData);

                SplatCovariance cov = CalcCovariance(quat, scale);

                // Project ellipse into the 1024x1024 shadow map
                float3x3 Vrk = float3x3(
                    cov.covA.x, cov.covA.y, cov.covA.z,
                    cov.covA.y, cov.covB.x, cov.covB.y,
                    cov.covA.z, cov.covB.y, cov.covB.z
                );

                float focal = _ShadowMapSize.x * center.projMat00;
                float3 viewPos = (_ShadowProjMatrix[3][3] == 1.0) ? float3(0.0, 0.0, 1.0) : center.view.xyz;
                float J1 = focal / viewPos.z;
                float2 J2 = -J1 / viewPos.z * viewPos.xy;
                float3x3 J = float3x3(J1, 0.0, J2.x, 0.0, J1, J2.y, 0.0, 0.0, 0.0);
                float3x3 T = mul(J, (float3x3)modelView);
                float3x3 cov2D = mul(mul(T, Vrk), transpose(T));

                float d1 = cov2D[0][0] + 0.3;
                float off = cov2D[0][1];
                float d2 = cov2D[1][1] + 0.3;
                float mid = 0.5 * (d1 + d2);
                float rad = length(float2((d1 - d2) * 0.5, off));
                float l1 = 2.0 * min(sqrt(2.0 * (mid + rad)), 512.0);
                float l2 = 2.0 * min(sqrt(2.0 * max(mid - rad, 0.1)), 512.0);

                if (l1 < 1.5 && l2 < 1.5)
                    return o;

                float2 c = centerProj.ww / _ShadowMapSize;
                float2 diagVec = normalize(float2(off, (mid + rad) - d1));
                float clipScale = min(1.0, sqrt(-log((1.0 / 255.0) / alpha)) * 0.5);
                float2 cornerUV = v.vertex.xy * _ScaleFactor * clipScale;
                float2 offset = (cornerUV.x * (l1 * diagVec) + cornerUV.y * (l2 * float2(diagVec.y, -diagVec.x))) * c;

                o.pos = centerProj + float4(offset.x, _ProjectionParams.x * offset.y, 0.0, 0.0);
                o.uv = cornerUV;
                o.alpha = alpha;
                o.depth = o.pos.z / o.pos.w;
                return o;
            }

            float4 fragSelfShadow(v2fSelfShadow i) : SV_Target
            {
                float A = dot(i.uv, i.uv);
                float gaussian = exp(-A * 4.0);
                clip(gaussian * i.alpha - 0.45);
                return float4(i.depth, 0.0, 0.0, 1.0);
            }
            ENDCG
        }

    }
}
