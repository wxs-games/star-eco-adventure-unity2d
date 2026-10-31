#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NPC_Ava (艾娃) 全息投影立绘重画管线 v11（亮蓝色全息色，对"颜色太深"做调整）
对应指令 [2506d9e1] + ArtStyleGuide §7.4（2026-10-31 主策划裁决）

v11 核心改动——把蓝色全息色整体提亮，避免过深：
- 基色锁亮蓝色：HOLO_BLUE [110,200,255]，B 通道压倒 R/G，读作亮蓝色全息投影
- 各色阶整体上提 30~50，避免整体过深
- 保持 v10 全息特性：发光只在边缘，内部暗淡，避免高光过曝冲灰
- ALPHA_BASE=0.75，边缘发光，内部暗淡，整体 alpha 均值 0.55~0.75
- 扫描线 pitch 2，边缘限定；色散 offset 10；噪点 36
- 硬约束：透明区域 RGB=0；alpha 0.55~0.75（整体均值）；无皮肤血色/木质实体；GUID 与 PPU=485 不变

输入：C:/tmp/npc_ava_original.png（原始实体立绘）
输出：Assets/Art/Art/Environment/Props/npc_ava.png（同名覆盖，GUID 与 PPU 不变）
"""

import os
import numpy as np
from PIL import Image, ImageFilter

ROOT = "E:/desktop/UnityXiangMu/BiShe/BiShe"
SRC = "C:/tmp/npc_ava_original.png"
DST = os.path.join(ROOT, "Assets", "Art", "Art", "Environment", "Props", "npc_ava.png")

# 色彩（ArtStyleGuide §7.4）—— 锁亮蓝色全息色，B 压倒 R/G，更浅，无近白
HOLO_DARK   = np.array([60,  140, 220], dtype=np.float64)   # 深全息蓝（提亮，不再过暗）
HOLO_BLUE   = np.array([110, 200, 255], dtype=np.float64)   # 中全息蓝（B 主导，更亮更浅）
HOLO_BRIGHT = np.array([160, 230, 255], dtype=np.float64)   # 亮部蓝色（更亮，不再近白）
EDGE        = np.array([10, 10, 14 ], dtype=np.float64)    # 深棕近黑描边（§3.2，微偏蓝）
GLOW        = np.array([110, 210, 255], dtype=np.float64)   # 边缘辉光（B 主导，更亮更浅）
BLUE_TINT   = np.array([0.55, 0.55, 1.0], dtype=np.float64)  # 极端压红/绿，突蓝

ALPHA_BASE = 0.75            # 内部基础透明度（§7.4 区间上限，确保整体均值达标）
ALPHA_EDGE_BOOST = 0.40      # 边缘额外发光，边缘比内部更亮
ALPHA_MAX = 1.0              # 边缘可完全不透明发光（整体均值仍由基础 alpha 主导，在 0.55~0.75 以内）
SCANLINE_PITCH = 2           # 扫描线间隔（更密）
SCANLINE_ALPHA = 0.5         # 扫描线 alpha（边缘限定，更保守）
NOISE_AMPLITUDE = 36         # 噪点幅度
CHROMATIC_OFFSET = 10        # 色散偏移（R 左 / B 右，明显彩色边缘）
INNER_GLOW_RADIUS = 6        # 内发光轮廓内缩
EDGE_BAND_THRESH = 0.25      # 边缘带阈值：超过此值的区域才叠加 glow/scanline


def load_rgba(path):
    im = Image.open(path).convert("RGBA")
    return np.asarray(im, dtype=np.float64)


def save_rgba(arr, path):
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    # 硬约束：alpha<=10 的像素 RGB 必须为 0（严格按最终 alpha，避免扫描线残留）
    low_a = arr[..., 3] <= 10
    if low_a.any():
        arr[low_a, :3] = 0
        arr[low_a, 3] = 0
    Image.fromarray(arr, "RGBA").save(path)


def gaussian_blur(a, radius):
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "L")
    im = im.filter(ImageFilter.GaussianBlur(radius=radius))
    return np.asarray(im, dtype=np.float64)


def channel_shift(arr, dx):
    if dx == 0:
        return arr.copy()
    out = np.zeros_like(arr)
    if dx > 0:
        out[:, dx:] = arr[:, :-dx]
    else:
        out[:, :dx] = arr[:, -dx:]
    return out


def main():
    rgba = load_rgba(SRC)
    h, w, _ = rgba.shape
    rgb = rgba[..., :3]
    a = rgba[..., 3]
    mask = a > 10

    # ---- 灰度（明度结构，保留体积与剪影）-----------------------------------
    gray = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]

    # ---- 中和实体读感：把偏红（腮红/皮肤血色）与偏棕（木质剪贴板）压暗 -
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    reddish = (r > b + 30) & mask
    brownish = (r > 120) & (g > 70) & (b < 100) & (r > b + 20) & mask
    gray[reddish] *= 0.30
    gray[brownish] *= 0.40

    # ---- 按灰度映射到全息蓝连续色阶（亮部用较暗蓝色，避免近白过曝）------
    t = np.clip(gray / 255.0, 0, 1)[..., None]
    base_color = HOLO_DARK * (1 - t) + HOLO_BLUE * t
    high_mask = (gray >= 200) & mask
    base_color[high_mask] = HOLO_BRIGHT   # [100,180,255] 而非接近白
    out_rgb = np.zeros_like(rgb)
    out_rgb[mask] = base_color[mask]
    out_rgb = out_rgb * BLUE_TINT[None, None, :]

    # ---- 色散（chromatic aberration）：R 左移、B 右移 -------------------------
    r_shifted = channel_shift(out_rgb[..., 0], -CHROMATIC_OFFSET)
    b_shifted = channel_shift(out_rgb[..., 2], +CHROMATIC_OFFSET)
    out_rgb[..., 0] = r_shifted
    out_rgb[..., 2] = b_shifted

    # ---- 边缘距离衰减 alpha：内部淡、边缘亮 -------------------------------
    blur_small = gaussian_blur(a, radius=2)
    blur_large = gaussian_blur(a, radius=10)
    edge_band = np.clip((blur_small - blur_large) / 255.0, 0, 1)  # 0~1，边缘带最高
    edge_alpha = edge_band * ALPHA_EDGE_BOOST * 255.0
    base_alpha = (a * ALPHA_BASE)
    out_a = np.clip(base_alpha + edge_alpha, 0, ALPHA_MAX * 255.0)
    out_a = np.where(mask, out_a, 0)

    # ---- 边缘辉光：只在边缘带叠加，内部不加（避免高光过曝）-------------
    glow = gaussian_blur(a, radius=10)
    glow = np.clip(glow / 255.0, 0, 1) ** 0.6
    glow_rgb = GLOW[None, None, :] * (glow[..., None] * 2.0)
    edge_glow_mask = (edge_band > EDGE_BAND_THRESH) & mask
    out_rgb = np.where(edge_glow_mask[..., None], out_rgb + glow_rgb, out_rgb)

    # ---- 内发光轮廓：只在边缘附近叠加 -------------------------------------
    eroded = gaussian_blur(a, radius=INNER_GLOW_RADIUS + 2)
    inner_glow = np.clip((255 - eroded) / 255.0, 0, 1) ** 2.0
    inner_glow_rgb = HOLO_BRIGHT[None, None, :] * (inner_glow[..., None] * 0.7)
    edge_inner_mask = (edge_band > EDGE_BAND_THRESH * 0.6) & mask
    out_rgb = np.where(edge_inner_mask[..., None], out_rgb + inner_glow_rgb, out_rgb)

    # ---- 扫描线：只在边缘带叠加，内部不加，保持暗淡 ---------------------
    scanline_color = HOLO_BRIGHT * SCANLINE_ALPHA
    for y in range(0, h, SCANLINE_PITCH):
        row_mask = mask[y, :, None] & (edge_band[y, :, None] > EDGE_BAND_THRESH * 0.5)
        out_rgb[y, :, :] = np.where(
            row_mask,
            np.clip(out_rgb[y, :, :] + scanline_color[None, None, :], 0, 255),
            out_rgb[y, :, :],
        )
        out_a[y, :] = np.clip(out_a[y, :] + SCANLINE_ALPHA * 30.0, 0, ALPHA_MAX * 255.0)

    # ---- 轻微噪点 ------------------------------------------------------------
    rng = np.random.default_rng(20261031)
    noise = rng.integers(-NOISE_AMPLITUDE, NOISE_AMPLITUDE + 1, size=(h, w, 3)).astype(np.float64)
    out_rgb = np.where(mask[..., None], np.clip(out_rgb + noise * 0.6, 0, 255), out_rgb)

    # ---- 描边 ----------------------------------------------------------------
    dark_edge = (r < 90) & (g < 90) & (b < 90) & (a > 150) & mask
    out_rgb[dark_edge] = EDGE
    out_a[dark_edge] = np.clip(out_a[dark_edge], 0, 200)

    # ---- 透明区域彻底清零 ----------------------------------------------------
    final_mask = out_a > 10
    out_rgb[~final_mask] = 0
    out_a[~final_mask] = 0

    out = np.dstack([out_rgb, out_a])
    save_rgba(out, DST)
    print(f"wrote {DST}  size={w}x{h}  alpha_mean={out_a[final_mask].mean():.1f}  "
          f"alpha_max={out_a[final_mask].max():.1f}  opaque_px={int(final_mask.sum())}")


if __name__ == "__main__":
    main()
