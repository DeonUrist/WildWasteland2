// Original WW bounded atmosphere. Integrates only the ray segment inside moving weather regions.
Shader "Hidden/WildWasteland/Weather/Atmosphere"
{
    Properties { _NoiseTex ("Dust noise", 2D) = "gray" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile __ WW_DIAGNOSTICS_ON
            #include "UnityCG.cginc"
            #include "WeatherDiagnostics.cginc"
            sampler2D _NoiseTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4x4 _WWInverseProjection, _WWViewToWorld;
            float4 _WWCenters[4], _WWSizes[4], _WWAxes[4], _WWVelocity[4];
            float4 _WWBackLightA[4], _WWBackLightB[4];
            float4 _WWWeatherAir, _WWWeatherSun;
            float4 _WWStormEye, _WWStormEyeShape;
            float3 _WWEye;
            float _WWWeatherClock, _WWWeatherInside, _WWWeatherApproach;
            int _WWCount, _WWSamples;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 depthUV : TEXCOORD1; };
            v2f vert(appdata_img v)
            {
                v2f o; o.pos = float4(v.vertex.xy,0,1); o.uv = v.texcoord; o.depthUV = v.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                    o.depthUV.y = 1-o.depthUV.y;
                #endif
                return o;
            }
            bool clipFace(float origin, float direction, float bound, inout float near, inout float far)
            {
                if (abs(direction)<0.0000001) return origin<=bound;
                float t = (bound-origin)/direction;
                if (direction>0) far=min(far,t); else near=max(near,t);
                return far>near;
            }
            float profile(float2 p)
            {
                p.y -= .055*sin(p.x*3.7+.4)+.025*sin(p.x*10.3+.9);
                float2 v = abs(float2(p.x/max(.1,.875-.125*p.y),p.y));
                float2 v2 = v*v;
                return 1-pow(dot(v2*v2*v2,float2(1,1)),1.0/6);
            }
            float hash(float3 p)
            {
                p=frac(p*.1031); p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float cloudNoise(float3 p)
            {
                float3 c=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(c),hash(c+float3(1,0,0)),f.x),lerp(hash(c+float3(0,1,0)),hash(c+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(c+float3(0,0,1)),hash(c+float3(1,0,1)),f.x),lerp(hash(c+float3(0,1,1)),hash(c+1),f.x),f.y),f.z);
            }
            float fogRegion(float3 ray, float limit, int index, out float height, out float scatter, out float2 travel)
            {
                float3 delta = _WWEye - _WWCenters[index].xyz;
                float4 axes = _WWAxes[index];
                float2 origin = float2(dot(delta.xz, axes.xy), dot(delta.xz, axes.zw)) / _WWSizes[index].xz;
                float2 direction = float2(dot(ray.xz, axes.xy), dot(ray.xz, axes.zw)) / _WWSizes[index].xz;
                float near = 0, far = limit;
                height = 0; scatter = 0; travel=0;
                if (!clipFace(origin.x,direction.x,1.05,near,far) || !clipFace(-origin.x,-direction.x,1.05,near,far)) return 0;
                if (!clipFace(origin.y,direction.y,1.12,near,far) || !clipFace(-origin.y,-direction.y,1.12,near,far)) return 0;
                float ceiling = _WWSizes[index].y*1.3;
                if (abs(ray.y) < 0.00001) { if (delta.y < -80 || delta.y > ceiling) return 0; }
                else
                {
                    float bottom = (-80-delta.y)/ray.y, top = (ceiling-delta.y)/ray.y;
                    near = max(near,min(bottom,top)); far = min(far,max(bottom,top));
                }
                if (far <= near || _WWCenters[index].w < 0.0001) return 0;
                travel=float2(near,far);
                float density = 0, step = (far-near)/_WWSamples;
                for (int sampleIndex = 0; sampleIndex < 8; sampleIndex++)
                {
                    if (sampleIndex >= _WWSamples) break;
                    float distance = near + (sampleIndex + 0.5) * step;
                    float3 p = delta + ray * distance;
                    float2 horizontal = float2(dot(p.xz, axes.xy), dot(p.xz, axes.zw)) / _WWSizes[index].xz;
                    float edge = smoothstep(0,0.16,profile(horizontal));
                    float rear = saturate((horizontal.y+1)*.5);
                    float crest=.91+.07*sin(horizontal.x*11+.4)+.04*sin(horizontal.x*27-1.2);
                    float localCeiling = ceiling*lerp(1,.35,rear)*crest;
                    float vertical = 1-smoothstep(0.55,1,p.y/localCeiling);
                    float mass = edge * vertical * lerp(1,.22,smoothstep(0,1,rear));
                    if (_WWStormEyeShape.y>.001)
                    {
                        float3 eyePoint=p+_WWCenters[index].xyz-_WWStormEye.xyz;
                        float clear=(1-smoothstep(_WWStormEye.w*.78,_WWStormEye.w,length(eyePoint.xz)));
                        clear*=1-smoothstep(_WWStormEyeShape.x*.75,_WWStormEyeShape.x,eyePoint.y);
                        mass*=1-clear*_WWStormEyeShape.y*.97;
                    }
                    density += mass;
                    if (_WWBackLightA[index].w > 0)
                    {
                        float3 world = p+_WWCenters[index].xyz;
                        float3 da = world-_WWBackLightA[index].xyz, db = world-_WWBackLightB[index].xyz;
                        scatter += mass * (pow(saturate(1-dot(da,da)/640000),2)*_WWBackLightA[index].w + pow(saturate(1-dot(db,db)/640000),2)*_WWBackLightB[index].w);
                    }
                    height += saturate(p.y/ceiling);
                }
                height /= _WWSamples;
                scatter /= max(0.0001,density);
                return density * step * _WWCenters[index].w * _WWVelocity[index].w;
            }
            half4 frag(v2f i) : SV_Target
            {
                float4 view = mul(_WWInverseProjection,float4(i.depthUV*2-1,1,1));
                float3 viewRay = normalize(view.xyz/view.w);
                float3 ray = normalize(mul((float3x3)_WWViewToWorld,viewRay));
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.depthUV));
                #if defined(WW_DIAGNOSTICS_ON)
                if (_WWDiagOutput==4) return half4(WWDiagDistance(depth).xxx,1);
                if (_WWDiagOutput==5) return half4(ray*.5+.5,1);
                if (_WWDiagNoAtmosphere>.5) return 0;
                #endif
                bool sky = depth>_ProjectionParams.z*.98;
                float limit = sky ? 16000 : min(16000,depth/max(0.001,abs(viewRay.z)));
                float total = 0, canopy = 0, lit = 0; float2 diagnosticTravel=0;
                for (int region = 0; region < 4; region++)
                {
                    if (region >= _WWCount) break;
                    float height, scatter; float2 travel;
                    float amount = fogRegion(ray,limit,region,height,scatter,travel);
                    if (amount>0) diagnosticTravel=travel;
                    canopy += amount*height; total += amount; lit += amount*scatter;
                }
                float averageHeight = canopy/max(0.0001,total);
                float transmission = exp(-min(total,12));
                float sunGlow = pow(saturate(dot(ray,_WWWeatherSun.xyz)),8)*0.12;
                float3 air = _WWWeatherAir.rgb * (1 + sunGlow) * lerp(1,.75,smoothstep(.3,.9,averageHeight));
                air *= 1 - smoothstep(.05,.5,ray.y)*.1;
                air *= 1+.35*saturate(lit/max(.0001,total));
                float3 gustP = _WWEye*.001+ray*4-_WWVelocity[0].xyz*_WWWeatherClock*.00035;
                float gustNoise = cloudNoise(gustP)+cloudNoise(gustP*2.3)*.35;
                air *= lerp(.73,1.23,saturate(gustNoise*.75));
                float alpha = 1-transmission;
                if (sky && _WWWeatherApproach>.001)
                {
                    float3 skyP=ray*3.7+_WWEye*.00012+float3(_WWWeatherClock*.009,0,-_WWWeatherClock*.005);
                    float broad=cloudNoise(skyP);
                    skyP+=float3(broad,broad*.4,-broad)*.7;
                    float cloud=broad*.55+cloudNoise(skyP*2.1)*.3+cloudNoise(skyP*4.3)*.15;
                    float3 canopyColor = _WWWeatherAir.rgb*lerp(.61,1.25,smoothstep(.15,.85,cloud));
                    float veil = max(_WWWeatherInside,_WWWeatherApproach*.9);
                    float cloudMix = smoothstep(.05,.65,ray.y);
                    air = lerp(air,canopyColor,cloudMix);
                    alpha = max(alpha,saturate(veil));
                }
                #if defined(WW_DIAGNOSTICS_ON)
                if (_WWDiagOutput==1) air=float3(.55,.15,.85);
                if (_WWDiagOutput==2) return half4(alpha.xxx,1);
                if (_WWDiagOutput==3) return half4(WWDiagDistance(diagnosticTravel.x),WWDiagDistance(diagnosticTravel.y),WWDiagDistance(max(0,diagnosticTravel.y-diagnosticTravel.x)),1);
                if (_WWDiagOutput==6) return half4((1-exp(-total)).xxx,1);
                #endif
                return half4(air*alpha,alpha);
            }
            ENDCG
        }
        Pass
        {
            // Multiply the completed world without sampling/copying camera color. Overlay UI renders afterward.
            Blend Zero SrcColor
            CGPROGRAM
            #pragma vertex darkVert
            #pragma fragment darkFrag
            #include "UnityCG.cginc"
            float _WWStormDarkening;
            float4 darkVert(appdata_img v):SV_POSITION { return float4(v.vertex.xy,0,1); }
            half4 darkFrag():SV_Target { float light=1-saturate(_WWStormDarkening); return half4(light,light,light,1); }
            ENDCG
        }
    }
    Fallback Off
}
