using System;
using Den.Tools.Matrices;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    // Immutable, MapMagic-local samples. Workers never touch Terrain/Physics to choose a route.
    internal sealed class ElevationGrid
    {
        internal Rect Area;
        internal int Size;
        internal float[] Heights;
        internal static ElevationGrid Capture(MatrixWorld heights,Rect area)
        {
            if(heights==null)return null;
            const int size=65;var grid=new ElevationGrid{Area=area,Size=size,Heights=new float[size*size]};
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
                grid.Heights[z*size+x]=heights.GetWorldInterpolatedValue(area.xMin+area.width*x/(size-1f),area.yMin+area.height*z/(size-1f))*heights.worldSize.y+heights.worldPos.y;
            return grid;
        }
        internal bool Sample(Vector3 p,out float height,out float slope)
        {
            height=slope=0f;
            if(Size<2||Heights==null||p.x<Area.xMin||p.x>Area.xMax||p.z<Area.yMin||p.z>Area.yMax)return false;
            float fx=(p.x-Area.xMin)/Area.width*(Size-1),fz=(p.z-Area.yMin)/Area.height*(Size-1);
            int x=Math.Min((int)fx,Size-2),z=Math.Min((int)fz,Size-2);fx-=x;fz-=z;
            float a=Heights[z*Size+x],b=Heights[z*Size+x+1],c=Heights[(z+1)*Size+x],d=Heights[(z+1)*Size+x+1];
            height=Mathf.Lerp(Mathf.Lerp(a,b,fx),Mathf.Lerp(c,d,fx),fz);
            float dx=Mathf.Lerp(b-a,d-c,fz)*(Size-1)/Area.width,dz=Mathf.Lerp(c-a,d-b,fx)*(Size-1)/Area.height;
            slope=Mathf.Sqrt(dx*dx+dz*dz);return true;
        }
        internal static bool Sample(ElevationGrid[] grids,Vector3 p,out float height,out float slope)
        {foreach(var grid in grids)if(grid!=null&&grid.Sample(p,out height,out slope))return true;height=slope=0f;return false;}
    }
}
