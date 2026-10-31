"""生成净化剂小瓶 sprite（拾取物实物资产）。

用途
----
`Assets/Prefabs/Eco/PurifyPickup.prefab` 原先的 SpriteRenderer 挂的是
`Assets/Settings/WhiteSquare.png`（4×4 纯白像素占位图），玩家看到的
「蓝色小方块」= 白方块 × prefab 的蓝色 tint (0.35, 0.8, 0.95)。
本脚本产出真正可辨识的**瓶子轮廓**，替换占位图。

规格依据
--------
- 画风：ArtStyleGuide §3.1「高清卡通描边，非像素」+ §3.2「统一粗细的深色描边」
- 语义色：ArtStyleGuide §4.3「净化 / 正向反馈 = 亮绿 #7DFC99，不能用于敌人、不能用于警示」
  → 瓶内液体用亮绿，与 fx_purify 特效的绿色同一语义
- 尺寸参照：同类可拾取物 `knowledge_card.png`（509×538 @PPU 1076 = 0.47×0.50u）
- 描边范式：参照 `crate_wood.png`（440×400 @PPU 400）——深棕近黑描边 + 平涂色阶 + 顶部高光
- 命名：ArtStyleGuide §9.2 美术资源用 snake_case（`knowledge_card` / `crate_wood` / `lever_switch_off`）

尺寸与 PPU 的关系（重要）
------------------------
**prefab 的 `m_LocalScale` 必须改成 1.0**（与 BreakableRock / GarbagePile / BreakableReed
等其他道具 prefab 一致；现在的 8.75 是为 4×4 白方块特殊设的）。
改 scale=1.0 后，世界尺寸 = 像素 / PPU。

想让瓶子高 **0.42u**（比 knowledge_card 的 0.50u 略小，避免挡玩家视线）：

    PPU = 像素高 / 0.42

取画布 128×176 px（瓶身瘦高，剪影一眼可辨），PPU = 176 / 0.42 ≈ **419** → 取 **420**，
实际世界高 = 176/420 = 0.4190u，宽 = 128/420 = 0.3048u。

挂载要点（给 A 的说明，见 docstring 末段）
------------------------------------
1. prefab `m_LocalScale` 8.75 → **1.0**
2. prefab `m_Color` (0.35, 0.8, 0.95) → **(1, 1, 1) 白色**（其他道具 prefab 全是白色 tint；
   保留蓝色 tint 会把绿瓶乘成青蓝，颜色全歪）
3. prefab `m_Sprite` → `purify_bottle.png`
4. prefab `CircleCollider2D.m_Radius` 0.08 → **0.45**（scale 1.0 下 0.08 太小够不到）
5. 场景实例 y：L3 高台顶面实测 **y = 3.65**（SecondFloor y=3.477，BoxCollider2D
   offset y=-0.247、size y=0.84 → 顶面 = 3.477-0.247+0.84/2 = 3.65）。
   瓶底要高于 3.65，瓶子高 0.419 → 中心 y ≈ 3.65 + 0.419/2 = **3.86**
   （A 目前改成了 3.8，底边仍埋进地面 0.03u，建议再抬到 3.86）

用法
----
    C:/Software/cutout-venv/Scripts/python.exe Tools/art-pipeline/make_purify_bottle.py
"""
from PIL import Image, ImageDraw, ImageFilter
import os

# ---- 画布与 PPU ----------------------------------------------------------
W, H = 128, 176
PPU = 420
OUT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "Assets", "Art", "Art", "Environment", "Props", "purify_bottle.png",
)

SS = 3  # 超采样倍数，最后 LANCZOS 缩回，提升边缘质量
CW, CH = W * SS, H * SS

# ---- 色板（ArtStyleGuide §4.3 语义色 + crate_wood 的描边/色阶范式）--------
OUTLINE = (34, 30, 22, 255)        # 深棕近黑描边
GLASS = (222, 238, 232, 255)       # 玻璃主体（微青白）
GLASS_DK = (176, 203, 196, 255)    # 玻璃暗部
GLASS_HI = (250, 255, 253, 255)    # 玻璃高光（最亮，短促）
GLASS_HI2 = (240, 249, 245, 255)   # 玻璃高光（柔，用于主反光带）
LIQ = (125, 252, 153, 255)         # 液体亮绿 = 语义色 #7DFC99
LIQ_DK = (66, 190, 106, 255)       # 液体暗部
LIQ_HI = (180, 255, 205, 255)      # 液面高光
CAP = (112, 126, 120, 255)         # 瓶盖（工业灰绿）
CAP_DK = (72, 86, 80, 255)
CAP_HI = (152, 166, 160, 255)
LABEL = (240, 243, 238, 255)       # 标签纸
LABEL_DK = (202, 211, 203, 255)

img = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

M = 5 * SS            # 画布边距
CAP_W = 62 * SS       # 瓶盖宽
CAP_H = 24 * SS       # 瓶盖高
NECK_W = 40 * SS      # 瓶颈宽
NECK_H = 26 * SS      # 瓶颈高（瓶盖下到瓶肩起）
BODY_TOP = M + CAP_H + NECK_H
BODY_BOT = CH - M
BODY_L = M
BODY_R = CW - M
BR = 8 * SS           # 瓶底两角圆角（小：瓶子底部应是近平底，不该收成八边形）

CX = CW / 2

cap_l = CX - CAP_W / 2
cap_r = CX + CAP_W / 2
cap_t = M
cap_b = M + CAP_H

neck_l = CX - NECK_W / 2
neck_r = CX + NECK_W / 2
neck_t = cap_b
neck_b = BODY_TOP

SHOULDER_H = 30 * SS   # 瓶肩纵向跨度
shoulder_y = BODY_TOP + SHOULDER_H


def smoothstep(t):
    """smoothstep：两端斜率为 0，用来做瓶肩的 S 形外扩（不是直线斜切）。"""
    return t * t * (3.0 - 2.0 * t)


def shoulder_curve(x_root, x_body, y_t0, y_t1, steps=14):
    """瓶肩曲线采样点。

    从瓶颈根 (x_root, y_t0) 平滑外扩到瓶身边 (x_body, y_t1)，
    用 smoothstep 使根部与瓶身都是水平切线 → 视觉上是圆润的收肩，不是尖角。
    外轮廓与内胆各调一次（内胆参数整体内收），保证描边等宽。
    """
    pts = []
    for i in range(steps + 1):
        t = i / steps
        x = x_root + (x_body - x_root) * smoothstep(t)
        y = y_t0 + (y_t1 - y_t0) * t
        pts.append((x, y))
    return pts


def shoulder_points(side):
    """按左右方向返回 (外轮廓曲线, 内胆曲线)。side=+1 右，-1 左。"""
    if side > 0:
        outer = shoulder_curve(neck_r, BODY_R, neck_b, shoulder_y)
        inner = shoulder_curve(neck_r - 4 * SS, BODY_R - 4 * SS,
                               neck_b - 2 * SS, shoulder_y + 2 * SS)
    else:
        outer = shoulder_curve(neck_l, BODY_L, neck_b, shoulder_y)
        inner = shoulder_curve(neck_l + 4 * SS, BODY_L + 4 * SS,
                               neck_b - 2 * SS, shoulder_y + 2 * SS)
    return outer, inner


SH_R_OUT, SH_R_IN = shoulder_points(+1)
SH_L_OUT, SH_L_IN = shoulder_points(-1)


# ---- 整体轮廓（瓶盖 + 瓶颈 + 瓶身一次成型，避免拼接处出现断层/脱节）--------
# 闭合路径：瓶盖顶 → 瓶盖右 → 内收到瓶颈 → 瓶颈右 → 瓶肩曲线（右）→
# 瓶身右 → 瓶底右角 → 瓶底 → 瓶底左角 → 瓶身左 → 瓶肩曲线（左）→
# 瓶颈左 → 内收到瓶盖 → 瓶盖左
silhouette = [
    (cap_l + 8 * SS, cap_t),
    (cap_r - 8 * SS, cap_t),
    (cap_r, cap_t + 8 * SS),
    (cap_r, cap_b),
    (neck_r, cap_b),
    (neck_r, neck_b),
]
silhouette += SH_R_OUT
silhouette += [
    (BODY_R, BODY_BOT - BR),
    (BODY_R - BR, BODY_BOT),
    (BODY_L + BR, BODY_BOT),
    (BODY_L, BODY_BOT - BR),
]
silhouette += list(reversed(SH_L_OUT))
silhouette += [
    (neck_l, cap_b),
    (cap_l, cap_b),
    (cap_l, cap_t + 8 * SS),
]
d.polygon(silhouette, fill=OUTLINE)

# ---- 瓶身内胆（比描边内收）------------------------------------------------
inset = 4 * SS
inner = [
    (cap_l + 8 * SS + 4 * SS, cap_t + 4 * SS),
    (cap_r - 8 * SS - 4 * SS, cap_t + 4 * SS),
    (cap_r - 4 * SS, cap_t + 8 * SS + 4 * SS),
    (cap_r - 4 * SS, cap_b - 4 * SS),
    (neck_r - 4 * SS, cap_b - 4 * SS),
    (neck_r - 4 * SS, neck_b - 2 * SS),
]
inner += SH_R_IN
inner += [
    (BODY_R - 4 * SS, BODY_BOT - BR - 4 * SS),
    (BODY_R - BR - 4 * SS, BODY_BOT - 4 * SS),
    (BODY_L + BR + 4 * SS, BODY_BOT - 4 * SS),
    (BODY_L + 4 * SS, BODY_BOT - BR - 4 * SS),
]
inner += list(reversed(SH_L_IN))
inner += [
    (neck_l + 4 * SS, cap_b - 4 * SS),
    (cap_l + 4 * SS, cap_b - 4 * SS),
    (cap_l + 4 * SS, cap_t + 8 * SS + 4 * SS),
]
d.polygon(inner, fill=GLASS)

# ---- 瓶颈加暗（玻璃在窄处更暗，制造厚度感）------------------------------
d.polygon([
    (neck_l + 4 * SS, neck_b - 4 * SS),
    (neck_r - 4 * SS, neck_b - 4 * SS),
    (neck_r - 4 * SS, neck_b),
    (neck_l + 4 * SS, neck_b),
], fill=GLASS_DK)

# ---- 瓶盖（盖住轮廓顶部，做成有厚度的金属盖）----------------------------
d.rounded_rectangle((cap_l + 4 * SS, cap_t + 4 * SS, cap_r - 4 * SS, cap_b),
                    radius=8 * SS, fill=CAP)
# 瓶盖下缘暗色，把盖子与瓶颈断开
d.rectangle((cap_l + 4 * SS, cap_b - 8 * SS, cap_r - 4 * SS, cap_b), fill=CAP_DK)
# 瓶盖顶部受光
d.rounded_rectangle((cap_l + 10 * SS, cap_t + 5 * SS, cap_r - 10 * SS, cap_t + 12 * SS),
                    radius=3 * SS, fill=CAP_HI)

# ---- 液体（瓶身内，亮绿语义色）-------------------------------------------
liq_top = shoulder_y + 4 * SS
liq_inset = inset + 6 * SS
liq_bot = BODY_BOT - inset
liq_box = (BODY_L + liq_inset, liq_top, BODY_R - liq_inset, liq_bot)
d.rounded_rectangle(liq_box, radius=6 * SS, fill=LIQ)
# 液体暗部：右侧一条窄带（表示瓶身圆柱转折），不铺到 1/3 宽，避免「半绿」硬分割
d.rectangle((CX + 22 * SS, liq_top, BODY_R - liq_inset, liq_bot), fill=LIQ_DK)
# 暗带与亮液交界的柔化：中间插一段过渡色
d.rectangle((CX + 14 * SS, liq_top, CX + 22 * SS, liq_bot), fill=(96, 222, 130, 255))
# 液面
d.rectangle((BODY_L + liq_inset, liq_top, BODY_R - liq_inset, liq_top + 7 * SS),
            fill=LIQ_HI)
# 瓶底积液暗部（压重；一直压到玻璃底缘，避免底下露出浅色横带）
d.rounded_rectangle((BODY_L + liq_inset, liq_bot - 12 * SS,
                     BODY_R - liq_inset, liq_bot),
                    radius=4 * SS, fill=LIQ_DK)

# ---- 玻璃高光（独立图层 + 轻模糊，避免硬边「柱子」感）----------------------
hl = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
hd = ImageDraw.Draw(hl)
# 主反光带：短、柔和（不再从液面拉到瓶底）
hl_x0 = BODY_L + 13 * SS
hd.rounded_rectangle((hl_x0, shoulder_y + 18 * SS, hl_x0 + 10 * SS, BODY_BOT - 44 * SS),
                     radius=5 * SS, fill=GLASS_HI2)
# 一点更亮的小高光，贴在主带右上
hd.rounded_rectangle((hl_x0 + 14 * SS, shoulder_y + 26 * SS, hl_x0 + 18 * SS,
                      shoulder_y + 52 * SS), radius=2 * SS, fill=GLASS_HI)
hl = hl.filter(ImageFilter.GaussianBlur(2 * SS))
img = Image.alpha_composite(img, hl)
d = ImageDraw.Draw(img)

# ---- 标签纸（贴瓶身下部一条窄带，不遮液体主色）--------------------------
# 收窄到瓶身中央 62% 宽；内容用「色块 + 细线」读作产品标签，不做成进度条
lab_t = BODY_BOT - 40 * SS
lab_b = BODY_BOT - 22 * SS
lab_l = CX - 28 * SS
lab_r = CX + 28 * SS
d.rectangle((lab_l, lab_t, lab_r, lab_b), fill=OUTLINE)
d.rectangle((lab_l + 3 * SS, lab_t + 3 * SS, lab_r - 3 * SS, lab_b - 3 * SS), fill=LABEL)
# 标签上的绿色圆角方块（净化语义标识）——全部收在内框（lab_t+3 ~ lab_b-3）内
d.rounded_rectangle((lab_l + 6 * SS, lab_t + 5 * SS, lab_l + 16 * SS, lab_t + 13 * SS),
                    radius=2 * SS, fill=LIQ)
# 标签上的两道细横线（读作文字）
d.rectangle((lab_l + 21 * SS, lab_t + 5 * SS, lab_r - 7 * SS, lab_t + 8 * SS), fill=LABEL_DK)
d.rectangle((lab_l + 21 * SS, lab_t + 10 * SS, lab_r - 13 * SS, lab_t + 13 * SS), fill=LABEL_DK)

out = img.resize((W, H), Image.LANCZOS)

# ---- 柔光晕（让瓶子从工业背景里跳出来，对应 fx_purify 的绿色发光）---------
glow = Image.new("RGBA", (W * 2, H * 2), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse((W * 2 - 152, H * 2 - 196, W * 2 - 44, H * 2 - 88), fill=(125, 252, 153, 60))
gd.ellipse((W * 2 - 132, H * 2 - 176, W * 2 - 64, H * 2 - 108), fill=(125, 252, 153, 85))
glow = glow.filter(ImageFilter.GaussianBlur(10))
glow = glow.resize((W, H), Image.LANCZOS)

canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
canvas = Image.alpha_composite(canvas, glow)
canvas = Image.alpha_composite(canvas, out)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
canvas.save(OUT)
print("written:", OUT)
print("size:", canvas.size, "PPU:", PPU)
print("world (prefab scale 1.0): %.4f x %.4f u" % (W / PPU, H / PPU))
