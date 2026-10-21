// 死後の場所（Afterlife）で、パーティーを不気味に光る半透明の魂に見せる。
// 縁（視線に対して寝ている面）ほど強く光り（フレネル）、中は薄く透ける。ワールドの上へ流れるノイズで光がゆらぎ、全体がゆっくり明滅する。
// 加算で重ねるので、暗い所ほど目立つ。先に深度だけを書くパスを通して、体の奥の面（腕の裏など）が透けて重なって見えないようにしてある。
// _Dissolve を上げると、ノイズの低い所から欠けていく（魂が消える・体へ戻るときの演出用）。
// 値は素材のインスペクタで変えればその場で反映される。
Shader "TpsDungeon/Soul"
{
    Properties
    {
        [HDR] _RimColor("縁の光の色", Color) = (0.45, 1.0, 1.45, 1)
        _RimPower("縁の光の鋭さ（大きいほど縁だけ光る）", Range(0.5, 8)) = 2.5
        [HDR] _BodyColor("中の色（a が濃さ）", Color) = (0.15, 0.42, 0.6, 0.14)
        _NoiseScale("ゆらぎの細かさ（1 m あたり）", Float) = 2.2
        _FlowSpeed("ゆらぎが上へ流れる速さ (m/秒)", Float) = 0.35
        _NoiseStrength("ゆらぎの強さ", Range(0, 1)) = 0.55
        _PulseSpeed("明滅の速さ（1 秒あたり）", Float) = 0.6
        _PulseStrength("明滅の強さ", Range(0, 1)) = 0.25
        _Dissolve("欠け具合（0 で欠けない、1 で消える）", Range(0, 1)) = 0
        [HDR] _DissolveEdgeColor("欠ける縁の色", Color) = (1.2, 1.8, 2.2, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _RimColor;
            float _RimPower;
            half4 _BodyColor;
            float _NoiseScale;
            float _FlowSpeed;
            float _NoiseStrength;
            float _PulseSpeed;
            float _PulseStrength;
            float _Dissolve;
            half4 _DissolveEdgeColor;
        CBUFFER_END

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // 0〜1 の滑らかな 3D の値ノイズ。
        float ValueNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                             lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                             lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        // 上へ流れる 2 オクターブのノイズ。
        float FlowNoise(float3 positionWS)
        {
            float3 p = positionWS * _NoiseScale - float3(0, _Time.y * _FlowSpeed * _NoiseScale, 0);
            return ValueNoise(p) * 0.65 + ValueNoise(p * 2.3 + 7.1) * 0.35;
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        ENDHLSL

        // 先に深度だけを書く。加算の本番のパスで、手前の面だけが光るように。
        Pass
        {
            Name "SoulDepth"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half4 FragDepth(Varyings input) : SV_Target
            {
                clip(FlowNoise(input.positionWS) - _Dissolve * 1.02);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Soul"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float noise = FlowNoise(input.positionWS);
                float cut = noise - _Dissolve * 1.02;
                clip(cut);

                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(abs(dot(normal, view))), _RimPower);

                float flicker = lerp(1.0, noise * 1.6, _NoiseStrength);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.2831853) * _PulseStrength;

                half3 color = (_RimColor.rgb * rim + _BodyColor.rgb * _BodyColor.a) * flicker * pulse;
                half alpha = saturate(rim + _BodyColor.a);

                // 欠けていく縁を光らせる。
                float edge = 1.0 - smoothstep(0.0, 0.08, cut);
                color += _DissolveEdgeColor.rgb * edge * step(0.001, _Dissolve);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
