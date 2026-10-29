# Leaf-space helper maps for leaf_veins.sbs (Substance Designer). Per leaf (alpha region) of a leaf-card texture:
#   *_along.png  16-bit  0 at the leaf base -> 1 at the tip along the leaf's long axis (0 outside leaves)
#   *_across.png 16-bit  distance from the (slightly bowed) midrib / leaf half width, 0 on the midrib -> 1 at the edge (1 outside)
#   *_alpha.png  8-bit   the card alpha (for the edge fade)
# common: 11 separate leaves, tip points away from the rosette centre. twisted: merged blobs, split at distance-field peaks.
# usage: python leaf_space.py <leaf texture.png> <out prefix> common|twisted
import sys, numpy as np
from PIL import Image
from scipy import ndimage as nd
src, pre, mode = sys.argv[1], sys.argv[2], sys.argv[3]
rng = np.random.default_rng(1029)
a = np.asarray(Image.open(src).convert('RGBA'))[..., 3].astype(np.float32) / 255
H, W = a.shape
solid = a > 0.35
if mode == 'twisted':
    dist = nd.distance_transform_edt(solid)
    peaks = (dist == nd.maximum_filter(dist, size=61)) & (dist > 22)
    markers, n = nd.label(nd.binary_dilation(peaks, iterations=10))
    comp, nc = nd.label(solid)
    lab = np.zeros(solid.shape, np.int32)
    for ci in range(1, nc + 1):          # each pixel joins the nearest leaf centre inside its own blob
        cm = comp == ci
        mk = np.where(cm, markers, 0)
        if mk.max() == 0: continue
        _, (iy, ix) = nd.distance_transform_edt(mk == 0, return_indices=True)
        lab[cm] = mk[iy, ix][cm]
else:
    lab, n = nd.label(nd.binary_closing(solid, iterations=2))
centre = np.array(nd.center_of_mass(solid))
along = np.zeros((H, W), np.float32); across = np.ones((H, W), np.float32)
grow = nd.binary_dilation(solid, iterations=3)      # cover the soft alpha fringe too
for k in range(1, lab.max() + 1):
    m = lab == k
    if m.sum() < 600: continue
    ys, xs = np.nonzero(m)
    c = np.array([ys.mean(), xs.mean()])
    w, v = np.linalg.eigh(np.cov(np.stack([ys - c[0], xs - c[1]])))
    ax = v[:, 1]
    if mode == 'common':
        if np.dot(ax, c - centre) < 0: ax = -ax
    elif rng.random() < 0.5: ax = -ax
    proj = (ys - c[0]) * ax[0] + (xs - c[1]) * ax[1]
    side = (ys - c[0]) * -ax[1] + (xs - c[1]) * ax[0]
    L0, L1 = np.percentile(proj, 1), np.percentile(proj, 99)
    halfW = np.percentile(np.abs(side), 95)
    bow = rng.uniform(-0.12, 0.12)
    mm = nd.binary_dilation(m, iterations=3) & grow
    yy, xx = np.nonzero(mm)
    t = ((yy - c[0]) * ax[0] + (xx - c[1]) * ax[1] - L0) / (L1 - L0)
    s = ((yy - c[0]) * -ax[1] + (xx - c[1]) * ax[0]) / halfW - bow * 4 * t * (1 - t)
    keep = across[yy, xx] > np.abs(s)                 # overlapping fringes: nearest midrib wins
    along[yy[keep], xx[keep]] = np.clip(t[keep], 0, 1)
    across[yy[keep], xx[keep]] = np.clip(np.abs(s[keep]), 0, 1)
def save16(img, path):
    Image.fromarray((np.clip(img, 0, 1) * 65535 + 0.5).astype(np.uint16)).save(path)
save16(along, pre + '_along.png'); save16(across, pre + '_across.png')
Image.fromarray((a * 255 + 0.5).astype(np.uint8)).save(pre + '_alpha.png')
print('leaves', lab.max())
