# 主角约 5000 四边面低模

## 本轮交付

- 最终：**4745 个四边面、4729 个几何顶点、9490 个三角面**。
- 原始 GLB：500000 个三角面。原 Downloads 文件保持不变。
- 检查：边界边 0、非流形边 0、零面积三角面 0、1 套新 UV。
- BaseColor 4096²；ORM、切线空间 Normal 2048²。
- 不是 TRIPO 服务输出：使用本机 Blender 5.1.2 / QuadriFlow，未上传模型或调用付费 API。

## 文件

- `DCC~/Player_Final_5K.blend`：最终可编辑四边面模型，含隐藏高模与打包贴图。
- `DCC~/Player_Retopo_5K.blend`：重拓扑中间文件；交付以 Final 文件为准。
- `DCC~/Player_Source.blend`：导入的原始高模参考。
- `Player_Low_5K.fbx`：供 Unity 导入的低模；在 Blender 中可查看四边面。
- `Player_Low_5K.glb`：便携预览版，格式输出为三角面，不用于保存四边面拓扑。
- `Comparison.png`：高低模相同镜头和灯光的对比。
- `Preview_Wire.png`、`Preview_Face_Wire.png`：全身与脸部线框证据。
- `validation.json`：几何检查、采样误差与未完成项。
- `artifacts.json`：主要产物 SHA256。
- `Work~`：可复现的处理脚本和过程日志；DCC~/Work~ 是 Unity 忽略导入的工作目录。

## 处理方法

在副本上以 0.000001 m 距离焊接，移除 73725 个重复顶点，保留原始高模的 UV 和纹理作为烘焙来源。QuadriFlow 以目标 5200 运行三个种子，选择边界缺陷较少的候选。通过对向边条带局部分割，令孔洞边数变为偶数，再用四边面补洞；不对全身做平滑细分，避免拇指被削弱。投射回高模表面后，修正共线角点，重新展开 UV 并烘焙颜色、ORM 和法线。

底色修正：上一版 DIFFUSE COLOR 烘焙受原材质金属度影响，导致皮肤等区域的底色被压暗。现改用将原始 Base Color 接到 Emission 后烘焙 EMIT，避免 BSDF 对颜色的衰减；重新打包贴图并导出，灯光和曝光不变。修正前版本保留在 `Work~/BeforeColorFix`，采样检查见 `color_validation.json`。

脚本顺序（使用 Blender 后台 `--python`）：`Work~/retopo_player.py` → `Work~/finish_player.py`；最后用系统 Python 执行 `Work~/finalize_delivery.py`。

## 眨眼、表情和关节检查

**当前是静态低模，并非已经完成的可动画角色。**眼睛和嘴主要体现在同一网格的贴图上，没有独立眼球、可闭合眼睑及嘴部解剖循环边；没有骨架、蒙皮或形态键。不能直接通过缩放眼睛或压扁整个眼区制作可信眨眼。

后续需在现有面数预算内重新分配脸部面数，建立眼睑和嘴周循环边，必要时增加独立眼球；然后制作 Blink_L / Blink_R 并检查闭合和穿插。肩、肘、膝目前是自动布线，需在骨骼绑定后通过弯肘、屈膝、抬臂测试再定稿。细节如鞋带、发丝和衣褶主要由烘焙贴图保留，不能当作独立可动部件。

本轮未替换游戏场景/玩家预制体，也未提交或推送 Git。
