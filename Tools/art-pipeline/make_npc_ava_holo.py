#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NPC_Ava (艾娃) 全息投影立绘重画管线
对应指令 [2506d9e1] + ArtStyleGuide §7.4（2026-10-31 主策划裁决）

设计约束（硬约束，不可擅自放宽）：
- 艾娃是光而非肉：呈现必须是「全息投影」，不得出现皮肤血色、腮红、木质/金属实体读感
- 基色锁定 AvatarTint #7CFC98（全息绿）
- 整体 alpha 0.55~0.75（本方案取 0.65），身后景物可透出
- 全息质感至少二：本方案同时做 边缘辉光 / 水平扫描线 / 轻微噪点（三项）
- 无实体描边上的「肉感」高光：高光不产生纯白，统一压为亮绿半透明
- 剪影可保留：复用现有 npc_ava.png 的轮廓与站姿，内部重新着色
- 画风：高清卡通描边，非像素；角色朝右；透明底；脚底基线对齐（§3.1）
- 描边：统一深色，约角色身高 0.8%~1.2%（§3.2）
- 产出：替换 Assets/Art/Art/Environment/Props/npc_ava.png，GUID 与 PPU=485 不变，
  集成装配发布只需重导入，三关 NPC_Ava_* 场景引用零破坏

输入：Assets/Art/Art/Environment/Props/npc_ava.png（现有实体立绘，390×922 RGBA）
输出：同路径覆盖写入（同名同 GUID，由 A 重导入即可）
"""

import os
import sys
import numpy as np
from PIL import Image, ImageFilter

ROOT = "E:/desktop/UnityXiangMu/BiShe/BiShe"
SRC = os.path.join(ROOT, "Assets", "Art", "Art", "Environment", "Props", "npc_ava.png")
DST = SRC  # 同名替换，GUID 与 PPU=485 不变，集成装配发布重导入即可，场景引用零破坏

# 色彩（ArtStyleGuide §4.3 / §7.4）
HOLO_GREEN = np.array([124, 252, 152], dtype=np.float64)   # #7CFC98 全息绿
HOLO_DARK  = np.array([24, 78, 52],   dtype=np.float64)    # 深全息绿（更饱和）
HOLO_BRIGHT= np.array([170, 255, 200], dtype=np.float64)   # 亮绿高光（不纯白）
EDGE       = np.array([34, 30, 22],   dtype=np.float64)    # 深棕近黑描边（§3.2）
GLOW       = np.array([124, 252, 152], dtype=np.float64)   # 边缘辉光色

ALPHA = 0.65          # 整体透明度（0.55~0.75，取 0.65）
SCANLINE_PITCH = 7    # 扫描线间隔（像素）
SCANLINE_ALPHA = 0.25 # 扫描线 alpha（略提高，增强全息感）
NOISE_AMPLITUDE = 12  # 噪点幅度
GREEN_TINT = np.array([0.82, 1.0, 0.85], dtype=np.float64)  # 整体色偏：压红压蓝突绿


def load_rgba(path):
    im = Image.open(path).convert("RGBA")
    return np.asarray(im, dtype=np.float64)


def save_rgba(arr, path):
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    Image.fromarray(arr, "RGBA").save(path)


def gaussian_blur(a, radius):
    """对单通道 float64 数组做高斯模糊（PIL 需要 uint8，这里做近似：用 PIL 模糊 alpha 通道再还原）"""
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "L")
    im = im.filter(ImageFilter.GaussianBlur(radius=radius))
    return np.asarray(im, dtype=np.float64)


def main():
    rgba = load_rgba(SRC)
    h, w, _ = rgba.shape
    rgb = rgba[..., :3]
    a = rgba[..., 3]
    mask = a > 10  # 有内容的区域

    # ---- 灰度（明度结构，保留体积与剪影）-----------------------------------
    gray = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]

    # ---- 中和实体读感：把偏红（腮红/皮肤血色）与偏棕（木质剪贴板）压暗 -
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    reddish = (r > b + 30) & mask                    # 腮红/皮肤血色
    brownish = (r > 120) & (g > 70) & (b < 100) & (r > b + 20) & mask  # 木质/金属实体
    gray[reddish] *= 0.35        # 腮红压暗，不再显血色
    gray[brownish] *= 0.45       # 剪贴板压暗，不再显木质质感

    # ---- 按灰度映射到全息绿连续色阶（暗部深绿→亮部亮绿，整体绿基调）-----
    out_rgb = np.zeros_like(rgb)
    t = np.clip(gray / 255.0, 0, 1)[..., None]            # 0~1 的明度因子
    base_color = HOLO_DARK * (1 - t) + HOLO_GREEN * t     # 连续插值，整体带绿
    high_mask = (gray >= 200) & mask                      # 高光区再往亮绿靠
    base_color[high_mask] = HOLO_BRIGHT
    out_rgb[mask] = base_color[mask]
    # 整体色偏：压红压蓝突绿，让整体呈现明显的全息绿基调（不再灰白）
    out_rgb = out_rgb * GREEN_TINT[None, None, :]

    # ---- 边缘辉光：对 alpha 做模糊，边缘处叠加亮绿发光 ----------------------
    glow = gaussian_blur(a, radius=4)           # 0~255，边缘最亮
    glow = np.clip(glow / 255.0, 0, 1) ** 1.1   # 边缘更宽、更亮
    glow_rgb = GLOW[None, None, :] * (glow[..., None] * 0.7 * 255.0)
    out_rgb = np.where(mask[..., None], out_rgb + glow_rgb, out_rgb)

    # ---- 整体半透明化：alpha = 原 alpha * ALPHA（0.55~0.75）------------------
    out_a = np.clip(a * ALPHA, 0, 255)

    # ---- 扫描线：每隔 SCANLINE_PITCH 行画一条半透明亮绿细线（不留灰白）--------
    for y in range(0, h, SCANLINE_PITCH):
        line = HOLO_BRIGHT[None, :] * SCANLINE_ALPHA   # 亮绿而非纯白
        out_rgb[y, :, :] = np.where(
            mask[y, :, None],
            np.clip(out_rgb[y, :, :] + line[None, :, :] * 255.0, 0, 255),
            out_rgb[y, :, :],
        )
        out_a[y, :] = np.clip(out_a[y, :] + SCANLINE_ALPHA * 40.0, 0, 255)

    # ---- 轻微噪点：在有内容区域叠加 ±NOISE_AMPLITUDE 的随机色阶 --------------
    rng = np.random.default_rng(20261031)
    noise = rng.integers(-NOISE_AMPLITUDE, NOISE_AMPLITUDE + 1, size=(h, w, 3)).astype(np.float64)
    out_rgb = np.where(mask[..., None], np.clip(out_rgb + noise * 0.5, 0, 255), out_rgb)

    # ---- 描边：保留原图深色边缘，统一为深棕近黑，略透明（§3.2）-------------
    dark_edge = (r < 90) & (g < 90) & (b < 90) & (a > 150) & mask
    out_rgb[dark_edge] = EDGE
    out_a[dark_edge] = np.clip(out_a[dark_edge], 0, 210)  # 描边略透，不生硬

    # ---- 把没有内容的区域彻底透明---------------------------------------------
    out_rgb[~mask] = 0
    out_a[~mask] = 0

    out = np.dstack([out_rgb, out_a])
    save_rgba(out, DST)
    print(f"wrote {DST}  size={w}x{h}  alpha_mean={out_a[mask].mean():.1f}  "
          f"opaque_px={int(mask.sum())}")


if __name__ == "__main__":
    main()
