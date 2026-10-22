"""Offline original volume/flow artwork. No game execution or runtime simulation.

Bakes an advected, lit density volume into a 32-frame RGBA atlas and a seamless
16-frame flow atlas and shared 128³ density for bounded vortices. NumPy/SciPy/Pillow only.
"""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates, zoom, gaussian_filter


def smooth(value):
    value = np.clip(value, 0, 1)
    return value * value * (3 - 2 * value)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--volume", type=int, default=192)
    parser.add_argument("--noise-only", action="store_true")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(837219)
    n = args.volume
    field = np.zeros((n, n, n), dtype=np.float32)
    for lattice, weight in ((6, .5), (12, .26), (24, .14), (48, .07), (96, .03)):
        small = rng.random((lattice, lattice, lattice), dtype=np.float32)
        field += zoom(small, n/lattice, order=2, mode="grid-wrap", prefilter=True).astype(np.float32) * weight
    field = np.clip((field-.3)*2.2, 0, 1)
    noise3d = zoom(field,128/n,order=1,mode="grid-wrap")
    (np.clip(noise3d,0,1)*255).astype(np.uint8).tofile(args.output/"vortex-noise-128.bytes")
    del noise3d
    if args.noise_only:
        print("VOLUME DENSITY TEXTURE COMPLETE",flush=True)
        return
    z, y, x = np.meshgrid(*([np.linspace(-1, 1, n, dtype=np.float32)]*3), indexing="ij")
    atlas = Image.new("RGBA", (2048, 4096), (128, 128, 128, 0))
    for frame in range(32):
        theta = frame*np.pi*2/32
        # Continuous periodic circulation; frame 32 would equal frame 0.
        cy = y*np.cos(theta) - z*np.sin(theta)
        cz = y*np.sin(theta) + z*np.cos(theta)
        coordinates = np.array(((cz+1)*n*.5, (cy+1)*n*.5,
            (x+1)*n*.5+np.sin(cy*5+theta)*5), dtype=np.float32)
        noise = map_coordinates(field, coordinates, order=1, mode="grid-wrap")
        del coordinates, cy, cz
        radius = np.sqrt((x*.98)**2 + (y*1.08)**2 + (z*1.12)**2)
        envelope = smooth((1-radius+(noise-.5)*.32)/.2)
        density = smooth((noise-.34)*3.1)*envelope
        # Approximate offline single scattering from a broad upper-left light.
        sun = np.cumsum(density[:, ::-1, :], axis=1)[:, ::-1, :]*.72
        sun += np.cumsum(density, axis=2)*.28
        lighting = .13 + .87*np.exp(-sun*(6/n))
        optical = density*(8/n)
        transmittance = np.exp(-(np.cumsum(optical, axis=0)-optical))
        weights = transmittance*(1-np.exp(-optical))
        alpha = np.sum(weights, axis=0)
        shade = np.sum(weights*lighting, axis=0)/np.maximum(alpha, .00001)
        shade = np.clip(.18+shade*.82, 0, 1)
        pixels = np.zeros((n, n, 4), dtype=np.uint8)
        pixels[:, :, :3] = (shade[:, :, None]*255).astype(np.uint8)
        pixels[:, :, 3] = (np.clip(alpha, 0, 1)*255).astype(np.uint8)
        tile = Image.fromarray(pixels).transpose(Image.Transpose.FLIP_TOP_BOTTOM).resize((504, 504), Image.Resampling.LANCZOS)
        atlas.paste(tile, ((frame % 4)*512+4, (frame//4)*512+4))
        if frame % 4 == 0:
            tile.save(args.output/f"billow-frame-{frame:02d}.png")
        print(f"VOLUME FRAME {frame+1}/32", flush=True)
        del noise, radius, envelope, density, sun, lighting, optical, transmittance, weights
    atlas.save(args.output/"dust-loop.png")
    del field, x, y, z, atlas
    side = 256
    fy, fx = np.meshgrid(np.fft.fftfreq(side), np.fft.rfftfreq(side), indexing="ij")
    spectrum = (rng.standard_normal(fx.shape)+1j*rng.standard_normal(fx.shape))
    spectrum *= (fx*fx+fy*fy+.00008)**(-.72)
    noise = np.fft.irfft2(spectrum, s=(side, side)).real
    noise = (noise-noise.mean())/noise.std()*.15+.5
    yy, xx = np.meshgrid(np.arange(side), np.arange(side), indexing="ij")
    flow = Image.new("RGB", (1024,1024))
    for frame in range(16):
        theta = frame*np.pi*2/16
        coordinates = np.array((yy+np.cos(theta)*35+np.sin(xx/side*np.pi*2+theta)*13,
            xx+np.sin(theta)*35+np.sin(yy/side*np.pi*4-theta)*19))
        density = map_coordinates(noise, coordinates, order=1, mode="grid-wrap")
        broad = gaussian_filter(density, 7, mode="wrap")
        ridge = np.clip(.5+np.gradient(density, axis=1)*5,0,1)
        tile = np.clip(np.stack((density,broad,ridge),axis=2)*255,0,255).astype(np.uint8)
        flow.paste(Image.fromarray(tile), ((frame%4)*side,(frame//4)*side))
    flow.save(args.output/"dust-flow.png")
    (args.output/"animated-dust.provenance.json").write_text(json.dumps({
        "author": "Wild Wasteland original procedural artwork", "seed": 837219,
        "cloud": {"frames": 32, "grid": [4,8], "cell": 512, "volume_grid": n,
            "lighting": "offline approximate single scattering; upper-left broad light", "loop": True},
        "flow": {"frames": 16, "grid": [4,4], "cell": 256, "loop": True,
            "channels": "density, broad density, horizontal ridge lighting"},
        "vortex_density": {"grid": [128,128,128], "format": "R8", "bytes": 2097152},
        "note": "No ApocaDustStorm images/code or movie-frame pixels are included. Runtime uses baked textures only."
    }, indent=2)+"\n")
    print("ANIMATED DUST BAKE COMPLETE", flush=True)


if __name__ == "__main__":
    main()
