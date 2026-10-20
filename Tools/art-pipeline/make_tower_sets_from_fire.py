# -*- coding: utf-8 -*-
"""Tower01 Idle / Aim / Disable 派生脚本 —— 定妆图 = Tower01_Fire_00

为什么单独一个脚本：用户指定「炮塔射击时的第一帧」为塔的定妆基准，Idle/Aim/Disable
三套的塔体必须与它完全一致；文生图做不到像素级一致，因此不再走 process_sheets.py 的
生成式管线，改为直接以 Fire_00 为底图逐帧叠加效果。

三套的效果（除 Disable 的「变灰」按语义作用于塔体颜色外，其余一律只写在背景区）：
  Idle   : 塔体不动；罐顶红色警戒灯辉光脉动 + 风扇轮外侧风影弧（对应 警戒灯/风扇）
  Aim    : 塔体不动；炮口绿气蓄能，由弱到强（攻击前摇，末帧锁定最强）
  Disable: 塔体几何不动；逐帧去饱和变灰（被净化停机），顶部冒烟，末帧全灰

硬约束（脚本保证）：
  - 画格与 Fire 同为 1039x979，塔体在格内的像素坐标与 Fire_00 完全相同 -> 四套零位移；
  - 底图 alpha<=8 的背景区才叠加；塔体（alpha>8）像素除 Disable 的变灰外逐点不变；
  - 输出沿用 Frames/<name>/<name>_NN.png 原名与原帧数（Idle 4 / Aim 4 / Disable 6），
    .meta/guid 不变 -> 三个 .anim 引用不失效。

用法：python make_tower_sets_from_fire.py
"""
import sys, os
sys.stdout.reconfigure(encoding='utf-8')
import numpy as np
from PIL import Image
from scipy import ndimage

TOWER = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..',
                     'Assets', 'Art', 'Art', 'Towers', 'Tower01')
FRAMES = os.path.join(TOWER, 'Frames')
SHEETS = os.path.join(TOWER, 'Sheets')
BASE_PNG = os.path.join(FRAMES, 'Tower01_Fire', 'Tower01_Fire_00.png')

CELL = (1039, 979)          # 与 Fire 同规格（塔体像素坐标与 Fire_00 完全一致）
ALPHA_MIN = 8
GREEN = np.array([124.0, 236.0, 72.0])
WARM = np.array([255.0, 96.0, 48.0])
ARC_RGB = np.array([232.0, 232.0, 232.0])
SMOKE_RGB = np.array([168.0, 168.0, 168.0])
FAN_C = (695.0, 705.0)      # 风扇轮中心（Fire_00 像素坐标，实测）
FAN_R = 95.0
K_BARREL = 1.6              # 炮口绿气的轴向拉伸


# ---------- 基础工具 ----------

def load_base():
    im = Image.open(BASE_PNG).convert('RGBA')
    a = np.asarray(im).astype(np.float32)
    assert (a.shape[1], a.shape[0]) == CELL, f'底图尺寸 {a.shape[1]}x{a.shape[0]} != {CELL}'
    return a


def overlay(base, cov, rgb, fg=None):
    """把覆盖度 cov 叠加到 base 的背景区，返回新 RGBA（uint8 数组）。
    cov 在塔体像素（fg）上强制归零 —— 保证塔体逐点不变。"""
    if fg is None:
        fg = base[..., 3] > ALPHA_MIN
    a = base[..., 3]
    cov = np.clip(cov, 0.0, 1.0).copy()
    cov[fg] = 0.0
    out_a = np.maximum(a, cov * 255.0)
    w = (cov * 255.0) / np.maximum(out_a, 1.0)
    out_rgb = base[..., :3] * (1.0 - w[..., None]) + rgb * w[..., None]
    return np.clip(np.dstack([out_rgb, out_a]), 0, 255).astype(np.uint8)


def radial_field(shape, cx, cy, ):
    yy, xx = np.mgrid[0:shape[0], 0:shape[1]]
    return np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2), xx - cx, yy - cy


def red_blobs(a):
    """红色部件（罐顶警戒灯 / 塔身红色圆灯）的 (cy, cx, 等效半径)，按 y 从小到大排序。"""
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = (r > 150) & (g < 120) & (b < 120) & (r - np.maximum(g, b) > 55)
    lbl, n = ndimage.label(m)
    out = []
    for k in range(1, n + 1):
        ys, xs = np.where(lbl == k)
        if len(ys) < 150:
            continue
        out.append((ys.mean(), xs.mean(), float(np.sqrt(len(ys) / np.pi))))
    out.sort()
    return out


def muzzle_point(alpha):
    """炮口：最左 10 列不透明像素质心。"""
    ys, xs = np.where(alpha > ALPHA_MIN)
    x0 = xs.min()
    sel = xs <= x0 + 10
    return ys[sel].mean(), xs[sel].mean()


def barrel_dir(fg, my, mx):
    """炮管轴向 = 炮口 - 炮口附近塔体质心。"""
    yy, xx = np.mgrid[0:fg.shape[0], 0:fg.shape[1]]
    near = fg & ((yy - my) ** 2 + (xx - mx) ** 2 <= 80 ** 2)
    cy, cx = yy[near].mean(), xx[near].mean()
    v = np.array([mx - cx, my - cy], float)
    n = float(np.hypot(*v))
    return v / n if n > 1e-6 else np.array([-1.0, 0.0])


# ---------- 三套的逐帧效果 ----------

def idle_frame(base, cap, halo_s, arc_deg):
    H, W = base.shape[:2]
    fg = base[..., 3] > ALPHA_MIN
    d, _, _ = radial_field((H, W), cap[1], cap[0])
    r = max(46.0, 2.0 * cap[2])
    out = overlay(base, (np.clip(1.0 - d / r, 0.0, 1.0) ** 1.6) * halo_s, WARM, fg)
    if arc_deg is not None:
        df, dx, dy = radial_field((H, W), *FAN_C)
        ang = np.degrees(np.arctan2(dy, dx))
        # 风影弧紧贴风扇轮缘：带内半圈压在轮盘上（被塔体遮掉），外半圈才露出，
        # 因此露出的弧必然与轮缘相邻 -> 不会变成游离背景的孤立灰斑。
        band = np.clip(1.0 - np.abs(df - (FAN_R + 4.0)) / 16.0, 0.0, 1.0)
        wnd = np.zeros_like(df)
        for base_ang in (-40.0, 0.0, 40.0):                          # 三道风影弧
            delta = (ang - (base_ang + arc_deg) + 180.0) % 360.0 - 180.0
            wnd = np.maximum(wnd, np.clip(1.0 - np.abs(delta) / 24.0, 0.0, 1.0))
        out = overlay(out.astype(np.float32), band * wnd * 0.45, ARC_RGB, fg)
    return out


def aim_frame(base, r, strength):
    H, W = base.shape[:2]
    alpha = base[..., 3]
    fg = alpha > ALPHA_MIN
    my, mx = muzzle_point(alpha)
    ux, uy = barrel_dir(fg, my, mx)
    _, dx, dy = radial_field((H, W), mx, my)
    along = dx * ux + dy * uy
    perp = -dx * uy + dy * ux
    d = np.sqrt((along / K_BARREL) ** 2 + perp ** 2)
    cov = (np.clip(1.0 - d / r, 0.0, 1.0) ** 1.5) * strength
    return overlay(base, cov, GREEN)


def disable_frame(base, t, dim, smoke):
    """t: 去饱和程度 0..1；dim: 亮度系数；smoke: (中心x, 中心y, 半径, 强度) 列表"""
    a = base[..., 3]
    rgb = base[..., :3]
    gray = (rgb @ np.array([0.299, 0.587, 0.114], np.float32))[..., None]
    out_rgb = (rgb * (1.0 - t) + gray * t) * dim
    out = np.clip(np.dstack([out_rgb, a]), 0, 255).astype(np.uint8)
    if smoke:
        H, W = a.shape
        cov = np.zeros((H, W), np.float32)
        for (sx, sy, sr, ss) in smoke:
            d, _, _ = radial_field((H, W), sx, sy)
            cov = np.maximum(cov, np.clip(1.0 - d / sr, 0.0, 1.0) ** 1.2 * ss)
        out = overlay(out.astype(np.float32), cov, SMOKE_RGB)
    return out


def smoke_plume(sx, sy, t):
    """按帧进度 t(0..1) 生成一股上升烟柱的烟团；t=0 时无烟。"""
    if t <= 0:
        return []
    puffs = []
    for k, (rise, rad, ss) in enumerate(((18.0, 26.0, 0.50), (48.0, 34.0, 0.42),
                                         (84.0, 42.0, 0.34), (126.0, 48.0, 0.24))):
        puffs.append((sx + (k % 2) * 20.0 - 6.0,
                      sy - rise * (0.35 + 0.65 * t),
                      rad * (0.5 + 0.5 * t),
                      ss * t))
    return puffs


# ---------- 输出 ----------

def write_set(name, imgs, cols):
    d = os.path.join(FRAMES, name)
    os.makedirs(d, exist_ok=True)
    for i, im in enumerate(imgs):
        im.save(os.path.join(d, f'{name}_{i:02d}.png'))
    rows = (len(imgs) + cols - 1) // cols
    cw, ch = CELL
    sheet = Image.new('RGBA', (cw * cols, ch * rows), (0, 0, 0, 0))
    for i, im in enumerate(imgs):
        sheet.paste(im, ((i % cols) * cw, (i // cols) * ch))
    sp = os.path.join(SHEETS, f'{name}_sheet.png')
    sheet.save(sp)
    praw = os.path.join(SHEETS, 'raw', f'{name}_raw.png')
    os.makedirs(os.path.dirname(praw), exist_ok=True)
    sheet.save(praw)
    print(f'  [OK] {name}: {len(imgs)} 帧  {cols}x{rows}  sheet={sheet.size}  -> {os.path.relpath(sp, TOWER)}')


def main():
    base = load_base()
    blobs = red_blobs(base)
    print('  红色部件（y 升序）:', [(round(b[0]), round(b[1]), b[2]) for b in blobs])
    cap = blobs[0]                     # 最靠上的红件 = 罐顶警戒灯
    print(f'  警戒灯 = ({cap[1]:.0f},{cap[0]:.0f})  风扇 = {FAN_C}')

    print('\n[1/3] Idle（塔体不动 + 警戒灯脉动 + 风影弧）')
    idle = [Image.fromarray(idle_frame(base, cap, hs, ad), 'RGBA')
            for hs, ad in ((0.0, None), (0.50, 18.0), (0.85, 36.0), (0.50, 0.0))]
    write_set('Tower01_Idle', idle, 2)

    print('\n[2/3] Aim（塔体不动 + 炮口绿气蓄能）')
    aim = [Image.fromarray(aim_frame(base, r, s), 'RGBA')
           for r, s in ((16, 0.0), (26, 0.62), (38, 0.80), (52, 0.96))]
    write_set('Tower01_Aim', aim, 2)

    print('\n[3/3] Disable（塔体几何不动 + 逐帧变灰 + 冒烟）')
    ys, xs = np.where(base[..., 3] > ALPHA_MIN)
    sx = float((xs.min() + xs.max()) / 2 + 52)      # 顶部偏右：从塔身最高处升起
    sy = float(ys.min() + 112)
    plan = [(0.00, 1.00, 0.00), (0.08, 1.00, 0.25), (0.22, 0.99, 0.50),
            (0.45, 0.97, 0.70), (0.70, 0.95, 0.85), (1.00, 0.92, 1.00)]
    dis = [Image.fromarray(disable_frame(base, t, dim, smoke_plume(sx, sy, st)), 'RGBA')
           for (t, dim, st) in plan]
    write_set('Tower01_Disable', dis, 3)


if __name__ == '__main__':
    main()
