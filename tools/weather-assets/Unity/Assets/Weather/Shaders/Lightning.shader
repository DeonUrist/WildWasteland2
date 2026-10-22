Shader "WildWasteland/Weather/Lightning"
{
    Properties
    {
        _Life ("Storm envelope", Range(0,1)) = 1
        _Pulse ("Two discharge channels", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Pulse;
            float _Life, _WWWeatherClock, _WWWeatherInside;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 direction : TEXCOORD1; float2 channel : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float strength : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float bucket = floor(_WWWeatherClock*24);
                float3 local = v.vertex.xyz;
                local.x += sin(local.y*0.08+bucket*2.1+v.channel.x*17)*8;
                local.z += sin(local.y*0.11+bucket*1.7)*6;
                float3 world = mul(unity_ObjectToWorld,float4(local,1)).xyz;
                float3 axis = normalize(mul((float3x3)unity_ObjectToWorld,v.direction));
                float3 side = cross(axis,normalize(_WorldSpaceCameraPos-world));
                side /= max(0.001,length(side));
                world += side*v.uv.x*(v.channel.y > 0.5 ? 7 : 12);
                o.pos = mul(UNITY_MATRIX_VP,float4(world,1)); o.uv = v.uv;
                o.strength = (v.channel.x < 0.5 ? _Pulse.x : _Pulse.y)*_Life;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float core = exp(-i.uv.x*i.uv.x*160)*4;
                float halo = exp(-i.uv.x*i.uv.x*5)*0.22;
                float strength = i.strength*lerp(1,0.3,_WWWeatherInside);
                return half4(float3(1,0.88,0.7)*(core+halo)*strength,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
