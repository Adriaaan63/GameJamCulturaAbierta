Shader "Custom/PauseBlurURP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 0.05)) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _BlurSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 s = float2(_BlurSize / aspect, _BlurSize);
                half3 sum = 0;

                // Blur 5x5 con UV clampeadas para evitar artefactos en los bordes
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 uv = saturate(i.uv + float2(x, y) * s * 0.5);
                        sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                    }
                }

                half3 col = sum / 25.0;

                // Multiplica por el Color del RawImage (para oscurecer)
                return half4(col * i.color.rgb, 1);
            }
            ENDHLSL
        }
    }
}