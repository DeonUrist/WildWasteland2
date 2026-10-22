using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Single owner of per-render camera overrides (README 4.2; "3 camera projection writers": Guns Ironsights wheel zoom,
    /// CleanHUD Binoculars zoom, PlayerBody ThirdPerson view/FOV). Today each subscribes Camera.onPreCull itself, rewrites the
    /// projection/view and restores it in onPostRender, so two of them can stack or undo each other. Here one onPreCull /
    /// onPostRender pair runs the registered writers per channel in priority order (highest first); the first writer that
    /// returns true owns that channel for this render, the others are skipped. Channels: Projection (matrix; the Ironsights
    /// and Binoculars m00/m11 scaling: also sets useJitteredProjectionMatrixForTransparentRendering and resets the culling
    /// matrix, exactly as both copies), View (worldToCameraMatrix; culling = projection * view, ThirdPerson), Fov (field of
    /// view). Every change is restored in onPostRender of the same camera (Reset*Matrix / previous FOV and jitter flag),
    /// unless the writer registered with persistent=true (then it is only reset when the writer stops claiming the channel).
    /// The Unity callbacks are subscribed only while at least one writer is registered. Not used until Wave B.
    /// </summary>
    public static class CameraArbiter
    {
        /// <summary>Projection writer: modify projection and return true to own the channel for this render of cam.</summary>
        public delegate bool ProjectionWriter(Camera cam, ref Matrix4x4 projection);
        /// <summary>View writer: set worldToCamera and return true to own the channel for this render of cam.</summary>
        public delegate bool ViewWriter(Camera cam, ref Matrix4x4 worldToCamera);
        /// <summary>FOV writer: set fov (degrees) and return true to own the channel for this render of cam.</summary>
        public delegate bool FovWriter(Camera cam, ref float fov);

        /// <summary>One registration.</summary>
        private sealed class Writer
        {
            public IModule Owner; public string Name; public int Priority; public bool Persistent;
            public ProjectionWriter Projection; public ViewWriter View; public FovWriter Fov;
        }

        static readonly List<Writer> _proj = new List<Writer>(), _view = new List<Writer>(), _fov = new List<Writer>();
        static bool _hooked;
        // what to undo after the render
        static Camera _projCam, _viewCam, _fovCam, _persistViewCam;
        static Writer _projWriter, _viewWriter, _fovWriter, _persistentWriter;
        static bool _jitter;
        static float _fovWas;

        /// <summary>Name of the writer that owned each channel in the last render (Debug page).</summary>
        public static string LastProjection, LastView, LastFov;

        /// <summary>Registers a projection writer (higher priority is asked first).</summary>
        public static void AddProjection(IModule owner, string name, int priority, ProjectionWriter w) { Insert(_proj, new Writer { Owner = owner, Name = name, Priority = priority, Projection = w }); }
        /// <summary>Registers a view writer; persistent keeps the view between renders (ThirdPerson behaviour) instead of restoring it after each.</summary>
        public static void AddView(IModule owner, string name, int priority, ViewWriter w, bool persistent = false) { Insert(_view, new Writer { Owner = owner, Name = name, Priority = priority, View = w, Persistent = persistent }); }
        /// <summary>Registers a field-of-view writer.</summary>
        public static void AddFov(IModule owner, string name, int priority, FovWriter w) { Insert(_fov, new Writer { Owner = owner, Name = name, Priority = priority, Fov = w }); }

        /// <summary>Inserts w sorted by descending priority (stable for equal priorities) and hooks the camera callbacks.</summary>
        static void Insert(List<Writer> list, Writer w)
        {
            int at = 0;
            while (at < list.Count && list[at].Priority >= w.Priority) at++;
            list.Insert(at, w);
            Hook();
        }

        /// <summary>Restores changes held by one named writer while retaining its registration for later frames.</summary>
        public static void Restore(IModule owner, string name)
        {
            if (Matches(_projWriter, owner, name)) { if (_projCam != null) { _projCam.useJitteredProjectionMatrixForTransparentRendering = _jitter; _projCam.ResetProjectionMatrix(); _projCam.ResetCullingMatrix(); } _projCam = null; _projWriter = null; }
            if (Matches(_viewWriter, owner, name)) { if (_viewCam != null) { _viewCam.ResetWorldToCameraMatrix(); _viewCam.ResetCullingMatrix(); } _viewCam = null; _viewWriter = null; }
            if (Matches(_persistentWriter, owner, name)) { if (_persistViewCam != null) { _persistViewCam.ResetWorldToCameraMatrix(); _persistViewCam.ResetCullingMatrix(); } _persistViewCam = null; _persistentWriter = null; }
            if (Matches(_fovWriter, owner, name)) { if (_fovCam != null) _fovCam.fieldOfView = _fovWas; _fovCam = null; _fovWriter = null; }
        }
        /// <summary>Removes all channels registered by one named writer and releases its current camera overrides.</summary>
        public static void Remove(IModule owner, string name)
        {
            Restore(owner, name);
            _proj.RemoveAll(w => Matches(w, owner, name)); _view.RemoveAll(w => Matches(w, owner, name)); _fov.RemoveAll(w => Matches(w, owner, name));
            if (_proj.Count == 0 && _view.Count == 0 && _fov.Count == 0) Unhook();
        }
        /// <summary>Whether a held channel belongs to the requested registration.</summary>
        static bool Matches(Writer writer, IModule owner, string name) { return writer != null && writer.Owner == owner && writer.Name == name; }

        /// <summary>Removes every writer of owner, restores what it may still hold, and unhooks when nothing is left.</summary>
        public static void RemoveAll(IModule owner)
        {
            if (_projWriter != null && _projWriter.Owner == owner) Restore(owner, _projWriter.Name);
            if (_viewWriter != null && _viewWriter.Owner == owner) Restore(owner, _viewWriter.Name);
            if (_fovWriter != null && _fovWriter.Owner == owner) Restore(owner, _fovWriter.Name);
            if (_persistentWriter != null && _persistentWriter.Owner == owner) Restore(owner, _persistentWriter.Name);
            _proj.RemoveAll(w => w.Owner == owner); _view.RemoveAll(w => w.Owner == owner); _fov.RemoveAll(w => w.Owner == owner);
            if (_proj.Count == 0 && _view.Count == 0 && _fov.Count == 0) Unhook();
        }

        /// <summary>Subscribes Camera.onPreCull/onPostRender once.</summary>
        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
        }

        /// <summary>Unsubscribes the camera callbacks.</summary>
        static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            Camera.onPreCull -= PreCull;
            Camera.onPostRender -= PostRender;
        }

        /// <summary>Camera.onPreCull: restores leftovers, then lets the first claiming writer of each channel modify cam.</summary>
        static void PreCull(Camera cam)
        {
            if (cam == null) return;
            RestoreAfterRender();
            LastProjection = LastView = LastFov = null;
            // FOV first: the projection writers then scale the FOV-derived matrix
            for (int i = 0; i < _fov.Count; i++)
            {
                float fov = cam.fieldOfView, was = fov;
                bool claim;
                try { claim = _fov[i].Fov(cam, ref fov); } catch (Exception e) { claim = false; Fail(_fov[i], e); }
                if (!claim) continue;
                _fovWriter = _fov[i]; _fovCam = cam; _fovWas = was; cam.fieldOfView = fov; LastFov = _fov[i].Name;
                break;
            }
            for (int i = 0; i < _proj.Count; i++)
            {
                var p = cam.projectionMatrix;
                bool claim;
                try { claim = _proj[i].Projection(cam, ref p); } catch (Exception e) { claim = false; Fail(_proj[i], e); }
                if (!claim) continue;
                _projWriter = _proj[i]; _projCam = cam; _jitter = cam.useJitteredProjectionMatrixForTransparentRendering;
                cam.projectionMatrix = p;
                cam.useJitteredProjectionMatrixForTransparentRendering = true;
                cam.ResetCullingMatrix();
                LastProjection = _proj[i].Name;
                break;
            }
            bool viewClaimed = false;
            for (int i = 0; i < _view.Count; i++)
            {
                var m = cam.worldToCameraMatrix;
                bool claim;
                try { claim = _view[i].View(cam, ref m); } catch (Exception e) { claim = false; Fail(_view[i], e); }
                if (!claim) continue;
                if (_persistViewCam != null && (_persistViewCam != cam || !_view[i].Persistent || _persistentWriter != _view[i]))
                { _persistViewCam.ResetWorldToCameraMatrix(); _persistViewCam.ResetCullingMatrix(); _persistViewCam = null; _persistentWriter = null; }
                cam.worldToCameraMatrix = m;
                cam.cullingMatrix = cam.projectionMatrix * m;
                if (_view[i].Persistent) { _persistViewCam = cam; _persistentWriter = _view[i]; } else { _viewCam = cam; _viewWriter = _view[i]; }
                LastView = _view[i].Name; viewClaimed = true;
                break;
            }
            if (!viewClaimed && ReferenceEquals(_persistViewCam, cam)) { cam.ResetWorldToCameraMatrix(); cam.ResetCullingMatrix(); _persistViewCam = null; _persistentWriter = null; }

        }

        /// <summary>Camera.onPostRender: restores the camera that was modified for this render.</summary>
        static void PostRender(Camera cam)
        {
            if (cam != null && (cam == _projCam || cam == _viewCam || cam == _fovCam)) RestoreAfterRender();
        }

        /// <summary>Undoes the non-persistent changes of the last render (Ironsights/Binoculars Restore).</summary>
        static void RestoreAfterRender()
        {
            if (_projCam != null) { _projCam.useJitteredProjectionMatrixForTransparentRendering = _jitter; _projCam.ResetProjectionMatrix(); _projCam.ResetCullingMatrix(); _projCam = null; }
            if (_viewCam != null) { _viewCam.ResetWorldToCameraMatrix(); _viewCam.ResetCullingMatrix(); _viewCam = null; }
            if (_fovCam != null) { _fovCam.fieldOfView = _fovWas; _fovCam = null; }
            _projWriter = _viewWriter = _fovWriter = null;
        }

        /// <summary>Logs a writer failure (Log rate-limits repeats).</summary>
        static void Fail(Writer w, Exception e)
        {
            Log.Warn(null,"[" + (w.Owner != null ? w.Owner.Id : "Core") + "] camera writer " + w.Name + " failed: " + e.Message);
        }
    }
}
