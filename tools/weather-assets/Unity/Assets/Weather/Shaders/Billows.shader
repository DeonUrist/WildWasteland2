// Original WW renderer: fixed mesh centers, camera-facing corners and motion evaluated on the GPU.
Shader "WildWasteland/Weather/Billows"
{
    Properties
    {
        _MainTex ("Animated dust atlas", 2D) = "white" {}
        _NoiseTex ("Repeating noise", 2D) = "gray" {}
        _Tint ("Dust color", Color) = (0.55,0.43,0.33,0.85)
        _Life ("Storm lifetime envelope", Range(0,1)) = 1
        _NearDust ("Interior wisps", Float) = 0
        _Vortex ("Rising vortex billows", Float) = 0
        _Hurricane ("Ground bank and upper canopy", Float) = 0
        _Canopy ("Dark cloud bank", Float) = 0
        _FrameBlend ("Interpolate frames", Float) = 1
        _RegionCenter ("Region origin", Vector) = (0,0,0,0)
        _RegionAxisX ("Across", Vector) = (1,0,0,0)
        _RegionAxisZ ("Depth", Vector) = (0,0,1,0)
        _RegionSize ("Half extents", Vector) = (6000,1500,0,0)
        _BackLightA ("Distant scatter A", Vector) = (0,0,0,0)
        _BackLightB ("Distant scatter B", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _NoiseTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Tint, _WWWeatherAir, _WWWeatherSun, _WWWeatherWind;
            float4 _BackLightA, _BackLightB;
            float4 _RegionCenter, _RegionAxisX, _RegionAxisZ, _RegionSize;
            float3 _WWEye;
            float _Life, _NearDust, _Vortex, _Hurricane, _Canopy, _FrameBlend, _WWWeatherClock, _WWWeatherInside;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 corner : TEXCOORD1;
                float4 cell : TEXCOORD2; // seed, width metres, height metres, relative height
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 corner : TEXCOORD1;
                float4 screen : TEXCOORD2;
                float4 shade : TEXCOORD3;
                float3 world : TEXCOORD4;
                float fade : TEXCOORD5;
            };
            v2f vert(appdata v)
            {
                v2f o;
                float t = _WWWeatherClock, phase = v.cell.x * 6.2831853;
                if (_Vortex < .5 && _NearDust < .5) phase += unity_ObjectToWorld._m03*.0007+unity_ObjectToWorld._m23*.0011;
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float3 forward = UNITY_MATRIX_I_V._m02_m12_m22;
                float3 center = mul(unity_ObjectToWorld, v.vertex).xyz;
                float puffFade = 1, puffScale = 1, stretch = 1;
                if (_NearDust > 0.5)
                {
                    float3 box = float3(32,10,32);
                    float gustTime = t+sin(t*.6)*.7;
                    center = _WWEye + frac((v.vertex.xyz * box + _WWWeatherWind.xyz * gustTime - _WWEye) / box) * box - box * 0.5;
                    puffFade *= 1-smoothstep(10,16,distance(center,_WWEye));
                    if (v.cell.w<-.5)
                    {
                        float3 wind = normalize(_WWWeatherWind.xyz+float3(0,.5,0));
                        float3 side = cross(wind,forward);
                        if (dot(side,side)>.01) { right=normalize(side); up=normalize(cross(forward,right)); }
                        else
                        {
                            float3 radial = center-_WWEye;
                            side = cross(radial,forward);
                            if (dot(side,side)>.01) { right=normalize(side); up=normalize(cross(forward,right)); }
                            stretch = v.cell.w< -2.5 ? 1 : .35;
                        }
                    }
                }
                else if (_Vortex > 0.5)
                {
                    float y = frac(v.vertex.y + t * 0.065);
                    float angle = atan2(v.vertex.z,v.vertex.x) + t * (1.4-y*0.45) + y*2;
                    float radius = (0.26 + y*0.1 + pow(y,5)*0.23) * (0.85 + v.cell.x*0.2);
                    float2 sway = float2(sin(t*0.17+y*2),cos(t*0.13+y*2))*0.025*y;
                    float2 radial = float2(cos(angle),sin(angle))*radius+sway;
                    center = mul(unity_ObjectToWorld,float4(radial.x,y,radial.y,1)).xyz;
                    puffFade = smoothstep(0,0.025,y)*(1-smoothstep(0.96,1,y));
                    puffScale = 1.15 + y * 0.65;
                }
                else
                {
                    if (_Hurricane > 0.5)
                    {
                        float y = v.vertex.y > 0.45 ? 0.8+(v.vertex.y-0.45)*0.4 : v.vertex.y*0.26;
                        center = mul(unity_ObjectToWorld,float4(v.vertex.x,y,v.vertex.z,1)).xyz;
                    }
                    center.y += sin(t * 0.13 + phase) * 10;
                    center.x += sin(t * 0.09 + phase) * 9;
                    center.z += cos(t * 0.11 + phase) * 9;
                    if (_Canopy<.5)
                    {
                        float across = dot(center-_RegionCenter.xyz,_RegionAxisX.xyz)/max(1,_RegionSize.x);
                        float wave = .055*sin(across*3.7+.4)+.025*sin(across*10.3+.9);
                        float rounded = 1-pow(saturate(1-pow(abs(across),6)),1.0/6);
                        center += _RegionAxisZ.xyz*(wave+rounded)*_RegionSize.y;
                        if (v.cell.w>-.5) center.y += (sin(across*15+phase*.05)*.09+sin(across*37)*.035)*v.cell.w*480;
                        else { center += _RegionAxisZ.xyz*sin(t*.35+phase)*35; center.y += sin(t*.55+phase)*8; }
                        puffFade *= 1-smoothstep(.92,1.02,abs(across));
                    }
                }
                float scale = _NearDust > 0.5 ? 1 : 1 + sin(t * 0.08 + phase) * 0.08;
                float verticalScale = (_Hurricane > 0.5 && _Vortex < 0.5 ? 0.6 : 1)*stretch;
                float3 world = center + right * v.corner.x * v.cell.y * scale * puffScale + up * v.corner.y * v.cell.z * scale * puffScale * verticalScale;
                o.pos = mul(UNITY_MATRIX_VP, float4(world,1));
                o.screen = ComputeScreenPos(o.pos);
                o.screen.z = -mul(UNITY_MATRIX_V, float4(world,1)).z;
                o.uv = v.uv; o.corner = v.corner + 0.5; o.world = world;
                o.fade = puffFade;
                float3 normal = normalize(right * v.corner.x + up * v.corner.y + forward * 0.65);
                float sunlight = 0.48 + 0.42 * saturate(dot(normal, _WWWeatherSun.xyz));
                o.shade = float4(sunlight, _Vortex > 0.5 ? frac(v.vertex.y+t*0.065) : v.cell.w, v.cell.x, distance(center, _WWEye));
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float t = _WWWeatherClock;
                float n = tex2D(_NoiseTex, i.world.xz * 0.009 + float2(-t * 0.025, t * 0.012)).r;
                float wobble = sin(t*0.16+i.shade.z*31+i.corner.y*5)*0.018;
                float4 stamp = 1;
                if (_NearDust<.5 || i.shade.y>-.5)
                {
                    float frame = frac(t/20+i.shade.z)*32;
                    float2 local = clamp(i.corner+float2(wobble,n*.025-.0125),.012,.988);
                    float2 cell = float2(fmod(floor(frame),4),7-floor(floor(frame)/4));
                    stamp = tex2D(_MainTex,(cell+local)/float2(4,8));
                    if (_FrameBlend>.5)
                    {
                        float next = fmod(floor(frame)+1,32);
                        float2 other = float2(fmod(next,4),7-floor(next/4));
                        stamp = lerp(stamp,tex2D(_MainTex,(other+local)/float2(4,8)),frac(frame));
                    }
                }
                float alpha = (1-exp(-stamp.a * 1.65)) * _Tint.a * _Life * i.fade * (0.82 + n * 0.18);
                if (_NearDust>.5 && i.shade.y<-.5)
                {
                    float2 q = (i.corner-.5)*2;
                    alpha = pow(saturate(1-dot(q,q)),2)*_Life*i.fade;
                }
                float2 border = min(i.corner,1-i.corner);
                alpha *= smoothstep(0,0.12,border.x) * smoothstep(0,0.12,border.y);
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                alpha *= saturate((depth - i.screen.z) / (_NearDust > 0.5 ? 1 : 16));
                alpha *= smoothstep(_NearDust > 0.5 ? 1.5 : 8, _NearDust > 0.5 ? 5 : 45, i.shade.w);
                float darkCanopy = lerp(1,.85,smoothstep(.35,1,i.shade.y));
                float detail = .45+1.15*dot(stamp.rgb,float3(.2126,.7152,.0722));
                float3 color = _Tint.rgb * i.shade.x * detail * darkCanopy;
                if (_NearDust<.5) color *= lerp(float3(.78,.72,.68),float3(1.2,1.14,1.06),i.shade.z);
                if (_NearDust<.5 && i.shade.y< -1.5) color = _Tint.rgb*(.7+stamp.r*.45);
                if (_Canopy>.5) color *= .65;
                if (_Vortex > .5) color *= lerp(.72,1.35,smoothstep(.25,.8,i.corner.x));
                float lit = 0;
                if (_BackLightA.w > 0)
                {
                    float3 da = i.world-_BackLightA.xyz, db = i.world-_BackLightB.xyz;
                    lit = pow(saturate(1-dot(da,da)/490000),2)*_BackLightA.w + pow(saturate(1-dot(db,db)/490000),2)*_BackLightB.w;
                }
                color += float3(0.23,0.19,0.14)*min(lit,1);
                // Dust has no HDR emission: repeated alpha-over converges to this bounded brown.
                color = min(color,_Tint.rgb*1.4);
                float haze = 1 - exp(-i.shade.w * _WWWeatherInside * (_Vortex > .5 ? .0016 : .026));
                color = lerp(color, _WWWeatherAir.rgb*(_Canopy>.5 ? .65 : 1), haze);
                if (_NearDust > 0.5)
                {
                    float gust = .65+.35*sin(t*.6+i.shade.z*2);
                    alpha *= _WWWeatherInside * (i.shade.y< -2.5 ? .6 : i.shade.y<-.5 ? .85 : .65)*gust;
                    float variation = i.shade.y< -2.5 ? lerp(.45,1.65,i.shade.z) : i.shade.y<-.5 ? lerp(.4,1.8,i.shade.z) : lerp(.5,1.5,i.shade.z);
                    color = _WWWeatherAir.rgb*variation;
                }
                return half4(color * alpha, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
