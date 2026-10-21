// 死後の場所の光の粒（沈んでいく粒・燭台の火・転生の渦から昇る粒）。パーティクルの四角に、丸くぼけた光を加算で描く。
// 色はパーティクルの頂点色に _Color を掛けたもの。
Shader "TpsDungeon/Afterlife Mote"
{
    Properties
    {
        [HDR] _Color("色", Color) = (0.6, 1.2, 1.6, 1)
        _Softness("ぼけ具合（大きいほど芯が小さい）", Range(0.5, 6)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Mote"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = saturate(1.0 - length(input.uv - 0.5) * 2.0);
                float glow = pow(d, _Softness);
                return half4(_Color.rgb * input.color.rgb, glow * input.color.a * _Color.a);
            }
            ENDHLSL
        }
    }
}
