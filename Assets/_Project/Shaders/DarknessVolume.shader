// 箱の中にある物を暗くする。階段の先（下りの竪穴の底、上りの天井の穴の先）を見えにくくするのに使う。
// 箱（ローカルで ±0.5 の立方体。Unity の Cube を拡大して使う）の中に見えている面を、ローカルの上の面（y = +0.5）から
// 下へ測った距離（メートル）で暗くする。_FadeStart までは何もせず、_FadeEnd で _Color（a が最も暗い所の濃さ）になり、そこから先はそのまま。
// 向きを逆にしたいときは箱を回す。値は素材のインスペクタで変えればその場で反映される。
// 暗くする所はシーンの深度（_CameraDepthTexture）から見えている面の位置を戻して決めるので、URP の Depth Texture が要る。
// 裏の面を ZTest Always で描くので、カメラが箱の中にあっても効き、箱の外にある物は暗くしない。
Shader "TpsDungeon/Darkness Volume"
{
    Properties
    {
        _Color("色（a が最も暗い所の濃さ）", Color) = (0, 0, 0, 1)
        _FadeStart("暗くなり始める距離 (m)", Float) = 0
        _FadeEnd("最も暗くなる距離 (m)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DarknessVolume"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif

                // 見えている面の位置を箱のローカルへ。箱の外なら何もしない。
                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 p = TransformWorldToObject(positionWS);
                if (any(abs(p) > 0.5)) discard;

                // 上の面からの距離をメートルに直す（箱のローカルの y 軸の長さが箱の高さ）。
                float height = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                float distance = (0.5 - p.y) * height;
                float darkness = smoothstep(_FadeStart, max(_FadeEnd, _FadeStart + 0.001), distance);
                return half4(_Color.rgb, _Color.a * darkness);
            }
            ENDHLSL
        }
    }
}
