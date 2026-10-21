// 死後の場所の空。上・地平・下の 3 色を縦にぼかしただけの暗い空（Skybox の材質に使う）。
// 霧の色と地平の色を揃えると、遠くの物が空へ溶ける。
Shader "TpsDungeon/Afterlife Sky"
{
    Properties
    {
        _TopColor("上の色", Color) = (0.02, 0.04, 0.06, 1)
        _HorizonColor("地平の色", Color) = (0.05, 0.1, 0.13, 1)
        _BottomColor("下の色", Color) = (0.005, 0.01, 0.015, 1)
        _Sharpness("地平のぼけの狭さ", Range(0.5, 8)) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }

        Pass
        {
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                float _Sharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float y = normalize(input.direction).y;
                float up = pow(saturate(y), 1.0 / _Sharpness);
                float down = pow(saturate(-y), 1.0 / _Sharpness);
                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                color = lerp(color, _BottomColor.rgb, down);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
