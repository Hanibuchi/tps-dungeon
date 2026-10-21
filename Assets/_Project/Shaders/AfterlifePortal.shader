// 死後の場所の転生の渦。水面に寝かせた Quad（UV が 0〜1）に、ゆっくり回る光の渦を加算で描く。
// 中心ほど明るく、外へ向かって渦の腕が伸びて消える。値は素材のインスペクタで変えればその場で反映される。
Shader "TpsDungeon/Afterlife Portal"
{
    Properties
    {
        [HDR] _Color("渦の色", Color) = (0.5, 1.3, 1.8, 1)
        [HDR] _CoreColor("中心の色", Color) = (1.5, 2.2, 2.6, 1)
        _Arms("渦の腕の数", Float) = 3
        _Twist("ねじれ", Float) = 5
        _Speed("回る速さ（1 秒あたりの回転）", Float) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Portal"
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
                half4 _CoreColor;
                float _Arms;
                float _Twist;
                float _Speed;
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
                float2 p = (input.uv - 0.5) * 2.0;
                float r = length(p);
                if (r > 1.0) discard;

                float angle = atan2(p.y, p.x);
                float spiral = sin(angle * _Arms + r * _Twist - _Time.y * _Speed * 6.2831853);
                float arms = smoothstep(0.2, 1.0, spiral) * (1.0 - r);
                float core = pow(saturate(1.0 - r * 2.2), 2.0);
                float edge = smoothstep(1.0, 0.85, r) * smoothstep(0.7, 0.95, r) * 0.5;

                half3 color = _Color.rgb * (arms + edge) + _CoreColor.rgb * core;
                return half4(color, saturate(arms + edge + core));
            }
            ENDHLSL
        }
    }
}
