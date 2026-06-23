Shader "Tracer/GizmoUnlit"{
    Properties{
        _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4 // 4 = LessEqual, 6 = Greater
    }
    
    SubShader{
        // "Queue"="Transparent" guarantees it renders AFTER the opaque walls have generated the Depth Buffer
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off      // solves the Two-Sided issue instantly
        ZWrite Off    // solves the weird rectangle artifact entirely
        ZTest [_ZTest]

        Pass{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata{
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // catches the UI Canvas Image.color!
            };

            struct v2f{
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;

            v2f vert (appdata v){
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color; // Pass Canvas color to the fragment
                return o;
            }

            fixed4 frag (v2f i) : SV_Target{
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}