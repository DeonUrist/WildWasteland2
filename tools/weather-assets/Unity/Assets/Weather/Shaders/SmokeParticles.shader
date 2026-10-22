// Original soft smoke, independent of reference-mod shaders and cutout images.
Shader "WildWasteland/Weather/SmokeParticles"
{
    Properties
    {
        _MainTex ("Animated density atlas",2D) = "white" {}
        _NoiseTex ("Flow noise",2D) = "gray" {}
        _Tint ("Sand tint",Color) = (.55,.43,.33,.88)
        _Life ("Lifetime",Float) = 1
        _Critical ("Cloud ceiling",Float) = 0
        _LocalDust ("Native local simulation",Float) = 0
        _Spiral ("Spiral wall smoke",Float) = 0
        _Perimeter ("Boundary placement",Float) = 0
        _LeadingFront ("Forward rollers",Float) = 0
        _VortexCloud ("Orbiting wall clouds",Float) = 0
        _SmokeCull ("Surface side",Float) = 0
        _FrameBlend ("Frame interpolation",Float) = 1
        _RegionCenter ("Region center",Vector) = (0,0,0,0)
        _RegionAxisX ("Across",Vector) = (1,0,0,0)
        _RegionAxisZ ("Depth",Vector) = (0,0,1,0)
        _RegionSize ("Size",Vector) = (6000,1500,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha ZWrite Off Cull [_SmokeCull]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile __ WW_DIAGNOSTICS_ON
            #include "UnityCG.cginc"
            #include "WeatherDiagnostics.cginc"
            sampler2D _MainTex,_NoiseTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Tint,_WWWeatherAir,_RegionCenter,_RegionAxisX,_RegionAxisZ,_RegionSize;
            float3 _WWEye;
            float _Life,_Critical,_FrameBlend,_WWWeatherInside,_WWWeatherClock,_LocalDust,_Spiral,_Perimeter,_LeadingFront;
            float4 _SpiralShape,_CloudOrigin,_CloudTint;
            float _VortexCloud;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 spiral:TEXCOORD1; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 screen:TEXCOORD1; float4 data:TEXCOORD2; float3 world:TEXCOORD3; float3 flow:TEXCOORD4; };
            float3 spiralSurface(float h,float angle)
            {
                float radius=1450*(.44-.14*sin(h*3.14159265)+.76*smoothstep(.62,1,h));
                radius*=1+.035*sin(angle*3+h*7)+.02*cos(angle*5-h*4);
                return float3(cos(angle)*radius+sin(h*4)*90*h,h*1600,sin(angle)*radius+cos(h*3)*70*h);
            }
            v2f vert(appdata v)
            {
                v2f o; o.flow=0; float seed=v.color.r, time=_WWWeatherClock;
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                if (_VortexCloud>.5)
                {
                    // UV + UV2 fill TEXCOORD0; Center occupies TEXCOORD1.xyz in world space.
                    // Translate the billboard centre only so it keeps facing the camera.
                    float3 center=v.spiral.xyz, relative=center-_CloudOrigin.xyz;
                    float top=step(.5,v.color.b), spiral=saturate((relative.y-1050)/380);
                    float merge=saturate((1-v.color.b)*4)*v.color.g;
                    float angle=lerp(time*(.16+seed*.05),lerp(time*.085+sin(time*.09-spiral*2.3)*.28,time*.025,merge),top);
                    float s=sin(angle), c=cos(angle);
                    float2 next=float2(c*relative.x-s*relative.z,s*relative.x+c*relative.z);
                    world.xz+=next-relative.xz;
                    world.y+=sin(time*(.3+seed*.1)+seed*6.28)*lerp(12,35,top);
                }
                else if (_Spiral>.5)
                {
                    float h=saturate(v.spiral.y+(v.uv.y-.5)*_SpiralShape.x);
                    float angle=v.spiral.x+(v.uv.x-.5)*_SpiralShape.y;
                    world=mul(unity_ObjectToWorld,float4(spiralSurface(h,angle),1)).xyz;
                    float swirl=angle-h*5.5+time*.35+_SpiralShape.z;
                    o.flow=float3(cos(swirl),sin(swirl),h-time*.035);
                    v.color.a*=smoothstep(0,.025,h)*(1-smoothstep(.75,1,h));
                    v.color.g=0; v.color.b=0;
                }
                else if (_LeadingFront>.5 && v.color.g>.75)
                {
                    float cycle=frac(seed+time/(12+seed*6));
                    world-=_RegionAxisZ.xyz*(cycle-seed)*360;
                    world.y+=sin(cycle*6.2831853)*30;
                    v.color.a*=smoothstep(0,.1,cycle)*(1-smoothstep(.7,1,cycle));
                }
                else if (_LocalDust<.5)
                {
                    float cycle=frac(time/(11+seed*9)+seed);
                    float roll=cycle*6.2831853;
                    world += _RegionAxisZ.xyz*(sin(roll)*75+cos(roll)*30);
                    world.y += (cos(roll)*.5+.5)*100;
                    float x=dot(world-_RegionCenter.xyz,_RegionAxisX.xyz)/max(1,_RegionSize.x);
                    if (_Perimeter<.5) world += _RegionAxisZ.xyz*(.055*sin(x*3.7+.4)+.025*sin(x*10.3+.9))*_RegionSize.y;
                }
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1)); o.screen=ComputeScreenPos(o.pos);
                o.screen.z=-mul(UNITY_MATRIX_V,float4(world,1)).z;
                o.uv=v.uv; o.data=v.color; o.world=world; return o;
            }
            float4 density(float2 uv,float frame)
            {
                return tex2D(_MainTex,(float2(fmod(frame,4),7-floor(frame/4))+uv)/float2(4,8));
            }
            half4 frag(v2f i):SV_Target
            {
                #if defined(WW_DIAGNOSTICS_ON)
                if (_WWDiagSmokeLayer>.5 && _WWDiagSmokeLayer<1.5 && i.data.b>.5) return 0;
                if (_WWDiagSmokeLayer>1.5 && i.data.b<.5) return 0;
                #endif
                float t=_WWWeatherClock, seed=i.data.r;
                float n=tex2D(_NoiseTex,i.world.xz*.004+float2(-t*.01,t*.006)).r;
                float2 uv=i.uv+(n-.5)*.035;
                if (_Spiral>.5)
                {
                    float2 ring=normalize(i.flow.xy);
                    n=tex2D(_NoiseTex,float2(ring.x*1.7+i.flow.z*2.1,ring.y*1.7-i.flow.z*3.3)).r;
                    uv=.5+ring*.23+(n-.5)*.18;
                    seed=frac(_SpiralShape.z);
                }
                float frame=frac(t/24+seed)*32;
                float4 stamp=density(clamp(uv,.02,.98),floor(frame));
                if (_FrameBlend>.5) stamp=lerp(stamp,density(clamp(uv,.02,.98),fmod(floor(frame)+1,32)),frac(frame));
                #if defined(WW_DIAGNOSTICS_ON)
                if (_Spiral>.5 && _WWDiagShape>.5) stamp=float4(.5,.5,.5,1);
                #endif
                float2 q=(i.uv-.5)*2;
                float radial=dot(q,q);
                float feather=pow(saturate(1-radial),1.4);
                float alpha=(1-exp(-stamp.a*8))*feather*_Life;
                if (_Spiral>.5) alpha=lerp(.78,.97,saturate(stamp.a))*_Life*i.data.a;
                else if (_LeadingFront>.5 && i.data.g>.75) alpha=(1-exp(-stamp.a*12))*pow(saturate(1-radial),.7)*_Life*i.data.a;
                else if (_LocalDust>.5) alpha*=i.data.a*(.22+seed*.12);
                else if (i.data.b>.5) alpha*=.65+.35*_Critical;
                if (_VortexCloud>.5)
                {
                    float radius=length(i.world.xz-_CloudOrigin.xz);
                    float eye=max(18,260*_CloudOrigin.w);
                    alpha*=i.data.a*smoothstep(eye*.7,eye+65*_CloudOrigin.w,radius);
                }
                float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                alpha*=saturate((depth-i.screen.z)/(_LocalDust>.5 ? 2 : 65));
                alpha*=smoothstep(_LocalDust>.5 ? .5 : 15,_LocalDust>.5 ? 2.5 : 100,distance(i.world,_WWEye));
                float leading=i.data.g;
                float merge=_VortexCloud>.5 && i.data.b>.5 ? saturate((1-i.data.b)*4) : 0;
                float shade=.65+stamp.r*.55+leading*.25*(1-merge);
                if (_VortexCloud>.5 && i.data.b>.5)
                {
                    float2 q=i.world.xz-_CloudOrigin.xz;
                    float phase=atan2(q.y,q.x)-t*.085-length(q)/(max(.45,_CloudOrigin.w)*360);
                    float arm=smoothstep(-.6,.7,sin(phase*2+(n-.5)*.4));
                    shade*=lerp(lerp(.58,1.12,arm),1,merge*i.data.g);
                }
                float3 color=lerp(_Tint.rgb,_CloudTint.rgb,merge*i.data.g)*shade;
                if (_LocalDust<.5 && i.data.b>.5) color*=.72;
                float haze=1-exp(-distance(i.world,_WWEye)*.0015*_WWWeatherInside);
                color=lerp(color,_WWWeatherAir.rgb,haze*.45);
                #if defined(WW_DIAGNOSTICS_ON)
                if (_WWDiagOutput==1) color=WWDiagColor(_WWDiagOwner);
                if (_WWDiagOutput==2) return half4(alpha,alpha,alpha,1);
                if (_WWDiagOutput==3) return half4(WWDiagDistance(i.screen.z),WWDiagDistance(depth),WWDiagDistance(max(0,depth-i.screen.z)),1);
                if (_WWDiagOutput==6) color=stamp.aaa;
                #endif
                return half4(color*alpha,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
