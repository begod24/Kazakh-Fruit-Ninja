// The backdrop: one painted picture per scene, drifting with the parallax offset. While the day moves on to
// the next scene (_TRANSITION), the next picture dissolves in through a noise mask that opens from the top, like
// dusk falling. Each picture has its own window (_UvRectA/B), since the paintings differ in shape. Very bright
// details (the sun, windows, the Бәйтерек sphere) are pushed above 1 so bloom makes them glow.
// 1 texture fetch, 3 during a transition.
Shader "Kazakh Ninja/Parallax Background"
{
    Properties
    {
        _PictureA ("Picture", 2D) = "black" {}
        _PictureB ("Next picture", 2D) = "black" {}
        _NoiseTex ("Dissolve noise", 2D) = "gray" {}
        _Blend ("Transition", Range(0, 1)) = 0
        _Offset ("Parallax offset (xy)", Vector) = (0, 0, 0, 0)
        _UvRectA ("Picture window (scale xy, offset zw)", Vector) = (0.94, 0.8, 0.03, 0.1)
        _UvRectB ("Next picture window", Vector) = (0.94, 0.8, 0.03, 0.1)
        _GlowThreshold ("Glow threshold", Range(0, 1)) = 0.72
        _GlowBoost ("Glow boost", Range(0, 8)) = 3
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Opaque" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }
        ZWrite On
        Cull Back

        Pass
        {
            Name "ParallaxBackground"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _TRANSITION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Both pictures share one sampler (clamped, bilinear).
            TEXTURE2D(_PictureA);
            SAMPLER(sampler_PictureA);
            TEXTURE2D(_PictureB);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _PictureA_ST;
                float4 _Offset;
                float4 _UvRectA;
                float4 _UvRectB;
                half _Blend;
                half _GlowThreshold;
                half _GlowBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUv : TEXCOORD0;
                float4 pictureUv : TEXCOORD1; // current.xy, next.zw
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenUv = input.uv;
                output.pictureUv = float4(input.uv * _UvRectA.xy + _UvRectA.zw, input.uv * _UvRectB.xy + _UvRectB.zw) + _Offset.xyxy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_PictureA, sampler_PictureA, input.pictureUv.xy).rgb;

            #if defined(_TRANSITION)
                half3 next = SAMPLE_TEXTURE2D(_PictureB, sampler_PictureA, input.pictureUv.zw).rgb;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.screenUv * float2(2.2, 1.1)).r;
                half threshold = noise * 0.55h + (1.0h - (half)input.screenUv.y) * 0.45h;
                const half width = 0.1h;
                half reveal = smoothstep(threshold - width, threshold + width, _Blend * (1.0h + 2.0h * width) - width);
                color = lerp(color, next, reveal);
            #endif

                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                color *= 1.0h + saturate(luminance - _GlowThreshold) * _GlowBoost;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
