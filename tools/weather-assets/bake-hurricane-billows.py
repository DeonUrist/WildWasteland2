"""Original offline rounded cloud-density artwork; no gameplay or fluid simulation."""
import argparse
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("output", type=Path)
parser.add_argument("--detail", type=Path, required=True)
args = parser.parse_args()
n = 128
rng = np.random.default_rng(194763)
z, y, x = np.indices((n, n, n), dtype=np.float32)
points = np.stack((x.ravel(), y.ravel(), z.ravel()), axis=1) / n
channels = []
for lattice, radius in ((4, .18), (7, .105), (22, .032)):
    centres = (np.indices((lattice, lattice, lattice)).reshape(3, -1).T + rng.uniform(.12, .88, (lattice**3, 3))) / lattice
    distance = cKDTree(centres, boxsize=1).query(points, workers=1)[0]
    cloud = np.exp(-np.square(distance / radius)).reshape(n, n, n)
    channels.append(cloud)
detail = np.fromfile(args.detail, dtype=np.uint8).reshape(n, n, n).astype(np.float32) / 255
pixels = np.stack((channels[0], channels[1], channels[2]*.8+detail*.2, channels[0]*.65+channels[1]*.35), axis=-1)
args.output.parent.mkdir(parents=True, exist_ok=True)
(np.clip(pixels, 0, 1)*255).astype(np.uint8).tofile(args.output)
print(f"Rounded periodic billow density: {args.output} ({args.output.stat().st_size} bytes)")
