#!/bin/bash
set -e

export PATH="$HOME/.dotnet:$PATH"

GAME_DIR="/Users/haowu/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS"
MOD_DIR="$GAME_DIR/mods/CardProbMod"

echo "🔨 编译..."
dotnet build Sts2Mod.csproj -c Release

echo "📦 部署到 $MOD_DIR"
mkdir -p "$MOD_DIR"
cp bin/Release/net9.0/CardProbMod.dll "$MOD_DIR/"
cp result_cleaned.csv "$MOD_DIR/"
cp CardProbMod.json "$MOD_DIR/"

echo "✅ 完成！重启游戏加载 mod。"
