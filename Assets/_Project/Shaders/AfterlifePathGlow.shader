// 死後の場所の道しるべ。足場に寝かせた小さな Quad に丸い淡い光を加算で描き、明るさの波を行き先へ向けて流す。
// 波の位置は、粒の置き場所を _Axis（道の向き）に沿って測った距離＋_Offset（m）で決まるので、道の向きへ光が送られていくように見える。
// 道の折れ目ごとに材質を分け、_Offset で前の区間の続きの距離にする。
Shader "TpsDungeon/Afterlife Path Glow"
{
    Properties
    {
        [HDR] _Color("色", Color) = (0.45, 0.85, 1.0, 1)
        _Softness("ぼけ具合（大きいほど芯が小さい）", Range(0.5, 6)) = 2.2
        _Base("波の無い所の明るさ", Range(0, 1)) = 0.35
        _WaveLength("波の間隔 (m)", Float) = 6
        _WaveSpeed("波の速さ (m/秒)", Float) = 3
        _Axis("道の向き（ワールド、xyz）", Vector) = (0, 0, 1, 0)
        _Offset("道に沿った距離のずらし (m)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "PathGlow"
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
                float _Base;
                float _WaveLength;
                float _WaveSpeed;
                float4 _Axis;
                float _Offset;
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
                float phase : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                // 粒の真ん中（物体の原点）で測るので、1 つの粒は全体が同じ明るさになる。
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                output.phase = dot(origin, _Axis.xyz) + _Offset;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = saturate(1.0 - length(input.uv - 0.5) * 2.0);
                float glow = pow(d, _Softness);
                // 0〜1 の鋸の波を、とがった山にする。距離が増える向きへ流れる。
                float t = frac((_Time.y * _WaveSpeed - input.phase) / max(_WaveLength, 0.01));
                float wave = pow(saturate(1.0 - abs(t - 0.5) * 2.0), 3.0);
                float brightness = lerp(_Base, 1.0, wave);
                return half4(_Color.rgb * brightness, glow * _Color.a);
            }
            ENDHLSL
        }
    }
}
