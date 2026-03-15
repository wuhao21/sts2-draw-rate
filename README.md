# 杀戮尖塔 2 选牌助手 (STS2 Card Advisor)

基于 [blackpatton17/sts2-draw-rate](https://github.com/blackpatton17/sts2-draw-rate) 开发的增强版选牌推荐 Mod。原版仅显示静态胜率/选取率统计，本版本新增了**流派感知的动态推荐系统**。

## 新增功能

相比原版的主要改进：

- **流派检测** — 实时分析当前牌组，自动识别 15 种流派（格挡流、消耗流、毒流、星辰流、灾厄流等）
- **动态评分** — 综合流派协同（50%）、统计数据（30%）、牌组需求（20%）三维度打分
- **牌组缺陷提示** — 自动检测缺过牌/缺 AOE/缺格挡，并在推荐中体现
- **前期补强** — 牌组较小时自动上调评分，避免前期全部"可跳"
- **血量感知** — 低血量时提升防御牌权重
- **三选一对比** — 全部偏弱时标出"三选一最优"
- **模拟选牌** — 评分基于"选了这张牌之后"的牌组状态，避免选牌后分数跳变
- **智能过滤** — 战斗中/诅咒牌/状态牌/基础牌自动隐藏评分框
- **商店感知** — 商店中考虑性价比
- **WoW 品质色** — 橙/紫/蓝/绿/灰五档直观区分

## 评级说明

| 颜色 | 星级 | 含义 |
|------|------|------|
| 🟠 橙 | ★★★★★ | 极力推荐 |
| 🟣 紫 | ★★★★☆ | 推荐 |
| 🔵 蓝 | ★★★☆☆ | 可选 |
| 🟢 绿 | ★★☆☆☆ | 可跳 |
| ⚪ 灰 | ★☆☆☆☆ | 不推荐 |

## 支持的流派

| 角色 | 流派 |
|------|------|
| 铁甲战士 | 格挡流、消耗流、力量流、烧血流 |
| 静默猎手 | 毒流、弃牌/Sly流、飞刀流 |
| 储君 | 星辰流、铸剑流 |
| 缚灵师 | 灾厄流、奥斯提流、灵魂流 |
| 缺陷体 | 利爪流、闪电球流、冰球流、暗球流 |

## 安装

### 前置条件
- 《杀戮尖塔 2》Steam 版，切换到 **Public Beta** 分支
- 游戏内启用 Mod 加载（首次会弹出确认窗口）

### 安装步骤

1. 从 [Releases](../../releases) 下载最新版本
2. 解压到游戏的 `mods` 目录：
   - **Windows**: `Slay the Spire 2/mods/CardProbMod/`
   - **macOS**: `SlayTheSpire2.app/Contents/MacOS/mods/CardProbMod/`
3. 目录结构：
   ```
   mods/
   └── CardProbMod/
       ├── CardProbMod.dll
       ├── CardProbMod.json
       └── result_cleaned.csv
   ```
4. 启动游戏，在选牌/商店界面即可看到推荐

> **注意**: 不需要 BaseLib，游戏原生支持 Mod 加载。

## 开发

```bash
# 需要 .NET 9.0 SDK
# lib/ 目录下需要 0Harmony.dll, GodotSharp.dll, sts2.dll

# 编译
dotnet build Sts2Mod.csproj -c Release

# macOS 一键编译部署
./deploy.sh
```

## 致谢

- [blackpatton17/sts2-draw-rate](https://github.com/blackpatton17/sts2-draw-rate) — 原版胜率显示 Mod
- [ptrlrd/spire-codex](https://github.com/ptrlrd/spire-codex) — STS2 卡牌数据库
- 小黑盒社区 — 胜率/选取率统计数据来源
