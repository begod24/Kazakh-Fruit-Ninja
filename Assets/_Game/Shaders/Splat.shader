// Stains on the backdrop, drawn by one particle system. The atlas holds 8 shapes as distance fields
// (R: 0.5 on the edge, 1 at the deepest point; G: wet highlight; B: pigment variation). Each particle
// picks its shape through its colour's alpha (variant * 32 + 16) and dries up with its age: the edge
// creeps inward, so small drops vanish first and big ones shrink, while the highlight fades.
// Needs the custom vertex streams Position, Color, UV, AgePercent (UV.xy + age in TEXCOORD0.z).
Shader "Kazakh Ninja/Splat"
{
    Properties
    {
        _MainTex ("Splat atlas", 2D) = "black" {}
        _Grid ("Atlas grid (columns, rows)", Vector) = (4, 2, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Gloss ("Wet highlight", Range(0, 2)) = 0.55
        _EdgeDarken ("Darker rim", Range(0, 1)) = 0.5
        _Softness ("Edge softness", Range(0.005, 0.2)) = 0.035
        _DryStart ("Starts drying at (age)", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Splat"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Grid;
                half _Opacity;
                half _Gloss;
                half _EdgeDarken;
                half _Softness;
                half _DryStart;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float3 uvAge : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                float2 uv : TEXCOORD0;
                half2 wetFade : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color.rgb;

                // Shape index from the alpha byte; row 0 is the top row of the atlas.
                float cell = floor(input.color.a * 255.0 / 32.0);
                float column = fmod(cell, _Grid.x);
                float row = floor(cell / _Grid.x);
                output.uv = (input.uvAge.xy + float2(column, _Grid.y - 1.0 - row)) / _Grid.xy;

                float age = input.uvAge.z;
                half wet = 1.0h - smoothstep(_DryStart, 1.0h, age);
                half fade = saturate((1.0h - age) * 8.0h); // no pop when the particle dies
                output.wetFade = half2(wet, fade);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 field = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                half wet = input.wetFade.x;

                half edge = lerp(0.97h, 0.5h, wet);
                half shape = smoothstep(edge - _Softness, edge + _Softness, field.r);
                half depth = saturate((field.r - edge) * 7.0h);

                // Pigment pools at the rim: darker and richer there, lighter inside.
                half3 color = input.color;
                half3 rim = color * color * 0.9h;
                color = lerp(rim, color, lerp(1.0h - _EdgeDarken, 1.0h, depth));
                color *= 0.8h + 0.4h * field.b;
                color += field.g * (_Gloss * wet);

                half alpha = shape * _Opacity * lerp(0.6h, 1.0h, wet) * input.wetFade.y;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
