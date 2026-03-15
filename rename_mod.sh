#!/usr/bin/env bash
set -euo pipefail

OLD_NAME="MyFirstMod"
OLD_AUTHOR="MyName"

# ── 交互式输入 ──────────────────────────────────────────────

read -rp "新的 Mod 名称 (仅英文字母/数字，如 CoolMod): " NEW_NAME
if [[ -z "$NEW_NAME" ]]; then
  echo "错误：Mod 名称不能为空" >&2; exit 1
fi
if [[ ! "$NEW_NAME" =~ ^[A-Za-z][A-Za-z0-9]*$ ]]; then
  echo "错误：Mod 名称只能包含英文字母和数字，且以字母开头" >&2; exit 1
fi

read -rp "作者名称 [${OLD_AUTHOR}]: " NEW_AUTHOR
NEW_AUTHOR="${NEW_AUTHOR:-$OLD_AUTHOR}"

read -rp "Mod 描述 [Slay the Spire 2 mod]: " NEW_DESC
NEW_DESC="${NEW_DESC:-Slay the Spire 2 mod}"

echo ""
echo "========================================="
echo "  Mod 名称:  ${OLD_NAME} → ${NEW_NAME}"
echo "  作者:      ${NEW_AUTHOR}"
echo "  描述:      ${NEW_DESC}"
echo "========================================="
echo ""
read -rp "确认执行？(y/N): " CONFIRM
if [[ "$CONFIRM" != "y" && "$CONFIRM" != "Y" ]]; then
  echo "已取消。"; exit 0
fi

# ── 替换文件内容 ─────────────────────────────────────────────

echo "► 替换文件内容中的 ${OLD_NAME} → ${NEW_NAME} ..."

replace_in_file() {
  local file="$1"
  if [[ -f "$file" ]]; then
    sed -i '' "s/${OLD_NAME}/${NEW_NAME}/g" "$file"
    echo "  ✓ $file"
  fi
}

replace_in_file "MainFile.cs"
replace_in_file "${OLD_NAME}.csproj"
replace_in_file "${OLD_NAME}.sln"
replace_in_file "${OLD_NAME}.json"
replace_in_file "project.godot"
replace_in_file "export_presets.cfg"

# 更新 author 和 description
if [[ -f "${OLD_NAME}.json" ]]; then
  sed -i '' "s/\"author\": \"${OLD_AUTHOR}\"/\"author\": \"${NEW_AUTHOR}\"/" "${OLD_NAME}.json"
  sed -i '' "s/\"description\": \".*\"/\"description\": \"${NEW_DESC}\"/" "${OLD_NAME}.json"
  echo "  ✓ ${OLD_NAME}.json (author/description)"
fi

# ── 重命名文件和目录 ─────────────────────────────────────────

echo "► 重命名文件和目录 ..."

rename_if_exists() {
  local src="$1" dst="$2"
  if [[ -e "$src" ]]; then
    mv "$src" "$dst"
    echo "  ✓ $src → $dst"
  fi
}

rename_if_exists "${OLD_NAME}/"                  "${NEW_NAME}/"
rename_if_exists "${OLD_NAME}.csproj"            "${NEW_NAME}.csproj"
rename_if_exists "${OLD_NAME}.sln"               "${NEW_NAME}.sln"
rename_if_exists "${OLD_NAME}.sln.DotSettings"   "${NEW_NAME}.sln.DotSettings"
rename_if_exists "${OLD_NAME}.json"              "${NEW_NAME}.json"

# ── 更新 README ──────────────────────────────────────────────

if [[ -f "README.md" ]]; then
  sed -i '' "s/${OLD_NAME}/${NEW_NAME}/g" "README.md"
  sed -i '' "s/${OLD_AUTHOR}/${NEW_AUTHOR}/g" "README.md"
  echo "  ✓ README.md"
fi

# ── 完成 ─────────────────────────────────────────────────────

echo ""
echo "✅ 重命名完成！"
echo ""
echo "后续步骤："
echo "  1. 用 IDE 重新打开 ${NEW_NAME}.sln"
echo "  2. 运行 dotnet restore 恢复依赖"
echo "  3. 运行 dotnet build 验证编译是否通过"
echo ""
echo "提示：此脚本仅供首次重命名使用。"
echo "      如果需要再次重命名，请手动修改脚本开头的 OLD_NAME 变量。"
