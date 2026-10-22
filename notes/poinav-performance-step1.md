# PoiNav performance: step 1 (2026-10-21, Testing)

Scope: the first step of Denis's five-step performance plan, NPC module only.
Code confirms that SteerDir called TryNext from movement and Walk repeated the call;
Walk also allocated a RaycastAll result. No profiler evidence establishes the size
of the FPS loss or the ranking of the remaining suspects.

PoiNav caches the current waypoint on Route from Nav.Next/TryNext. SteerDir reads
that point, and Brain passes the resulting direction to Walk; Idle does the same.
Walking clamps its displacement at the cached point until the next think. Failed
queries and traversal starts invalidate the cached point; world shifts translate it.
The existing 0.75-second guidance expiry remains. Floor probes use one shared
64-hit RaycastNonAlloc buffer and attachedRigidbody self filtering. A full buffer
falls back to normal movement rather than trusting a truncated, unordered result.
Route length suffix sums are computed at plan time, retaining the current link-start
distance and outside-goal tail when answering pathLeft.

Three NPC source files changed. No config/save format changes. Replanning policy,
nearest-node searches, instance construction, population and Towns are unchanged.
Existing unrelated generated-LOD work remains in the combined build.

Windows Release with Deploy=false passed: zero errors, two existing CS0649 warnings
for Route.GlideAt/Glides. No game or harness was run. Base WildWasteland is absent
from the configured game's plugins directory; only WildWastelandMP is installed.
The base DLL was staged separately, not deployed. No commit, push or release.

Denis's check: use an active base installation and keep PoiNavigation enabled.
Compare the same Camp_7/Camp_15 fight and large-town walk before/after with
ModProfiler. Check resident walking, corners, car roofs, ladder approaches,
waiting for an occupied ladder, climbing/downward traversal and stopping after
combat. Expect normal routing with less movement-hook work, no waypoint
overshoot and no new stalling. Compare PoiNavigation on/off as a separate baseline.
No new diagnostic log lines were added; performance and gameplay remain unverified.
