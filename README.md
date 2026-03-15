# MyFirstMod — Slay the Spire 2 Mod

基于 [Godot 4.5 + C# + Harmony](https://github.com/Alchyr/sts2-modding) 的杀戮尖塔 2 mod 模板。

## 前置要求

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Slay the Spire 2](https://store.steampowered.com/app/2868840/Slay_the_Spire_2/)（Steam 版）
- [MegaDot v4.5.1](https://github.com/nicemicro/megadot/releases)（Godot 的定制版本，导出 `.pck` 资源时需要）

## 初始化：自定义 Mod 名称

从模板创建项目后，运行重命名脚本将 `MyFirstMod` 替换为你自己的 mod 名称：

```bash
./rename_mod.sh
```

脚本会交互式地询问新名称、作者和描述，然后自动完成所有文件的重命名和内容替换。

## 快速开始

```bash
# 编译 .dll 并自动复制到游戏 mods 目录
dotnet build

# 编译 .dll + 导出 Godot .pck 资源包，一并复制到 mods 目录
dotnet publish
```

构建成功后，mod 文件会自动复制到游戏的 `mods/MyFirstMod/` 目录下，启动游戏即可加载。

## 两个命令的区别

| 命令 | 产物 | 适用场景 |
|---|---|---|
| `dotnet build` | `.dll` + `.json` | 只改了 C# 代码，不涉及图片/场景等资源 |
| `dotnet publish` | `.dll` + `.json` + `.pck` | 新增或修改了 Godot 资源（图片、场景、材质等） |

## 项目结构

```
.
├── MainFile.cs            # mod 入口，Harmony 补丁在这里初始化
├── MyFirstMod.json        # mod 清单（ID、名称、版本等，游戏靠它识别 mod）
├── MyFirstMod.csproj      # C# 项目配置（依赖、路径、构建流程）
├── MyFirstMod.sln         # VS/Rider 解决方案文件
├── MyFirstMod/
│   └── mod_image.png      # mod 图标（显示在游戏 mod 列表中）
├── project.godot          # Godot 项目文件（导出 .pck 时需要）
├── export_presets.cfg      # Godot 导出预设
├── nuget.config            # NuGet 包存放在本地 packages/ 目录
└── packages/               # NuGet 依赖缓存（自动管理，不用动）
```

## 日常开发你只需关心这几个文件

- **`MainFile.cs`** — 写 mod 逻辑（Harmony 补丁、游戏行为修改等）
- **`MyFirstMod.json`** — 更新版本号、描述等 mod 元信息
- **`MyFirstMod/`** — 放 Godot 资源（图片、场景、本地化文件等）

## macOS 路径配置

项目会自动检测操作系统并设置路径。macOS 下的默认值：

| 配置项 | 默认路径 |
|---|---|
| Steam 库 | `~/Library/Application Support/Steam/steamapps` |
| GodotPath | `~/Applications/Godot_mono.app/Contents/MacOS/Godot` |
| 游戏目录 | `<Steam库>/common/Slay the Spire 2` |
| Mods 目录 | `<游戏目录>/SlayTheSpire2.app/Contents/mods/` |

如果路径不对，在 `MyFirstMod.csproj` 的 macOS 段落中修改即可。

## 构建流程（自动执行）

1. `dotnet build` → 编译 C# → 生成 `MyFirstMod.dll` → 连同 `MyFirstMod.json` 复制到游戏 mods 目录
2. `dotnet publish` → 在 build 基础上额外调用 MegaDot 导出 `.pck` 资源包到 mods 目录

## 技术栈

- **Godot 4.5 (MegaDot)** — 游戏引擎，mod 资源以 `.pck` 形式加载
- **C# / .NET 9** — mod 代码语言
- **Harmony** — 运行时方法补丁框架，用于 hook 游戏逻辑
- **BaseLib** — 杀戮尖塔 2 社区 mod 基础库
