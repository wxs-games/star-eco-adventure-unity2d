# -*- coding: utf-8 -*-
"""Tower01_Aim 派生脚本（塔体零改动）

为什么单独一个脚本：Aim 的塔体要求与 Tower01_Idle 的定妆**完全一样**，
文生图做不到像素级一致，因此不再走 process_sheets.py 的生成式管线，改为直接以
Idle 的 4 帧为底图，只在**透明背景区**叠加炮口「蓄能/锁定」绿气光效，逐帧增强。

关键约束（脚本已保证）：
  - 光效只写在 alpha==0 的背景像素上；塔体（alpha>0）的 RGB 与 alpha **逐点不变**；
  - 输出文件名沿用 Tower01_Aim_00..03.png -> .meta/guid 不变 -> Aim.anim 引用不失效；
  - 画面尺寸沿用 942x979，重拼 2x2 sheet 与 Idle/Disable 同规格，无需缩放。

用法：python make_aim_from_idle.py
"""
import sys, os
sys.stdout.reconfigure(encoding='utf-8')
import numpy as np
from PIL import Image

TOWER = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..',
                     'Assets', 'Art', 'Art', 'Towers', 'Tower01')
IDLE_DIR = os.path.join(TOWER, 'Frames', 'Tower01_Idle')
AIM_DIR = os.path.join(TOWER, 'Frames', 'Tower01_Aim')
SHEETS = os.path.join(TOWER, 'Sheets')

CELL = (942, 979)          # 与 Idle / Disable 同规格
N = 4
GLOW_RGB = np.array([124.0, 236.0, 72.0])       # 毒气绿
GLOW_R = [16, 26, 38, 52]                        # 逐帧扩张半径（轴向再乘 K，确保不触格边）
GLOW_A = [0.45, 0.62, 0.80, 0.96]                # 逐帧增强强度
K = 1.6                                          # 轴向拉伸 -> 锥形喷气而非圆光斑
ALPHA_MIN = 8


def muzzle_point(alpha):
    """炮口位置：最左侧一小段不透明像素的质心（炮管顶端）。"""
    ys, xs = np.where(alpha > ALPHA_MIN)
    x0 = xs.min()
    sel = xs <= x0 + 10
    return ys[sel].mean(), xs[sel].mean()


def barrel_dir(fg, my, mx):
    """炮管轴向：炮口指向外 = 炮口位置 - 炮口附近塔体（炮管）质心。"""
    H, W = fg.shape
    yy, xx = np.mgrid[0:H, 0:W]
    near = fg & ((yy - my) ** 2 + (xx - mx) ** 2 <= 80 ** 2)
    if near.sum() == 0:
        return np.array([-1.0, 0.0])
    cy, cx = yy[near].mean(), xx[near].mean()
    v = np.array([mx - cx, my - cy])            # (dx, dy)，图像坐标 y 向下
    n = np.hypot(v[0], v[1])
    return v / n if n > 1e-6 else np.array([-1.0, 0.0])


def make_frame(path, r, strength):
    im = Image.open(path).convert('RGBA')
    src = np.asarray(im).astype(np.float32)
    alpha = src[..., 3]
    fg = alpha > ALPHA_MIN
    my, mx = muzzle_point(alpha)
    ux, uy = barrel_dir(fg, my, mx)

    H, W = alpha.shape
    yy, xx = np.mgrid[0:H, 0:W]
    dx, dy = xx - mx, yy - my
    along = dx * ux + dy * uy                     # 沿炮管向外为正
    perp = -dx * uy + dy * ux
    d = np.sqrt((along / K) ** 2 + perp ** 2)
    glow = (np.clip(1.0 - d / r, 0.0, 1.0) ** 1.5) * strength
    glow[fg] = 0.0                                # 塔体像素一律不参与叠加

    out_a = np.maximum(alpha, glow * 255.0)
    w = (glow * 255.0) / np.maximum(out_a, 1.0)
    out_rgb = src[..., :3] * (1.0 - w[..., None]) + GLOW_RGB * w[..., None]
    out = np.dstack([out_rgb, out_a]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, 'RGBA'), (my, mx, ux, uy)


def main():
    frames = []
    for i in range(N):
        src = os.path.join(IDLE_DIR, f'Tower01_Idle_{i:02d}.png')
        img, mz = make_frame(src, GLOW_R[i], GLOW_A[i])
        os.makedirs(AIM_DIR, exist_ok=True)
        dst = os.path.join(AIM_DIR, f'Tower01_Aim_{i:02d}.png')
        img.save(dst)
        frames.append(img)
        print(f'  [OK] Aim_{i:02d} <- Idle_{i:02d}  炮口=({mz[1]:.0f},{mz[0]:.0f})  轴向=({mz[2]:+.2f},{mz[3]:+.2f})  r={GLOW_R[i]}  a={GLOW_A[i]}')

    # 2x2 sheet（等大直贴，无需缩放）
    cw, ch = CELL
    sheet = Image.new('RGBA', (cw * 2, ch * 2), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, ((i % 2) * cw, (i // 2) * ch))
    sp = os.path.join(SHEETS, 'Tower01_Aim_sheet.png')
    sheet.save(sp)
    # raw 留档：与 sheet 同格的源图（本套 raw 即成品网格本身）
    sheet.save(os.path.join(SHEETS, 'raw', 'Tower01_Aim_raw.png'))
    print(f'  [OK] sheet {sheet.size} -> {os.path.relpath(sp, TOWER)}')


if __name__ == '__main__':
    main()
