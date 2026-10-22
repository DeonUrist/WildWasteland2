using System;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;

namespace WildWasteland.Modules.Weather
{
    // Paused native particles: upload on layout/quality changes only. Unity sorts
    // billboards; texture animation and rolling movement remain on the GPU.
    internal sealed class WeatherSmoke : IDisposable
    {
        private sealed class Bank
        {
            internal GameObject Owner;
            internal ParticleSystem System;
            internal ParticleSystemRenderer Renderer;
            internal float Span, Depth;
            internal int Detail = -1;
            internal bool Used;
            internal bool Leading;
        }
        private readonly Bank[] _banks;
        private readonly ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[320];
        private readonly Material _material;
        private readonly bool _vortex;
        private Camera _camera;
        internal WeatherSmoke(Material material, bool vortex = false) { _material = material; _vortex = vortex; _banks = new Bank[vortex ? 320 : 192]; Camera.onPreCull += BeforeCamera; }
        internal void Begin(Camera camera)
        {
            _camera = camera;
            for (int i = 0; i < _banks.Length; i++) if (_banks[i] != null) _banks[i].Used = false;
        }
        internal void Draw(int slot, Vector3 position, Quaternion rotation, float span, float depth, WeatherVisualDetail detail, bool leading, MaterialPropertyBlock properties)
        {
            var bank = _banks[slot];
            if (bank == null)
            {
                bank = new Bank(); _banks[slot] = bank;
                bank.Owner = new GameObject((_vortex ? "WW.Weather.VortexCloud." : "WW.Weather.Smoke.")+slot) { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
                bank.Owner.transform.SetParent(Runner.Host.transform,false);
                bank.System = bank.Owner.AddComponent<ParticleSystem>();
                bank.System.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = bank.System.main; main.playOnAwake = false; main.loop = false; main.maxParticles = _particles.Length;
                main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = 0; main.startSize3D = true;
                var emission = bank.System.emission; emission.enabled = false;
                var shape = bank.System.shape; shape.enabled = false;
                bank.Renderer = bank.Owner.GetComponent<ParticleSystemRenderer>();
                bank.Renderer.sharedMaterial = _material; bank.Renderer.sortMode = ParticleSystemSortMode.Distance;
                bank.Renderer.renderMode = ParticleSystemRenderMode.Billboard; bank.Renderer.alignment = ParticleSystemRenderSpace.View;
                bank.Renderer.maxParticleSize = 1; bank.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                bank.Renderer.receiveShadows = false; bank.Renderer.lightProbeUsage = LightProbeUsage.Off;
                if (_vortex) bank.Renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream> {
                    ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.Center });
            }
            bank.Used = true; bank.Owner.transform.SetPositionAndRotation(position,rotation);
            if (bank.Detail != (int)detail || bank.Span != span || bank.Depth != depth || bank.Leading != leading)
            {
                bank.Detail = (int)detail; bank.Span = span; bank.Depth = depth; bank.Leading = leading;
                int across = detail == WeatherVisualDetail.Low ? 4 : detail == WeatherVisualDetail.Max ? 10 : 6;
                int rows = detail == WeatherVisualDetail.Low ? 3 : 4;
                int count = 0; var random = new System.Random(7013+slot*397);
                if (_vortex)
                {
                    int along = detail == WeatherVisualDetail.Low ? 8 : detail == WeatherVisualDetail.Max ? 18 : 12;
                    int spiral = along*3;
                    for (int arm = 0; arm < 2; arm++) for (int i = 0; i < spiral; i++)
                    {
                        float seed = (float)random.NextDouble(), u = (i+seed*.3f)/(spiral-1);
                        float width = Mathf.Lerp(span,depth,Mathf.SmoothStep(0,1,u));
                        float angle = arm*Mathf.PI+u*7+slot*1.731f+(seed-.5f)*.05f;
                        float radius = (420+u*1590)*width;
                        float size = (440+seed*130+u*150)*width;
                        Vector3 point = new Vector3(Mathf.Cos(angle)*radius,1050+u*380+(seed-.5f)*25,Mathf.Sin(angle)*radius);
                        _particles[count++] = Particle(point,size,.7f,seed,1,1);
                        if (i%3 == 0) _particles[count++] = Particle(point+Vector3.up*35,size*3.2f,.6f,seed,0,1,.7f);
                    }
                    int around = along*2;
                    for (int ring = 0; ring < 3; ring++) for (int i = 0; i < around; i++)
                    {
                        float seed = (float)random.NextDouble(), angle = (i+seed*.6f)*Mathf.PI*2/around;
                        float scale = Mathf.Max(.25f,span), radius = 600*span+ring*150*scale+(seed-.5f)*100*scale;
                        float size = (280+seed*200)*scale;
                        _particles[count++] = Particle(new Vector3(Mathf.Cos(angle)*radius,size*(.10f+seed*.12f)-20,Mathf.Sin(angle)*radius),size,.7f,seed,1,0);
                    }
                    int clouds = detail == WeatherVisualDetail.Low ? 8 : detail == WeatherVisualDetail.Max ? 16 : 12;
                    for (int arm = 0; arm < 2; arm++) for (int i = 0; i < clouds; i++)
                    {
                        float seed = (float)random.NextDouble(), u = i/(float)(clouds-1), spread = Mathf.Max(.7f,depth);
                        float angle = arm*Mathf.PI+7+u*2.4f+slot*1.731f+(seed-.5f)*.2f;
                        float radius = Mathf.Lerp(1700*depth,3150*spread,u), size = (1300+u*800+seed*300)*spread;
                        _particles[count++] = Particle(new Vector3(Mathf.Cos(angle)*radius,1500+u*140+seed*70,Mathf.Sin(angle)*radius),size,.55f,seed,u,.75f,.55f-u*.33f);
                    }
                }
                else
                {
                for (int band = 0; band < 2; band++) for (int row = 0; row < rows; row++) for (int x = 0; x < across; x++)
                {
                    float seed = (float)random.NextDouble(), u = (x+.5f+(seed-.5f)*.7f)/across;
                    float h = row/(float)(rows-1), size = 530+seed*240;
                    float y = 100+h*780+(float)random.NextDouble()*70;
                    if (row == 0) { size = 260+seed*200; y = size*.18f; }
                    _particles[count++] = Particle(new Vector3((u-.5f)*span,y,-depth+band*130+(seed-.5f)*100),size,row == 0 ? .65f : 1,seed,row == 0 ? .5f : 0,0);
                }
                int canopy = detail == WeatherVisualDetail.Low ? 3 : detail == WeatherVisualDetail.Max ? 6 : 4;
                for (int i = 0; i < canopy; i++)
                {
                    float seed = (float)random.NextDouble();
                    _particles[count++] = Particle(new Vector3(((i+.5f)/canopy-.5f)*span,1080+seed*250,-depth*.4f+(i%3)*220),1700+seed*650,.65f,seed,0,1);
                }
                if (leading)
                {
                    int rollers=across*2;
                    for (int row=0;row<2;row++) for (int i=0;i<rollers;i++)
                    {
                        float seed=(float)random.NextDouble(), size=280+seed*200;
                        _particles[count++] = Particle(new Vector3(((i+.5f+(row == 0 ? 0 : .35f))/rollers-.5f)*span,size*.18f,-60-row*100-seed*300),size,.7f,seed,1,0);
                    }
                }
                }
                bank.System.Play(); bank.System.Pause(); bank.System.SetParticles(_particles,count);
            }
            bank.Renderer.SetPropertyBlock(properties); bank.Renderer.enabled = true;
        }
        private static ParticleSystem.Particle Particle(Vector3 position, float size, float aspect, float seed, float leading, float canopy, float opacity = 1)
        {
            return new ParticleSystem.Particle { position = position, velocity = Vector3.zero, startSize3D = new Vector3(size,size*aspect,size),
                rotation = (seed-.5f)*50, startColor = new Color(seed,leading,canopy,opacity), startLifetime = 100000, remainingLifetime = 100000, randomSeed = (uint)(seed*1000000)+1 };
        }
        private void BeforeCamera(Camera camera)
        {
            for (int i = 0; i < _banks.Length; i++) if (_banks[i] != null) _banks[i].Renderer.enabled = _banks[i].Used && camera == _camera;
        }
        internal void Suspend()
        {
            _camera = null;
            for (int i = 0; i < _banks.Length; i++) if (_banks[i] != null) { _banks[i].Used = false; _banks[i].Renderer.enabled = false; }
        }
        public void Dispose()
        {
            Camera.onPreCull -= BeforeCamera; Suspend();
            for (int i = 0; i < _banks.Length; i++) if (_banks[i] != null) UnityEngine.Object.Destroy(_banks[i].Owner);
        }
    }
}
