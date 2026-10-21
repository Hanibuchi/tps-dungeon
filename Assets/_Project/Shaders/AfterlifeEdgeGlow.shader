// 死後の場所の足場の縁に引く淡い光の線。寝かせた細い Quad（V が幅の向き）に、幅の真ん中ほど明るい光を加算で描く。
// 暗い中でも、歩ける所と水の境目が分かるように。
Shader "TpsDungeon/Afterlife Edge Glow"
{
    Properties
    {
        [HDR] _Color("色", Color) = (0.25, 0.6, 0.75, 1)
        _Softness("ぼけ具合（大きいほど細い）", Range(0.5, 6)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "EdgeGlow"
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = saturate(1.0 - abs(input.uv.y - 0.5) * 2.0);
                float glow = pow(across, _Softness);
                return half4(_Color.rgb, glow * _Color.a);
            }
            ENDHLSL
        }
    }
}
