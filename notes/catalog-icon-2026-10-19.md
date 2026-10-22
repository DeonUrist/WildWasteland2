# Wild Wasteland catalog icon

Denis supplied a monochrome circular character/skull emblem and requested stylized options before publication. Built-in image_gen produced an A/B/C comparison; he explicitly chose **C — simplified emblem**. The first transparent export had artifacts and was rejected. The corrected second export retains opaque black facial details, white/gray shapes and a transparent exterior.

Final project asset: `icon.png`,128x128 RGBA,13,846 bytes. System.Drawing exported the generated artwork at catalog resolution with alpha retained; the saved icon was visually inspected. No Python image editing. Built-in image_gen was used, not a CLI/API fallback.

The root icon lets the existing Index builder find it at main when an older release tag/ZIP lacks an icon. Rebuilding the Index is sufficient; the1.0.1 DLL and published ZIP stay unchanged. The package tool now includes `WildWasteland/icon.png` in future ZIPs. Required icon limits: at most256x256 and48KiB. Avoid publishing an entry with a null icon when artwork is expected.

## Source and prompt

User reference: `C:/Users/chura/AppData/Local/Temp/codex-clipboard-10ec77bb-7e8a-40dd-8565-5a2eaa571973.png`.

Preview board: `C:/Users/chura/.codex/generated_images/01a153b0-6a9d-7103-ab45-13113c2f0f4e/exec-7360f187-edf4-4bc2-b954-780f3aff4eb8.png`.

Selected final source: `C:/Users/chura/.codex/generated_images/01a153b0-6a9d-7103-ab45-13113c2f0f4e/exec-cf52e3a4-cb17-40bc-8400-d6fee14adf5d.png`.

Final built-in prompt (transparent_background=true, preview board as reference):

> Produce ONE clean finished square icon using approved design C in the RIGHT column of the supplied reference. Faithfully reproduce C alone, centered with padding, no letter C or small duplicate. The icon is a flat white/gray emblem with OPAQUE BLACK interior details: a surprised cartoon man with BLACK dot eyes and BLACK mouth in a circular gray ring; white cowboy skull with BLACK hatband and BLACK eye sockets at upper left; white long-eared animal skull with solid BLACK sunglasses at upper right; white alien skull with solid BLACK eyes at lower right. Keep the circle interior solid BLACK, and only make the exterior of the complete emblem transparent. Use smooth vector-like solid shapes, no texture/noise/speckles/grain, no gray gradients, no distressed effect. All facial details must remain crisply BLACK on solid WHITE. Do not remove the black from eyes/glasses/mouth or from inside the ring. The outer transparent background must be clean, without stray flecks. Keep the exact simplified C style and layout. Only one emblem filling 85% of square canvas. Suitable for a mod list icon at small sizes.

No gameplay/source behavior, config, save or game deployment changes. Concurrent wiki work is excluded from the icon commit. The separate Apocasetter catalog sort change is necessary for first place among uninstalled mods because clients sort names themselves.
