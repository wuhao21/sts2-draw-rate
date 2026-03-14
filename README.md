# Slay the Spire 2 Card Win Rate Mod (选牌助手)

这是一个为《杀戮尖塔 2》(Slay the Spire 2) 打造的原生胜率显示 Mod。
在奖励三选一和商店购买界面，此 Mod 会在卡牌下方动态显示该卡牌的 **胜率、选取率、综合评分及评级推荐**，帮助你做出更优的决策。

> **提示:** 本 Mod 使用基于 Harmony 的原生 C# 注入方式，完美融入游戏 UI，不会在战斗中(手牌/牌库)产生视觉干扰。相比旧版的 OCR 截图方案，性能开销为 0，匹配准确率为 100%。如果你对旧版 OCR 方案感兴趣，请查看 [ocr branch](https://github.com/blackpatton17/sts2-draw-rate/tree/ocr-version)。

## 📸 效果预览
*(可以在这里插入一张游戏内三选一界面的截图)*

## 📥 安装说明

### 前置需求
为了使 Mod 正常工作，你需要确保游戏支持加载第三方 Mod：
1. **BaseLib** (或游戏官方支持的 Mod 加载器)
2. 确保你的游戏版本支持 `.dll` 模组注入。

### 安装步骤

1. **下载 Release 压缩包**
   前往 [Releases 页面](../../releases) 下载最新版本的压缩包。

2. **定位游戏 Mod 文件夹**
   打开你的 Steam 库，右键点击 `Slay the Spire 2` -> `管理` -> `浏览本地文件`。
   在游戏根目录中找到或创建一个名为 `mods` 的文件夹。

3. **解压文件**
   将下载压缩包内名为 `CardProbMod` 的整个文件夹解压到 `mods` 目录下。
   正确的路径结构应该如下所示：
   ```text
   Slay the Spire 2/
   └── mods/
       └── CardProbMod/
           ├── CardProbMod.dll        # 核心逻辑文件
           ├── CardProbMod.json       # Mod 配置文件
           └── result_cleaned.csv     # 胜率数据库文件
   ```

4. **启动游戏**
   正常启动游戏。如果你在结算界面或商店看到卡牌下方浮现出带颜色的胜率信息框，说明安装成功！

## ⚙️ 数据更新与自定义
本 Mod 的核心数据由根目录下的 `result_cleaned.csv` 文件提供。你可以随时用文本编辑器或 Excel 打开它，手动修改卡牌的胜率或添加新卡牌。

- **格式要求:** CSV 的第一列必须是卡牌的 `InternalName` (如 `BurningPact`，区分大小写需采用大驼峰格式)。
- **实时更新:** 修改 CSV 文件并保存后，只需重启游戏即可生效，**无需重新编译**。

## 🛠️ 常见问题 (FAQ)

- **Q: 为什么有些牌显示“数据库缺失”？**
  A: 这说明 `result_cleaned.csv` 中没有记录这张牌的内部 ID。你可以记下灰色提示框中给出的 `InternalName`（如 `ForgottenRitual`），将其添加到 CSV 的第一列并补充相应数据即可。
- **Q: 打击和防御怎么不显示胜率？**
  A: 为了保持界面的整洁，基础的过渡牌（以 `Strike` 和 `Defend` 开头）已经被刻意过滤，不会显示胜率框。
- **Q: 会导致游戏卡顿吗？**
  A: 完全不会。Mod 使用 Godot 引擎原生的对象池生命周期检测（Timer 异步轻量检测），无图形遮罩，极度丝滑。

## 👨‍💻 参与开发
如果你想自行编译此项目：
1. 安装 [.NET 9.0 SDK](https://dotnet.microsoft.com/download)。
2. 克隆本仓库。
3. 确保你的 `lib` 文件夹中引用了正确的 `0Harmony.dll`, `GodotSharp.dll` 和 `sts2.dll`。
4. 运行 `dotnet build Sts2Mod.csproj -c Debug` 即可在 `bin/Debug/net9.0/` 下生成最新的 DLL。
