// Original WW weather prototype. Built-in renderer, Unity 2020.3.
// Animation runs in the shader; the runtime owner supplies shared storm parameters.
Shader "WildWasteland/Weather/DustShell"
{
    Properties
    {
        _MainTex ("RGBA dust / flipbook", 2D) = "white" {}
        _NoiseTex ("Repeating noise", 2D) = "gray" {}
        _Tint ("Dust tint and opacity", Color) = (0.55,0.36,0.17,0.8)
        _Sheet ("Columns, rows, frames, fps (0 = static)", Vector) = (1,1,1,0)
        _Flow ("UV velocity and noise scale", Vector) = (0.035,-0.015,2,3)
        _TimeOffset ("Animation offset", Float) = 0
        _FrameOffset ("Atlas frame / variation offset", Float) = 0
        _SideFade ("UV side fade (0 for closed funnels)", Range(0,0.5)) = 0
        _VerticalFade ("Top and bottom fade", Range(0,1)) = 1
        _NoiseStrength ("Noise modulation", Range(0,1)) = 1
        _FlashPosition ("Flash world position and radius", Vector) = (0,0,0,1)
        _FlashColor ("Cloud flash color and gain", Color) = (0,0,0,0)
        _SoftDistance ("Depth fade distance", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local _SOFT_DEPTH
            #include "UnityCG.cginc"
            sampler2D _MainTex, _NoiseTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Tint, _Sheet, _Flow, _FlashPosition, _FlashColor;
            float _TimeOffset, _SoftDistance, _FrameOffset, _SideFade, _VerticalFade, _NoiseStrength;
            float _WWWeatherClock;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float4 screen : TEXCOORD2;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                return o;
            }
            float2 atlasUV(float2 uv, float frame)
            {
                float2 grid = max(_Sheet.xy, 1);
                float2 cell = float2(fmod(frame, grid.x), floor(frame / grid.x));
                // Atlas authoring order: frame zero is the top-left cell.
                return (float2(cell.x, grid.y - 1 - cell.y) + uv) / grid;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float t = _WWWeatherClock + _TimeOffset;
                float clock = t * _Sheet.w + _FrameOffset;
                float count = max(1, _Sheet.z);
                float frame = fmod(floor(clock), count);
                float4 a = tex2D(_MainTex, atlasUV(i.uv, frame));
                float4 b = tex2D(_MainTex, atlasUV(i.uv, fmod(frame + 1, count)));
                float4 dust = lerp(a, b, frac(clock));
                float n = tex2D(_NoiseTex, i.uv * _Flow.zw + t * _Flow.xy).r;
                float m = tex2D(_NoiseTex, i.uv * _Flow.zw * 1.73 - t * _Flow.xy * 0.61).r;
                float edge = smoothstep(0, 0.08, i.uv.y) * (1 - smoothstep(0.82, 1, i.uv.y));
                edge = lerp(1, edge, _VerticalFade);
                if (_SideFade > 0.001)
                    edge *= smoothstep(0, _SideFade, i.uv.x) * (1 - smoothstep(1 - _SideFade, 1, i.uv.x));
                float alpha = saturate(dust.a * _Tint.a * edge * lerp(1, 0.35 + n * m * 1.6, _NoiseStrength));
                #if defined(_SOFT_DEPTH)
                    float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                    alpha *= saturate((scene - i.screen.z) / max(0.01, _SoftDistance));
                #endif
                float3 delta = i.world - _FlashPosition.xyz;
                float flash = saturate(1 - dot(delta, delta) / max(1, _FlashPosition.w * _FlashPosition.w));
                float3 color = dust.rgb * _Tint.rgb * (0.65 + n * 0.6);
                color += _FlashColor.rgb * (_FlashColor.a * flash * flash);
                return float4(color * alpha, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
