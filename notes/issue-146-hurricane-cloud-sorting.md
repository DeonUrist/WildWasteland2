# #146: Hurricane top/base clouds drawn through other bodies — Testing

Deployment follow-up: Denis confirmed the game closed. Verified no Apocalypter process, disabled manifest absent and active runtime asset folders present; copied only the uniquely staged DLL to `E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/WildWasteland/WildWasteland.dll`. Installed MD5 **C440D41FDBAF5211537C8C0530703414** matches the stage. This supersedes the pending-deployment statement below. No rebuild/game launch/harness/commit/push/release; runtime appearance remains Testing.

Issue: https://github.com/DeonUrist/WildWasteland/issues/146 (title only; no body/comments).

Cause: WeatherVortexSmoke assigned every body to transparent queue 3001 and every top/base particle bank to 3002. The latter queue draws after all bodies regardless of distance. HurricaneSand and SmokeParticles both use premultiplied alpha and ZWrite Off, so the opaque camera depth texture cannot reject a cloud behind another hurricane. WeatherSmoke's existing Distance particle sorting only sorts within its bank. The native SandStorm hierarchy was inspected in Sources; these body/cloud materials are WW-owned. Live LogOutput.log confirms WW 1.0.4 and Weather on; installed pre-fix DLL MD5 BB5060CC4EF3E91C9C9A0909C5B52EFE. The log contains no storm-preview/render diagnostic capture; runtime visual confirmation remains Denis's test.

Fix: WeatherVortexSmoke.cs now assigns the cloud clone the body's queue, allowing Unity's existing transparent distance sorting to interleave distant cloud banks and nearer bodies. Three added lines/one removed line include a short explanatory comment and startup confirmation. No shader/bundle, geometry, palette, particle count, configuration, save, Core or other-module change. Existing pending crew/wiki work is preserved. Reference: https://docs.unity.cn/2020.2/Documentation/Manual/built-in-rendering-order.html (queues render separately; distance sorting operates within a queue).

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` succeeded: zero warnings/errors. Unique stage: `V:/Repos/Apocalypter/_releases/WildWasteland_issue146_20261019_233531.dll`; build/stage MD5 **C440D41FDBAF5211537C8C0530703414**. Game process 26704 was running, so the installed DLL was not replaced. Apocasetter disabled manifest absent and active asset folders present. Deployment pending game closure. No game launch/harness, commit, push or release. Board status Testing.

After deployment/restart, confirm `[Weather] Hurricane bodies and top/base clouds share transparent queue 3001 for distance sorting.` when Weather initializes its visuals. This confirms the renderer policy, not image quality.

Denis's retest:

1. F3 > Debug: enable Developer mode (Enable Debug must already be on); Developer > Weather > Weather visual preview: diagnostics off, clear previews, choose Critical dust storm preview ahead. Allow 40 gameplay seconds for formation.
2. Align two hurricanes so the farther one's top or base overlaps the nearer body's silhouette. Move sideways and reverse the viewing order. Expect the nearer dense body to hide the farther cloud effect, with soft blending at transparent edges.
3. Inspect both thin/wide tops, ground dust, then an eye looking outward/up. Expect the established spiral, ground blending and open eye to remain. Check the same view's FPS.

This removes the unconditional queue-priority error. Unity still sorts transparent renderers/banks as units, and GPU orbiting particles retain their existing reference-position sorting; intersecting translucent banks can still exhibit sorting limits. Do not stack another rendering experiment if this retest fails: revert this correction first and obtain the matching DLL hash, log and affected view from Denis.
