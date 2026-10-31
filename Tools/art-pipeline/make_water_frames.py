"""生成 L2 水体动画 6 帧（water_river_00~05.png 重制版）。

背景（为什么重出）
------------------
旧 6 帧是同一 prompt 的 6 个独立 AI 变体（相邻帧像素差恒定 25.95/34.60，
相位检验比值 0.967 找不到任何位移对应关系）→ 逐帧播放必然跳变；
且画面里含背景墙/塑料瓶/石头，违反 U-0011「纯水面」禁令。
主策划已把出图规格写死 ArtStyleGuide §6.7，本脚本按规格程序化生成——
相位动画只有程序化能保证，AI 生成做不到。

规格落地（与 §6.7 的两处差异及理由）
------------------------------------
- 画布 **1920×640**，不是 2048×1024。理由：Simple 模式下 sprite 世界尺寸
  = px/PPU。2048×1024 @PPU128 = 16×8u，会溢出 15×5u 池区（上下各盖 1.5u
  河床/岸）。1920×640 @PPU128 = **正好 15×5u**，A 接线 drawMode 0 后零缩放
  直接落位；像素密度与地面完全一致（同为 128）。若要改回 2048×1024，
  改下面 WIDTH/HEIGHT 两个常量重跑即可。
- PPU 128（与 ground_forest 一致）→ 6 个 .meta 的 spritePixelsToUnits
  需 64→128（本脚本不写 meta，GUID 必须保留给 water_river_flow.anim 引用）。

无缝与循环的数学保证
--------------------
- 左右无缝：所有 x 依赖项的周期是 WIDTH 的整分数（λ = W/k，k 整数）→
  col(W-1) 与 col(0) 是相邻周期点，天然衔接。
- 6 帧循环：所有时间项形如 sin(2π*(t + c))，t = i/6 → 第 6 帧到第 1 帧
  的相位差与帧间相同，循环闭合。
- 构图一致：底色渐变 / 闪点位置 6 帧完全相同（不依赖 t），只有波纹相位
  在动 —— 满足「画面中任何物体像素位置必须完全一致」（纯水面无物体）。

用法
----
    C:/Software/cutout-venv/Scripts/python.exe Tools/art-pipeline/make_water_frames.py
"""
import os
import numpy as np
from PIL import Image, ImageFilter

# ---- 画布 ---------------------------------------------------------------
WIDTH, HEIGHT = 1920, 640
PPU = 128                      # 与 ground_forest 一致；世界尺寸 15×5u
FRAMES = 6
OUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "Assets", "Art", "Art", "Environment", "Tiles", "WaterAnim",
)

# ---- 色板（water_river.png 暗青绿基准，向 ground_forest 黄绿靠半步）------
C_DEEP = np.array([46, 64, 50], dtype=np.float64)      # 远水（顶部，稍亮防死黑）
C_MID = np.array([58, 80, 62], dtype=np.float64)       # 中层
C_SHALLOW = np.array([78, 104, 76], dtype=np.float64)  # 浅水（近）
C_CREST = np.array([111, 143, 114], dtype=np.float64)  # 波峰亮线 #6F8F72
C_SPARK = np.array([176, 204, 168], dtype=np.float64)  # 闪点 #B0CCA8

W, H = WIDTH, HEIGHT
xs = np.arange(W)
ys = np.arange(H)
X, Y = np.meshgrid(xs, ys)          # (H, W)

# ---- 波长（全部取 W 的整分数 → 左右无缝）---------------------------------
LAM1 = W / 6                        # 320px 大涌浪
LAM2 = W / 14                       # ~137px 中波
LAM3 = W / 29                       # ~66px 细碎波

# 时间相位（帧 i → t = i/6），各层速度系数为整数 → 6 帧后全部回到起点
K1, K2, K3 = 1, 2, 3                # 每帧相位推进 = k/6 圈


def base_field():
    """底色层（不随帧变化）：分段垂直渐变 + 固定的暗色深水斑块（构图的一部分）。"""
    v = Y / H
    # 分段渐变：顶(远) C_DEEP → 0.45 处 C_MID → 底(近) C_SHALLOW
    col = np.empty((H, W, 3), dtype=np.float64)
    for c in range(3):
        col[..., c] = np.interp(v, [0.0, 0.45, 1.0],
                                [C_DEEP[c], C_MID[c], C_SHALLOW[c]])
    # 固定斑块：用与时间无关的周期函数造不规则深浅水区（同一构图每帧一致）
    patch = (12 * np.sin(2 * np.pi * X / (W / 5) + 3 * v * np.pi)
             + 9 * np.sin(2 * np.pi * X / (W / 9) - 2 * v * np.pi))
    col += patch[..., None] * np.array([0.10, 0.14, 0.10])
    return col


BASE = base_field()


def wave_field(t):
    """波纹位移场（随帧变化）：三组不同尺度/速度的正弦波叠加。

    振幅沿 y 有包络：中部波大、上下边缘衰减（避免波线被画布边缘截断）。
    """
    env = 0.22 + 0.78 * np.exp(-((Y / H - 0.52) ** 2) / 0.10)   # 波包络（底值 0.22 防顶部平坦死色）
    # 每层加 y 相位斜率 → 波峰线是斜向波浪线（真实水面形态），斜率按
    # y_rad_per_px / (2π/λ) 折算：L1≈22°、L2 反向≈15°、L3≈12°，三层交织
    w1 = 6.0 * np.sin(2 * np.pi * X / LAM1 + 0.008 * Y + 2 * np.pi * t * K1 + 1.3)
    w2 = 3.2 * np.sin(2 * np.pi * X / LAM2 - 0.012 * Y + 2 * np.pi * t * K2 + 0.4)
    w3 = 1.6 * np.sin(2 * np.pi * X / LAM3 + 0.020 * Y + 2 * np.pi * t * K3 + 2.2)
    return (w1 + w2 + w3) * env


def crest_mask(wf):
    """波峰亮线：波场正值高位区（沿 x 的斜向波浪形亮带）。"""
    hi = 7.5
    top = 10.8
    s = (wf - hi) / (top - hi)
    s = np.clip(s, 0, 1)
    return s ** 1.4


SPARK_N = 90
rng = np.random.default_rng(20261031)                    # 固定种子 → 每帧位置一致
spark_x = rng.integers(8, W - 16, SPARK_N)
spark_y = rng.integers(40, H - 40, SPARK_N)
spark_len = rng.integers(6, 22, SPARK_N)
spark_off = rng.random(SPARK_N)                          # 闪烁相位错开


def spark_layer(t):
    """闪点层：位置固定、亮度 sin 脉动（周期 1 圈 → 6 帧循环）。"""
    layer = np.zeros((H, W), dtype=np.float64)
    amp = np.clip(np.sin(2 * np.pi * (t + spark_off)), 0, 1) ** 3
    for j in range(SPARK_N):
        if amp[j] < 0.04:
            continue
        x0, y0, ln = spark_x[j], spark_y[j], spark_len[j]
        layer[y0, x0:x0 + ln] = np.maximum(layer[y0, x0:x0 + ln], amp[j])
        layer[y0 + 1, x0:x0 + ln - 3] = np.maximum(
            layer[y0 + 1, x0:x0 + ln - 3], amp[j] * 0.7)
    return layer[..., None] * C_SPARK


def render_frame(i):
    t = i / FRAMES
    wf = wave_field(t)
    cm = crest_mask(wf)

    img = BASE + wf[..., None] * np.array([0.55, 0.62, 0.55])   # 波谷暗 / 波峰亮
    img = img + cm[..., None] * (C_CREST - img) * 0.6           # 波峰亮线（水平波浪形）
    img = img + spark_layer(t)                                   # 闪点

    # 风格化：轻模糊 → 色阶化（平涂感，贴合项目非写实画风）
    rgb8 = np.clip(img, 0, 255).astype(np.uint8)
    im = Image.fromarray(rgb8, "RGB").convert("RGBA")
    im = im.filter(ImageFilter.GaussianBlur(0.7))
    arr = np.asarray(im).astype(np.float64)[..., :3]
    levels = 22
    arr = np.round(arr / 255 * (levels - 1)) / (levels - 1) * 255

    out = np.zeros((H, W, 4), dtype=np.uint8)
    out[..., :3] = np.clip(arr, 0, 255).astype(np.uint8)
    out[..., 3] = 255
    return Image.fromarray(out, "RGBA")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    frames = []
    for i in range(FRAMES):
        im = render_frame(i)
        p = os.path.join(OUT_DIR, "water_river_%02d.png" % i)
        im.save(p)
        frames.append(np.asarray(im, dtype=np.int16))
        print("written:", p)

    # ---- 自检 1：左右无缝（首末列差异应与相邻列梯度同量级）--------------
    a0 = frames[0].astype(np.float64)
    edge_gap = np.abs(a0[:, 0] - a0[:, W - 1]).mean()
    inner_gap = np.abs(a0[:, 1:] - a0[:, :-1]).mean()
    print("自检1 左右无缝: 首末列差 %.2f vs 内部相邻列均差 %.2f (比值应 < 3)" % (edge_gap, inner_gap))

    # ---- 自检 2：首尾循环（帧5→0 的差应与帧0→1 同量级）------------------
    d_loop = np.abs(frames[5] - frames[0]).mean()
    d_step = np.abs(frames[0] - frames[1]).mean()
    print("自检2 首尾循环: 帧5→帧0 差 %.2f vs 帧0→帧1 差 %.2f (比值应 ≈ 1)" % (d_loop, d_step))

    # ---- 自检 3：构图一致（底色层帧间差=0，闪点位置一致）----------------
    # 波纹区外取顶部 30 行（包络≈0、无闪点），帧间差应为 0
    d_static = max(np.abs(frames[k][:30] - frames[0][:30]).mean() for k in range(1, FRAMES))
    print("自检3 构图一致: 顶部静区帧间最大差 %.4f (应 = 0)" % d_static)

    print("PPU:", PPU, " 世界尺寸: %.2f x %.2f u" % (W / PPU, H / PPU))


if __name__ == "__main__":
    main()
