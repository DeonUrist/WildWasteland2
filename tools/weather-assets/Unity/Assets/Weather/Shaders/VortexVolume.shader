// Bounded baked-density volume. No live fluid simulation or particle updates.
Shader "WildWasteland/Weather/VortexVolume"
{
    Properties
    {
        _MainTex ("Animated flow atlas", 2D) = "gray" {}
        _DensityTex ("Baked density", 3D) = "white" {}
        _Tint ("Shared sand tint", Color) = (.55,.43,.33,.88)
        _Life ("Envelope", Range(0,1)) = 1
        _FrameBlend ("Interpolate atlas", Float) = 1
        _Steps ("Bounded samples", Float) = 12
        _BackLightA ("Distant scatter", Vector) = (0,0,0,0)
        _BackLightB ("Distant scatter", Vector) = (0,0,0,0)
        _RegionCenter ("Region origin", Vector) = (0,0,0,0)
        _RegionAxisX ("Across", Vector) = (1,0,0,0)
        _RegionAxisZ ("Depth", Vector) = (0,0,1,0)
        _RegionSize ("Half extents", Vector) = (6000,1500,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile __ WW_DIAGNOSTICS_ON
            #include "UnityCG.cginc"
            #include "WeatherDiagnostics.cginc"
            sampler2D _MainTex;
            sampler3D _DensityTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Tint, _BackLightA, _BackLightB, _WWWeatherAir, _WWWeatherSun;
            float4 _RegionCenter, _RegionAxisX, _RegionAxisZ, _RegionSize;
            float3 _WWEye;
            float _WWWeatherClock, _WWWeatherInside, _Life, _FrameBlend, _Steps;
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; float4 screen:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz;
                o.screen=ComputeScreenPos(o.pos); return o;
            }
            float3 flow(float2 uv,float frame)
            {
                float2 cell=float2(fmod(frame,4),3-floor(frame/4));
                return tex2D(_MainTex,(cell+clamp(frac(uv),.004,.996))/4).rgb;
            }
            half4 frag(v2f i):SV_Target
            {
                float3 relativeEye = _WWEye-_RegionCenter.xyz;
                float2 region = float2(dot(relativeEye,_RegionAxisX.xyz),dot(relativeEye,_RegionAxisZ.xyz))/_RegionSize.xy;
                region.y -= .055*sin(region.x*3.7+.4)+.025*sin(region.x*10.3+.9);
                float2 edge = abs(float2(region.x/max(.1,.875-.125*region.y),region.y));
                float2 e2 = edge*edge;
                float reveal = smoothstep(.025,.22,1-pow(dot(e2*e2*e2,float2(1,1)),1.0/6));
                if (reveal*_Life<.001) return 0;
                float3 eye=mul(unity_WorldToObject,float4(_WWEye,1)).xyz;
                float3 ray=normalize(i.local-eye);
                float3 inv=1/(ray+float3(.000001,.000001,.000001));
                float3 ta=(float3(-1,0,-1)-eye)*inv, tb=(float3(1,1,1)-eye)*inv;
                float3 mn=min(ta,tb), mx=max(ta,tb);
                float near=max(0,max(mn.x,max(mn.y,mn.z))), far=min(mx.x,min(mx.y,mx.z));
                float3 worldStep=mul((float3x3)unity_ObjectToWorld,ray);
                float metres=length(worldStep); float3 direction=worldStep/metres;
                float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float viewZ=max(.001,-mul((float3x3)UNITY_MATRIX_V,direction).z);
                far=min(far,depth/viewZ/metres);
                if (far<=near || _Life<.001) return 0;
                float time=_WWWeatherClock;
                // Integrate the two curved shell intervals rather than sampling empty box/eye space.
                float a=dot(ray.xz,ray.xz), b=dot(eye.xz,ray.xz);
                if (a>.00001)
                {
                    float d=b*b-a*(dot(eye.xz,eye.xz)-.9*.9);
                    if (d<0) return 0;
                    float root=sqrt(d); near=max(near,(-b-root)/a); far=min(far,(-b+root)/a);
                }
                if (far<=near) return 0;
                float firstEnd=far, secondStart=far;
                if (a>.00001)
                {
                    float d=b*b-a*(dot(eye.xz,eye.xz)-.18*.18);
                    if (d>0)
                    {
                        float root=sqrt(d);
                        firstEnd=clamp((-b-root)/a,near,far);
                        secondStart=clamp((-b+root)/a,near,far);
                    }
                }
                float firstLength=firstEnd-near;
                float path=firstLength+far-secondStart;
                if (path<.00001) return 0;
                float step=path/_Steps;
                #if defined(WW_DIAGNOSTICS_ON)
                if (_WWDiagOutput==3) return half4(WWDiagDistance(near*metres),WWDiagDistance(far*metres),WWDiagDistance(path*metres),1);
                float diagnosticDensity=0;
                #endif
                float transmission=1; float3 accumulated=0;
                for (int sampleIndex=0;sampleIndex<24;sampleIndex++)
                {
                    if (sampleIndex>=_Steps) break;
                    float along=(sampleIndex+.5)*step;
                    float distance=along<firstLength ? near+along : secondStart+along-firstLength;
                    float3 p=eye+ray*distance; float h=saturate(p.y);
                    float radius=.5-.08*sin(h*3.14159265)+.22*pow(h,6);
                    float2 sway=float2(sin(h*3+time*.23),cos(h*4+time*.19))*h*.055;
                    float2 q=p.xz-sway; float radial=length(q);
                    // Cartesian noise wraps continuously around 360 degrees. Height/time shear lifts the rotating smoke.
                    float angle=-time*.55+h*7, s=sin(angle), c=cos(angle);
                    float2 rotated=float2(c*q.x-s*q.y,s*q.x+c*q.y);
                    float3 uv=float3(rotated.x*1.7,h*2.8-time*.18,rotated.y*1.7);
                    float n;
                    #if defined(WW_DIAGNOSTICS_ON)
                    if (_WWDiagShape>.5) n=.5; else
                    #endif
                    {
                        // Filter the density over a march step, including the height-dependent twist.
                        // Fixed mip 1 resolves details much smaller than the 12/18/24 samples can integrate.
                        float2 swaySlope=(float2(sin(h*3+time*.23),cos(h*4+time*.19))+
                            h*float2(3*cos(h*3+time*.23),-4*sin(h*4+time*.19)))*.055;
                        float2 dq=ray.xz-swaySlope*ray.y;
                        float2 dr=float2(c*dq.x-s*dq.y,s*dq.x+c*dq.y)+float2(-rotated.y,rotated.x)*(7*ray.y);
                        float3 footprint=float3(dr.x*1.7,ray.y*2.8,dr.y*1.7)*step;
                        float lod=clamp(log2(max(1,length(footprint)*128)),1,7);
                        float coarse=tex3Dlod(_DensityTex,float4(uv,lod)).r;
                        float fine=tex3Dlod(_DensityTex,float4(uv*2.17+float3(.13,time*.02,.37),min(7,lod+1.1187))).r;
                        n=smoothstep(.23,.74,coarse*.72+fine*.28);
                    }
                    float shell=(radial-radius*.8)/max(.01,radius*.22);
                    float mass=exp(-shell*shell*1.2)*lerp(.12,1.4,n);
                    mass*=smoothstep(radius*.45,radius*.6,radial)*(1-smoothstep(radius,radius*1.18,radial));
                    mass*=smoothstep(0,.035,h)*(1-smoothstep(.82,1,h));
                    float opacity=1-exp(-mass*step*metres*.05*_Life*reveal);
                    float3 world=mul(unity_ObjectToWorld,float4(p,1)).xyz;
                    float3 delta=world-_BackLightA.xyz;
                    float glow=pow(saturate(1-dot(delta,delta)/1400000),2)*_BackLightA.w;
                    float sunlight=saturate(dot(normalize(float3(q.x,.2,q.y)),_WWWeatherSun.xyz));
                    float shade=(.42+n*.72+sunlight*.18)*lerp(1,.8,smoothstep(.55,1,h));
                    float3 color=_Tint.rgb*(shade+glow*.18);
                    float haze=1-exp(-distance*metres*.0012);
                    color=lerp(color,_WWWeatherAir.rgb,haze*.25);
                    #if defined(WW_DIAGNOSTICS_ON)
                    if (_WWDiagShape>.5) color=_Tint.rgb;
                    if (_WWDiagOutput==1) color=WWDiagColor(_WWDiagOwner);
                    diagnosticDensity+=transmission*n*opacity;
                    #endif
                    accumulated+=transmission*color*opacity;
                    transmission*=1-opacity;
                }
                #if defined(WW_DIAGNOSTICS_ON)
                if (_WWDiagOutput==2) return half4((1-transmission).xxx,1);
                if (_WWDiagOutput==6) return half4(diagnosticDensity.xxx,1);
                #endif
                return half4(accumulated,1-transmission);
            }
            ENDCG
        }
    }
    Fallback Off
}
