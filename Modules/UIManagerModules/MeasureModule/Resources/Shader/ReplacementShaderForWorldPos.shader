//does not work
//tries:
//https://discussions.unity.com/t/render-depth-to-rendertexture-and-calculate-world-position/85880/2
//https://discussions.unity.com/t/unityobjecttoviewpos-projectionparams-w-vs-unityobjecttoclippos/787611
//https://discussions.unity.com/t/object-depth-shader/640398/6

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/ReplacementShaderForWorldPos"
{
	SubShader {
		Tags { "RenderType"="Opaque" }
        //LOD 200
        //Cull off
		
		CGINCLUDE

        #include "UnityCG.cginc"

		struct VIN {
			float4 pos : POSITION0;
		};

		struct V2F {
			float4 clipPos  : POSITION0;
			//float4 p	:TEXCOORD0;
            //float depth : TEXCOORD1;
		};

		V2F myVert( VIN i ) {
			V2F o;
			o.clipPos = UnityObjectToClipPos( i.pos );
            //o.pos = mul(UNITY_MATRIX_MV, i.pos);
            //o.depth = o.pos.z;
		    //o.sPos = ComputeScreenPos(o.pos);   //UnityObjectToClipPos(  i.pos );
            //o.p = UnityObjectToClipPos( i.pos );
			return o;
		}

		float4 myFrag(V2F i) : COLOR {
			//camera farplane is hardcoded to 100
            //TODO: pass it like any other parameter from the unity script
			
            //return float4(i.p.z / 100, i.p.z / 100, i.p.z / 100, 1);

            //float depth = clamp(LinearEyeDepth(-i.p.z/100), 0, 1);
            //float depth = LinearEyeDepth((i.p.z/100)/(i.p.w/100));
            
            //float clipSpaceRange01 = UNITY_Z_0_FAR_FROM_CLIPSPACE(i.p.z);
            //float depth = clipSpaceRange01;

            // float depth = tex2D(_CameraDepthTexture, i.p);
            // #if defined(UNITY_REVERSED_Z)
            //     depth = 1.0f - z;
            // #endif

            //float depth = ComputeScreenPos(i.pos).z;
            //float depth = i.sPos.z;

            float linearDepth01 = Linear01Depth(i.clipPos.z / i.clipPos.w);
            float depth = linearDepth01;
            //float depth = (i.pos.z / i.pos.w);

            //float depth = i.pos.w;
            //float depth = LinearEyeDepth(i.p.z);
            #if defined(UNITY_REVERSED_Z)
                depth = 1.0f - depth;
            #endif

        /*
        #ifdef UNITY_REVERSED_Z
            // unity_CameraInvProjection always in OpenGL matrix form
            // that doesn't match the current view matrix used to calculate the clip space

            // transform clip space into normalized device coordinates
            float3 ndc = i.p.xyz / i.p.w;

            // convert ndc's depth from 1.0 near to 0.0 far to OpenGL style -1.0 near to 1.0 far 
            depth = (1.0 - ndc.z) * 2.0 - 1.0;
        #endif
        */

            return float4(depth, depth, depth, 1);

        /* colorful texture
        #ifdef UNITY_REVERSED_Z
            // unity_CameraInvProjection always in OpenGL matrix form
            // that doesn't match the current view matrix used to calculate the clip space

            // transform clip space into normalized device coordinates
            float3 ndc = i.p.xyz / i.p.w;

            // convert ndc's depth from 1.0 near to 0.0 far to OpenGL style -1.0 near to 1.0 far 
            ndc = float3(ndc.x, ndc.y * _ProjectionParams.x, (1.0 - ndc.z) * 2.0 - 1.0);

            // transform back into clip space and apply inverse projection matrix
            float3 viewPos =  mul(unity_CameraInvProjection, float4(ndc * i.p.w, i.p.w));
        #else
            // using OpenGL, unity_CameraInvProjection matches view matrix
            float3 viewPos = mul(unity_CameraInvProjection, i.p);
        #endif

            // transform from view to world space
            return mul(unity_MatrixInvV, float4(viewPos, 1.0));
        */
		} 

		ENDCG

		pass {
			CGPROGRAM
			#pragma fragment myFrag
			#pragma vertex myVert
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma target 2.0
			ENDCG
		}
	} 
}