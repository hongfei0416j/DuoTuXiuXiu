# DuoTuXiuXiu · 多图秀秀

一款面向 Windows 的桌面拼图工具，参考「美图秀秀 · 拼图」的交互：左侧选版式、中间实时预览、右侧调画布参数，支持 2–100 张任意张数、2000+ 数据驱动版式、无缝拼接与高分辨率导出。

技术栈：**.NET 10 + Avalonia 11 + CommunityToolkit.Mvvm + SkiaSharp**，全自绘浅色皮肤（圆角卡片、自定义标题栏、统一控件样式），不依赖任何收费或闭源组件。

---

## 功能特性

### 图片导入
- 文件对话框、文件夹批量导入、拖拽导入。
- 支持 JPG / PNG / WebP / BMP / GIF / TIFF。
- 自动按 EXIF 方向摆正（手机竖拍照片不会横过来）。
- 超大图按最长边 2400 px 等比缩放，控制内存占用。
- 多张图片**并行解码**，导入时状态栏显示进度。
- 重复文件自动跳过；损坏 / 非图片文件给出友好提示，不会崩溃。
- 导入后按实际张数自动匹配版式。

### 张数与版式
- 支持 **2–100 张**任意张数（张数胶囊 + 下拉可精确选择）。
- 版式库为**数据驱动**：每个版式由一组 0~1 归一化单元格描述，共 **2000+ 个版式**；2–20 张每档 8–22 种，且同形状自动去重。
- 版式类型覆盖：
  - 均匀等分网格（横条 / 竖条 / 2–9 行均分、田字格、九宫格）；
  - 大图 + 小图混排（左/右/上/下/宽幅主图、左列双大图、侧栏大图）；
  - 横竖条分割、横幅混排；
  - 中心大图环绕（5 / 7 / 9 张）；
  - 异形：L 形拼贴、错位拼贴、杂志错落、拍立得留白相框。
- 左栏按张数分组展示、可纵向滚动；缩略图用真实排版引擎绘制，面积明显更大的主图会加深显示；选中态为深色底 + 白色对勾。
- 支持自动匹配、手动指定行数/列数、预设版式切换、恢复自动版式。
- 「随机切换布局」使用洗牌袋算法：保持当前张数不变，一轮内不重复。
- 手动选择的预设版式不会被自动匹配覆盖；张数变化导致版式无法容纳时，自动回退并提示。

### 画布与参数
- 画布尺寸：宽 / 高 px 精确输入，可锁定比例。
- 常用比例：1:1、4:3、3:4、2:3、4:5、3:2、9:16、16:9。
- 外边框（padding）、间距（gap）、圆角：滑块与数值输入框双向联动，实时预览。
- 无缝模式：一键设置 gap = 0 且 padding = 0，关闭后恢复原值。
- 画布背景：预设色板 / 取色器 / 自定义颜色，或选择背景图片，支持恢复原始背景。
- 格子填充方式：填充裁切（Crop）或完整适应（Fit）。

### 编辑与导出
- 拖拽调整图片顺序、单张替换、删除、清空。
- 预览区支持缩放、旋转、水平翻转、填充画布查看。
- 导出 PNG / JPG；分辨率支持 1x / 2x / 3x 或自定义像素宽度；JPG 可调质量。
- 导出后可自动打开所在文件夹。

---

## 构建与运行

前置条件：Windows + [.NET 10 SDK](https://dotnet.microsoft.com/)。

```bat
:: 构建
dotnet build E:\KABUWORK\多图秀秀\DuoTuXiuXiu.sln -c Debug

:: 运行
dotnet run --project E:\KABUWORK\多图秀秀\src\PhotoCollage\DuoTuXiuXiu.csproj
```

可执行文件输出路径：

- Debug：`src\PhotoCollage\bin\Debug\net10.0\DuoTuXiuXiu.exe`
- Release：`src\PhotoCollage\bin\Release\net10.0\DuoTuXiuXiu.exe`

> 构建前请先关闭正在运行的 `DuoTuXiuXiu.exe`，否则 Windows 会锁定输出文件。

---

## 目录结构

```
多图秀秀/
├─ DuoTuXiuXiu.sln
├─ .gitignore
└─ src/PhotoCollage/
   ├─ Program.cs / App.axaml(.cs)      入口与全局异常兜底
   ├─ Models/                          纯数据模型
   │  ├─ LayoutTile.cs                 归一化单元格 + 版式定义
   │  ├─ LayoutSpec.cs / LayoutCell.cs 排版请求与计算结果
   │  ├─ LayoutPreset.cs               左栏可选版式
   │  ├─ CollageScene.cs / CollageImage.cs
   │  └─ CellFillMode.cs 等
   ├─ Services/
   │  ├─ LayoutLibrary.cs              版式库（2000+，数据驱动，扩展只改这里）
   │  ├─ LayoutEngine.cs               排版计算（网格 / 大图 / 横幅 / 错落 / 预设）
   │  ├─ ImageLoader.cs                图片解码（并行、EXIF、直拷像素）
   │  ├─ CollageRenderer.cs            画布绘制
   │  ├─ ExportService.cs              PNG / JPG 导出
   │  ├─ SampleImageFactory.cs         首次启动示例图
   │  └─ CrashLog.cs                   崩溃日志
   ├─ ViewModels/                      MVVM 视图模型
   ├─ Views/                           MainWindow / ErrorDialog
   ├─ Controls/                        CollagePreview / LayoutThumb / ValueSlider
   ├─ Converters/                      绑定转换器
   └─ Styles/                          Palette.axaml / Theme.axaml
```

---

## 扩展版式

所有版式集中在 [LayoutLibrary.cs](src/PhotoCollage/Services/LayoutLibrary.cs)。新增一个版式只需追加一段单元格定义：

```csharp
Add("自定义版式", "说明文字", "asymmetric", new TileBuilder()
    .Add(0, 0, 0.6, 1)          // 左侧大图
    .Grid(0.6, 0, 0.4, 1, 4, 2) // 右侧 4 张，2 列
    .Inset(0.01));              // 可选：留白相框
```

- `Add(x, y, w, h)`：按 0~1 归一化坐标放置单元格；
- `Rows` / `Cols` / `Grid`：按行/列/网格批量生成；
- `Inset`：整体内缩，形成留白/相框效果。

缩略图、实时预览与导出都会自动使用新定义，间距、外边距、圆角在渲染阶段统一叠加，无需修改界面代码。

---

## 性能说明

- 图片解码后直接按行拷贝像素到 Avalonia `WriteableBitmap`，跳过「重新编码 → 再解码」的旧路径。
- 批量导入使用并行解码（并发 2–6，按 CPU 自动调整），解码完成后按原顺序入列表。

实测（8 张 4000×3000 JPG，Release）：

| 路径 | 平均耗时 |
|---|---|
| 旧路径（PNG 中转） | 849 ms/张 |
| 新路径（直拷像素） | 88 ms/张 |
| 新路径 + 并行导入 | 27 ms/张 |

---

## 已知限制

- 预览区的缩放 / 旋转 / 翻转 / 填充画布目前**只影响预览**，不写入导出结果。
- 单张图片暂未开放独立的偏移 / 缩放微调，填充裁切默认居中。
- 图片导入上限 100 张。
- 工程目录仍为 `src/PhotoCollage`（历史原因），与解决方案名 `DuoTuXiuXiu` 不一致，不影响编译与运行。

---

## 第三方依赖

| 依赖 | 许可证 |
|---|---|
| Avalonia | MIT |
| CommunityToolkit.Mvvm | MIT |
| SkiaSharp | MIT |
