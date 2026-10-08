// The swipe trail (TrailRenderer, Stretch UVs: u = 0 at the blade tip, 1 at the tail; v across).
// A soft coloured body from the trail gradient, a white-hot core for bloom, and per-blade patterns:
// a glint gliding down steel, wind streaks, a chain of ornament diamonds, a jumping lightning bolt,
// flowing flame. One style keyword per material, at most one texture fetch.
Shader "Kazakh Ninja/Blade Trail"
{
    Properties
    {
        [KeywordEnum(Plain, Steel, Wind, Ornament, Lightning, Fire)] _Style ("Style", Float) = 0
        _NoiseTex ("Noise (tileable)", 2D) = "gray" {}
        [HDR] _CoreColor ("Core", Color) = (2.5, 2.4, 2.2, 1)
        _CoreWidth ("Core width", Range(0.02, 1)) = 0.22
        _GlowIntensity ("Body brightness", Range(0, 6)) = 1.4
        [HDR] _AccentColor ("Accent", Color) = (1, 0.8, 0.3, 1)
        _PatternScale ("Pattern repeats along the trail", Float) = 6
        _ScrollSpeed ("Pattern speed", Float) = 2
        _Additive ("Additive (0 = alpha blend, 1 = add)", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BladeTrail"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _STYLE_PLAIN _STYLE_STEEL _STYLE_WIND _STYLE_ORNAMENT _STYLE_LIGHTNING _STYLE_FIRE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _CoreColor;
                half4 _AccentColor;
                half _CoreWidth;
                half _GlowIntensity;
                float _PatternScale;
                float _ScrollSpeed;
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
                float u = input.uv.x;
                half across = abs(input.uv.y * 2.0 - 1.0); // 0 in the middle, 1 at the edges
                half4 tint = input.color;                   // the trail gradient; alpha fades to the tail

                half body = 1.0h - smoothstep(0.35h, 1.0h, across);
                half core = 1.0h - smoothstep(0.0h, _CoreWidth, across);
                half3 color = tint.rgb * _GlowIntensity;

            #if defined(_STYLE_STEEL)
                // A glint slides from the tip down the blade, like light on polished steel.
                half glint = saturate(1.0h - abs(frac(u * 0.8 - _Time.y * _ScrollSpeed) - 0.5h) * 6.0h);
                glint *= glint;
                core = saturate(core + glint * (1.0h - across));
                color = lerp(color, _AccentColor.rgb, glint * 0.5h);
            #elif defined(_STYLE_WIND)
                // Streaks of air torn along the stroke.
                half gust = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(u * _PatternScale - _Time.y * _ScrollSpeed, input.uv.y * 0.6)).r;
                half streaks = smoothstep(0.4h, 0.75h, gust);
                body *= 0.25h + 0.75h * streaks;
                color = lerp(color, _AccentColor.rgb * _GlowIntensity, streaks * 0.5h);
            #elif defined(_STYLE_ORNAMENT)
                // A chain of diamonds down the middle, like the border of a тұскиіз.
                float cellU = frac(u * _PatternScale - _Time.y * _ScrollSpeed) - 0.5;
                half diamond = 1.0h - smoothstep(0.3h, 0.4h, (half)abs(cellU) * 1.2h + across * 0.55h);
                color = lerp(color, _AccentColor.rgb, diamond);
                body = max(body, diamond);
            #elif defined(_STYLE_LIGHTNING)
                // The bolt jumps about the middle line and is redrawn twenty times a second.
                half jitter = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(u * 2.3 + floor(_Time.y * 20.0) * 0.173, 0.37)).r * 2.0h - 1.0h;
                across = abs((half)(input.uv.y * 2.0 - 1.0) - jitter * 0.6h);
                core = 1.0h - smoothstep(0.0h, _CoreWidth, across);
                body = (1.0h - smoothstep(0.1h, 0.9h, across)) * 0.75h;
                color = lerp(color, _AccentColor.rgb, 0.35h);
            #elif defined(_STYLE_FIRE)
                // Flames licking back along the stroke.
                float2 flow = float2(u * _PatternScale - _Time.y * _ScrollSpeed, input.uv.y * 0.8 - _Time.y * 0.9);
                half flame = saturate(SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flow).r * 1.7h - across * 1.2h + 0.15h);
                color = lerp(_AccentColor.rgb, tint.rgb, flame) * _GlowIntensity * (0.5h + flame);
                body *= saturate(flame * 1.8h);
            #endif

                half alpha = body * tint.a;
                half3 rgb = color * alpha + _CoreColor.rgb * (core * tint.a);
                return half4(rgb, alpha * (1.0h - _Additive));
            }
            ENDHLSL
        }
    }
}
