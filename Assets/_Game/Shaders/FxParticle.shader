// Particles from the FX atlas. Premultiplied blending, so one shader covers both solid particles
// (juice, crumbs, smoke: _Additive 0) and glowing ones (sparks, glints, flashes: _Additive 1, with
// _Intensity above 1 so bloom picks them up). One texture fetch, no keywords.
Shader "Kazakh Ninja/FX Particle"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 8)) = 1
        _Additive ("Additive (0 = alpha blend, 1 = add)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FxParticle"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Intensity;
                half _Additive;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = texel.a * input.color.a;
                half3 rgb = texel.rgb * input.color.rgb * (_Intensity * alpha);
                return half4(rgb, alpha * (1.0h - _Additive));
            }
            ENDHLSL
        }
    }
}
