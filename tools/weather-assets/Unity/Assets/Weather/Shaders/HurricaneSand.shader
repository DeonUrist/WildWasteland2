Shader "WildWasteland/Weather/HurricaneSand"
{
    Properties
    {
        _DensityTex ("Rounded multiscale billows",3D) = "gray" {}
        _Tint ("Hurricane tint",Color) = (.55,.43,.33,1)
        _Life ("Envelope",Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always ZClip Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ WW_DIAGNOSTICS_ON
            #include "UnityCG.cginc"
            #include "WeatherDiagnostics.cginc"
            sampler3D _DensityTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Tint,_SpiralProfile,_WWWeatherSun,_WWWeatherAir,_BackLightA;
            float3 _WWEye;
            float _Life,_WWWeatherClock,_WWWeatherInside,_Steps;
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; float4 screen:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz;
                o.screen=ComputeScreenPos(o.pos); return o;
            }
            float radiusAt(float h) { return 1450*(.44-.14*sin(saturate(h)*3.14159265)+.76*smoothstep(.62,1,h)); }
            float4 advect(float3 p,float layer,float mip)
            {
                float h=p.y/1600;
                float angle=-_WWWeatherClock*(.22+layer*.019)*_SpiralProfile.z+sin(h*3+_SpiralProfile.y)*.65+_SpiralProfile.y;
                float s=sin(angle), c=cos(angle);
                float2 q=float2(c*p.x-s*p.z,s*p.x+c*p.z);
                float height=(p.y-_WWWeatherClock*(30+layer*2.7))/max(.065,_SpiralProfile.x);
                return tex3Dlod(_DensityTex,float4(float3(q.x,height,q.y)/1900+layer*.231,mip));
            }
            float densityAt(float3 p,out float skip,float mip)
            {
                float h=p.y/1600, phase=_SpiralProfile.y;
                float2 bend=float2(sin(h*4+phase),cos(h*3+phase))*h*110;
                float2 q=p.xz-bend;
                float radial=length(q), angle=atan2(q.y,q.x);
                float radius=radiusAt(h)*(1+.055*sin(angle*3+h*5+phase)+.025*cos(angle*5-h*3));
                float eye=270+35*sin(h*2+phase);
                skip=max(max((radial-(radius+420))*.25,eye-130-radial),max(-p.y,p.y-2080));
                if (skip>40) return 0;
                float3 local=float3(q.x,p.y,q.y);
                float4 a=advect(local,0,mip), b=advect(local,1,mip), c=advect(local,2,mip);
                float billow=a.r*.46+b.g*.34+c.r*.20;
                float outer=radius+(billow-.48)*380+(c.b-.5)*150;
                float edge=1-smoothstep(outer-75,outer+115,radial);
                float interior=1-smoothstep(radius-150,radius+15,radial);
                float body=lerp(smoothstep(.14,.76,billow)*(.65+.5*c.b),.70+.30*billow,interior);
                float erosion=(1-interior)*(1-c.b)*.30;
                float bulk=(1-smoothstep(radius-45,radius+55,radial))*(.55+.4*billow);
                float eyeBoundary=min(eye+(billow-.5)*120+(c.b-.5)*80,radius-150);
                float wispMask=smoothstep(radius-30,radius+70,radial)*(1-smoothstep(radius+170,radius+400,radial));
                float wisps=pow(saturate((b.g*.4+c.b*.6-.28)*2),3)*.085*wispMask;
                float vertical=smoothstep(-15,50,p.y)*(1-smoothstep(950,1150,p.y));
                return (max(max(0,body-erosion)*edge,bulk)+wisps)*smoothstep(eyeBoundary-20,eyeBoundary+50,radial)*vertical;
            }
            half4 frag(v2f i):SV_Target
            {
                if (_Life<.001) return 0;
                float3 eye=mul(unity_WorldToObject,float4(_WWEye,1)).xyz;
                float3 ray=normalize(i.local-eye);
                float3 inv=1/(ray+float3(.000001,.000001,.000001));
                float3 ta=(float3(-2500,-30,-2500)-eye)*inv, tb=(float3(2500,2100,2500)-eye)*inv;
                float3 mn=min(ta,tb), mx=max(ta,tb);
                float near=max(0,max(mn.x,max(mn.y,mn.z))), far=min(mx.x,min(mx.y,mx.z));
                float3 worldRay=mul((float3x3)unity_ObjectToWorld,ray);
                float metres=length(worldRay); worldRay/=max(.0001,metres);
                float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float viewZ=max(.0001,-mul((float3x3)UNITY_MATRIX_V,worldRay).z);
                far=min(far,depth/viewZ/max(.0001,metres));
                if (far<=near) return 0;
                float3 sunWorld=normalize(_WWWeatherSun.xyz+float3(0,.00001,0));
                float3 sun=normalize(mul((float3x3)unity_WorldToObject,sunWorld));
                float forward=pow(saturate(dot(worldRay,sunWorld)),4);
                float baseStep=lerp(22,10,saturate((_Steps-64)/64));
                float3 textureRay=float3(ray.x,ray.y/max(.065,_SpiralProfile.x),ray.z);
                float mip=max(1,log2(max(1,length(textureRay)*baseStep*128/1900)));
                float distance=near, transmission=1, opticalDepth=0; float3 accumulated=0;
                for (int sampleIndex=0;sampleIndex<128;sampleIndex++)
                {
                    if (sampleIndex>=_Steps || distance>=far || transmission<.005) break;
                    float3 p=eye+ray*(distance+baseStep*.5);
                    float skip; float dust=densityAt(p,skip,mip);
                    float step=min(far-distance,max(baseStep,skip*.65));
                    if (dust>.001)
                    {
                        float lightDepth=0, backDepth=0, unused;
                        float3 backPosition=mul(unity_WorldToObject,float4(_BackLightA.xyz,1)).xyz;
                        float3 backRay=normalize(backPosition-p+float3(.00001,0,0));
                        for (int lightIndex=0;lightIndex<3;lightIndex++)
                        {
                            lightDepth+=densityAt(p+sun*(35+lightIndex*55),unused,mip)*2.7;
                            backDepth+=densityAt(p+backRay*min(length(backPosition-p),35+lightIndex*55),unused,mip)*2.7;
                        }
                        float direct=exp(-lightDepth*.4);
                        float3 world=mul(unity_ObjectToWorld,float4(p,1)).xyz;
                        float back=exp(-length(world-_BackLightA.xyz)/1100)*saturate(_BackLightA.w);
                        float illumination=saturate(.16+.24*exp(-lightDepth*.22)+direct*(.7+forward*.35)+back*exp(-backDepth*.5)*.95);
                        float3 color=lerp(_Tint.rgb*float3(.22,.17,.14),_Tint.rgb*float3(2.0,1.8,1.5),illumination);
                        float optical=dust*step*.085*_Life, opacity=1-exp(-optical);
                        accumulated+=transmission*opacity*color; transmission*=1-opacity; opticalDepth+=optical;
                    }
                    distance+=step;
                }
                float alpha=1-transmission;
                float haze=(1-exp(-near*metres*.0003*_WWWeatherInside))*.12;
                accumulated=lerp(accumulated,_WWWeatherAir.rgb*alpha,haze);
                #if defined(WW_DIAGNOSTICS_ON)
                if (_WWDiagOutput==1) accumulated=WWDiagColor(_WWDiagOwner)*alpha;
                if (_WWDiagOutput==2) return half4(alpha,alpha,alpha,1);
                if (_WWDiagOutput==3) return half4(WWDiagDistance(near*metres),WWDiagDistance(far*metres),saturate(opticalDepth/4.61),1);
                #endif
                return half4(accumulated,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}

