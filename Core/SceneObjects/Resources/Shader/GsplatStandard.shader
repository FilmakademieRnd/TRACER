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
            #pragma target 4.5

            #include "UnityCG.cginc"

            // Pre-projected & already depth-sorted 32-byte (2 x uint4) splat buffer (100% linear coalesced reads)
            StructuredBuffer<uint4> _ProjectedSplatBuffer;
            uint _SplatCount;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = float4(0.0, 0.0, 2.0, 1.0);
                o.uv = half2(0.0, 0.0);
                o.color = half4(0.0, 0.0, 0.0, 0.0);

                uint subInstanceId = (uint)round(v.vertex.z);
                uint globalSplatOrder = v.instanceID * 128U + subInstanceId;
                if (globalSplatOrder >= _SplatCount)
                    return o;

                // Direct linear read in sorted order (eliminates 29.2M random _OrderBuffer fetches & WebGPU bounds checks)
                uint baseIdx = globalSplatOrder * 2u;

                float4 centerClip = asfloat(_ProjectedSplatBuffer[baseIdx]);
                if (centerClip.w <= 0.0)
                    return o;

                uint4 axesAndColor = _ProjectedSplatBuffer[baseIdx + 1u];

                float2 axis1 = float2(f16tof32(axesAndColor.x & 0xFFFFu), f16tof32(axesAndColor.x >> 16u));
                float2 axis2 = float2(f16tof32(axesAndColor.y & 0xFFFFu), f16tof32(axesAndColor.y >> 16u));
                half uvScale = (half)f16tof32(axesAndColor.z & 0xFFFFu);

                float2 offset = v.vertex.x * axis1 + v.vertex.y * axis2;
                o.vertex = centerClip + float4(offset.x, _ProjectionParams.x * offset.y, 0.0, 0.0);
                o.uv = (half2)v.vertex.xy * uvScale;

                uint c = axesAndColor.w;
                o.color = half4(
                    (c & 0xFFu),
                    ((c >> 8u) & 0xFFu),
                    ((c >> 16u) & 0xFFu),
                    ((c >> 24u) & 0xFFu)
                ) * (half)(1.0 / 255.0);

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half A = dot(i.uv, i.uv);

                // Pure FP16 Gaussian approximation (zero exp() SFU stalls, zero discard TBDR penalties)
                half t = saturate((half)1.0 - A);
                half t2 = t * t;
                half gaussian = t2 * t2;

                half alpha = gaussian * i.color.a;
                return half4(i.color.rgb * alpha, alpha);
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

            #include "UnityCG.cginc"
            #define SH_BANDS_0
            #include "Gsplat.hlsl"

            uint _SplatCount;
            float _ScaleFactor;
            float4x4 _MATRIX_M;

            StructuredBuffer<uint4> _PositionBuffer;

            struct appdataShadow
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2fShadow
            {
                float4 pos : SV_POSITION;
                half2 uv : TEXCOORD1;
                half maxRadiusSq : TEXCOORD2;
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
                o.uv = half2(0.0, 0.0);
                o.maxRadiusSq = (half)0.0;

                uint subInstanceId = (uint)round(v.vertex.z);
                uint splatId = v.instanceID * 128U + subInstanceId;
                if (splatId >= _SplatCount)
                    return o;

                // Direct linear read (no _OrderBuffer needed for ZWrite depth shadow pass!)
                uint4 packedData = _PositionBuffer[splatId];

                float alpha = float((packedData.x >> 24u) & 0xFFu) * (1.0 / 255.0);
                if (alpha <= 0.35) // Exact threshold where exp(-4*A)*alpha can ever exceed 0.35
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

                // Early shadow frustum cull before covariance math
                float guardW = center.proj.w * 1.25;
                if (center.proj.w <= 0.0 || abs(center.proj.x) > guardW || abs(center.proj.y) > guardW)
                    return o;

                uint3 uScale = uint3(packedData.w & 0xFFu, (packedData.w >> 8u) & 0xFFu, (packedData.w >> 16u) & 0xFFu);
                const float lnScaleStepLog2 = ((9.0 - (-12.0)) / 254.0) * 1.44269504089;
                const float minScaleLog2 = -12.0 * 1.44269504089;
                float3 scale = float3(
                    (uScale.x == 0u) ? 0.0 : exp2(minScaleLog2 + float(uScale.x - 1u) * lnScaleStepLog2),
                    (uScale.y == 0u) ? 0.0 : exp2(minScaleLog2 + float(uScale.y - 1u) * lnScaleStepLog2),
                    (uScale.z == 0u) ? 0.0 : exp2(minScaleLog2 + float(uScale.z - 1u) * lnScaleStepLog2)
                );
                float4 quat = DecodeQuatShadow(packedData);

                SplatCovariance cov = CalcCovariance(quat, scale);
                SplatCorner corner;
                SplatSource src;
                src.cornerUV = v.vertex.xy * _ScaleFactor;

                if (!InitCorner(src, cov, center, false, corner))
                    return o;

                // Tight clip exact to the 0.35 shadow cutout threshold (shrinks shadow quad size!)
                float maxRadiusSq = 0.25 * (log(alpha) + 1.04982212); // exact solution to exp(-4*A)*alpha = 0.35
                float tightClip = min(1.0, sqrt(max(0.0, maxRadiusSq)));
                corner.offset *= tightClip;
                corner.uv *= tightClip;

                float4 clipPos = center.proj + float4(corner.offset.x, _ProjectionParams.x * corner.offset.y, 0.0, 0.0);
                o.pos = UnityApplyLinearShadowBias(clipPos);
                o.uv = (half2)corner.uv;
                o.maxRadiusSq = (half)maxRadiusSq;
                return o;
            }

            fixed4 fragShadow(v2fShadow i) : SV_Target
            {
                // Exact analytical cutout without exp() in the pixel shader!
                clip(i.maxRadiusSq - dot(i.uv, i.uv));
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

            StructuredBuffer<uint4> _PositionBuffer;

            struct appdataSelfShadow
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2fSelfShadow
            {
                float4 pos : SV_POSITION;
                half2 uv : TEXCOORD0;
                half maxRadiusSq : TEXCOORD1;
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
                o.uv = half2(0.0, 0.0);
                o.maxRadiusSq = (half)0.0;
                o.depth = 0.0;

                uint subInstanceId = (uint)round(v.vertex.z);
                uint splatId = v.instanceID * 128U + subInstanceId;
                if (splatId >= _SplatCount)
                    return o;

                // Direct linear read (no _OrderBuffer needed for ZWrite depth pass!)
                uint4 packedData = _PositionBuffer[splatId];

                float alpha = float((packedData.x >> 24u) & 0xFFu) * (1.0 / 255.0);
                if (alpha <= 0.45) // Exact threshold where exp(-4*A)*alpha can ever exceed 0.45
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
                float guardW = centerProj.w * 1.25;
                if (centerProj.w <= 0.0 || abs(centerProj.x) > guardW || abs(centerProj.y) > guardW)
                    return o;

                centerProj.z = clamp(centerProj.z, -abs(centerProj.w), abs(centerProj.w));

                uint3 uScale = uint3(packedData.w & 0xFFu, (packedData.w >> 8u) & 0xFFu, (packedData.w >> 16u) & 0xFFu);
                const float lnScaleStepLog2 = ((9.0 - (-12.0)) / 254.0) * 1.44269504089;
                const float minScaleLog2 = -12.0 * 1.44269504089;
                float3 scale = float3(
                    (uScale.x == 0u) ? 0.0 : exp2(minScaleLog2 + float(uScale.x - 1u) * lnScaleStepLog2),
                    (uScale.y == 0u) ? 0.0 : exp2(minScaleLog2 + float(uScale.y - 1u) * lnScaleStepLog2),
                    (uScale.z == 0u) ? 0.0 : exp2(minScaleLog2 + float(uScale.z - 1u) * lnScaleStepLog2)
                );
                float4 quat = DecodeQuatSelfShadow(packedData);

                SplatCovariance cov = CalcCovariance(quat, scale);

                float3x3 Vrk = float3x3(
                    cov.covA.x, cov.covA.y, cov.covA.z,
                    cov.covA.y, cov.covB.x, cov.covB.y,
                    cov.covA.z, cov.covB.y, cov.covB.z
                );

                float focal = _ShadowMapSize.x * _ShadowProjMatrix[0][0];
                float3 viewPos = (_ShadowProjMatrix[3][3] == 1.0) ? float3(0.0, 0.0, 1.0) : (centerView.xyz / centerView.w);
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

                // Tight clip exact to the 0.45 self-shadow cutout threshold (exp(-4*A)*alpha = 0.45)
                float maxRadiusSq = 0.25 * (log(alpha) + 0.79850770);
                float tightClip = min(1.0, sqrt(max(0.0, maxRadiusSq)));

                float2 cornerUV = v.vertex.xy * _ScaleFactor * tightClip;
                float2 offset = (cornerUV.x * (l1 * diagVec) + cornerUV.y * (l2 * float2(diagVec.y, -diagVec.x))) * c;

                o.pos = centerProj + float4(offset.x, _ProjectionParams.x * offset.y, 0.0, 0.0);
                o.uv = (half2)cornerUV;
                o.maxRadiusSq = (half)maxRadiusSq;
                o.depth = o.pos.z / o.pos.w;
                return o;
            }

            float4 fragSelfShadow(v2fSelfShadow i) : SV_Target
            {
                // Exact analytical cutout without exp() in the pixel shader!
                clip(i.maxRadiusSq - dot(i.uv, i.uv));
                return float4(i.depth, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
        // ====================================================================
        // PASS 4: TRACER OBJECT ID SELECTION (Used by EvaluationHelper)
        // ====================================================================
        Pass
        {
            Name "SelectableId"
            Tags { "LightMode" = "GsplatSelectable" "SelectableType" = "Selectable" }

            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vertSelectableId
            #pragma fragment fragSelectableId
            #pragma target 4.5

            #include "UnityCG.cginc"

            StructuredBuffer<uint4> _ProjectedSplatBuffer;
            uint _SplatCount;
            fixed4 _SelectableId;

            struct appdataId
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2fId
            {
                float4 vertex : SV_POSITION;
                half2 uv : TEXCOORD0;
                half maxRadiusSq : TEXCOORD1;
            };

            v2fId vertSelectableId(appdataId v)
            {
                v2fId o;
                o.vertex = float4(0.0, 0.0, 2.0, 1.0);
                o.uv = half2(0.0, 0.0);
                o.maxRadiusSq = (half)0.0;

                uint subInstanceId = (uint)round(v.vertex.z);
                uint globalSplatOrder = v.instanceID * 128U + subInstanceId;
                if (globalSplatOrder >= _SplatCount)
                    return o;

                uint baseIdx = globalSplatOrder * 2u;
                float4 centerClip = asfloat(_ProjectedSplatBuffer[baseIdx]);
                if (centerClip.w <= 0.0)
                    return o;

                uint4 axesAndColor = _ProjectedSplatBuffer[baseIdx + 1u];
                float alpha = float((axesAndColor.w >> 24u) & 0xFFu) * (1.0 / 255.0);
                if (alpha <= 0.25)
                    return o;

                float2 axis1 = float2(f16tof32(axesAndColor.x & 0xFFFFu), f16tof32(axesAndColor.x >> 16u));
                float2 axis2 = float2(f16tof32(axesAndColor.y & 0xFFFFu), f16tof32(axesAndColor.y >> 16u));
                float uvScale = f16tof32(axesAndColor.z & 0xFFFFu);

                // Exact analytical radius where (1 - A)^4 * alpha = 0.25 -> A = 1 - (0.25 / alpha)^0.25
                float maxRadiusSq = max(0.0, 1.0 - pow(0.25 / alpha, 0.25));
                float tightClip = min(1.0, sqrt(maxRadiusSq));

                float2 offset = (v.vertex.x * axis1 + v.vertex.y * axis2) * tightClip;
                o.vertex = centerClip + float4(offset.x, _ProjectionParams.x * offset.y, 0.0, 0.0);
                o.uv = (half2)(v.vertex.xy * uvScale * tightClip);
                o.maxRadiusSq = (half)maxRadiusSq;
                return o;
            }

            fixed4 fragSelectableId(v2fId i) : SV_Target
            {
                clip(i.maxRadiusSq - dot(i.uv, i.uv));
                return _SelectableId;
            }
            ENDCG
        }
    }
}
