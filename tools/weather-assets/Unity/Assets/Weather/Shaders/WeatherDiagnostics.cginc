#ifndef WW_WEATHER_DIAGNOSTICS
#define WW_WEATHER_DIAGNOSTICS
int _WWDiagOutput;
float _WWDiagOwner, _WWDiagShape, _WWDiagNoAtmosphere, _WWDiagSmokeLayer;
float3 WWDiagColor(float id)
{
    return .2+.8*frac(sin(float3(id+1,id+7,id+13)*float3(12.9898,78.233,39.425))*43758.5453);
}
float WWDiagDistance(float metres) { return saturate(log2(1+max(0,metres))/14); }
#endif
