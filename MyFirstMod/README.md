# MyFirstMod — Slay the Spire 2 Mod

基于 [Godot 4.5 + C# + 游戏事件 API](https://github.com/Alchyr/sts2-modding) 的杀戮尖塔 2 mod 模板。

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
# 首次构建或修改了 Godot 资源后，必须用 publish 生成 .pck
dotnet publish

# 后续只改 C# 代码时，可以只 build（更快，但不更新 .pck）
dotnet build
```

> **重要：** 游戏只通过 `.pck` 文件发现 mod。第一次部署**必须**用 `dotnet publish`。

## 两个命令的区别

| 命令 | 产物 | 适用场景 |
|---|---|---|
| `dotnet build` | `.dll` + `.json` | 只改了 C# 代码，且 `.pck` 已存在 |
| `dotnet publish` | `.dll` + `.json` + `.pck` | 首次部署、新增或修改了 Godot 资源 |

## 版本管理

只需要修改 **`MyFirstMod.json`** 中的 `version` 字段，构建时会自动完成以下操作：

1. 从 `MyFirstMod.json` 读取 `version`，生成带版本号的输出文件夹（如 `MyFirstMod-v0.1.0/`）
2. 在 `dotnet publish` 时自动生成 `mod_manifest.json`（打包进 `.pck`，供游戏读取）

推荐使用语义化版本（如 `v1.0.0`）。

## 项目结构

```
.
├── MainFile.cs            # mod 入口（初始化、延迟修改游戏模型）
├── MyFirstMod.json        # 唯一的元信息文件（版本号、作者、描述等）
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

- **`MainFile.cs`** — 写 mod 逻辑（游戏模型修改、事件订阅等）
- **`MyFirstMod.json`** — 更新版本号、描述等元信息（`mod_manifest.json` 构建时自动生成）
- **`MyFirstMod/`** — 放 Godot 资源（图片、场景、本地化文件等）

## macOS 路径配置

项目会自动检测操作系统并设置路径。macOS 下的默认值：

| 配置项 | 默认路径 |
|---|---|
| Steam 库 | `~/Library/Application Support/Steam/steamapps` |
| GodotPath | `~/Applications/Godot_mono.app/Contents/MacOS/Godot` |
| 游戏目录 | `<Steam库>/common/Slay the Spire 2` |
| Mods 目录 | `<游戏目录>/SlayTheSpire2.app/Contents/MacOS/mods/` |

如果路径不对，在 `MyFirstMod.csproj` 的 macOS 段落中修改即可。

## 构建流程（自动执行）

1. `dotnet build` → 编译 C# → 生成 `MyFirstMod.dll` → 连同 `MyFirstMod.json` 复制到 `mods/MyFirstMod-<版本号>/`
2. `dotnet publish` → 在 build 基础上额外调用 MegaDot 导出 `.pck` 资源包到同一目录

## 技术栈

- **Godot 4.5 (MegaDot)** — 游戏引擎，mod 资源以 `.pck` 形式加载
- **C# / .NET 9** — mod 代码语言
- **游戏事件 API** — 订阅 `RunManager` 等游戏事件，或通过 `SceneTree.ProcessFrame` 延迟执行
- **反射** — 访问游戏内部私有字段（如地图路径数据）
- **Harmony**（可选）— 运行时方法补丁，但在 ARM64 macOS 上有兼容性限制

---

## 踩坑记录（macOS ARM64）

在 Apple Silicon Mac 上开发 STS2 mod 时总结的经验，节省后来人的调试时间。

### 1. 游戏只通过 `.pck` 文件发现 mod

游戏的 `ModManager` 扫描 `mods/` 目录时 **只查找 `.pck` 文件**，找到 `.pck` 后才检查同名 `.dll`。
仅有 `.dll` 和 `.json` 文件时，mod 对游戏完全不可见。

**解决：** 必须用 `dotnet publish` 生成 `.pck`，不能只用 `dotnet build`。

### 2. 需要 `mod_manifest.json` 打包进 `.pck`

游戏从 `.pck` 内部读取 `res://mod_manifest.json`（字段为 `pck_name`/`name`/`author`/`version`）。
构建脚本会在 `dotnet publish` 时自动从 `MyFirstMod.json` 生成此文件，无需手动维护。

### 3. macOS Mods 路径是 `Contents/MacOS/mods/`

游戏通过 `Path.GetDirectoryName(OS.GetExecutablePath())` + `"mods"` 定位 mod 目录。
macOS 可执行文件在 `Contents/MacOS/` 下，所以实际路径是：

```
SlayTheSpire2.app/Contents/MacOS/mods/
```

原始模板中写的 `Contents/mods/` 是**错误的**。

### 4. Godot 内部 publish 会覆盖 DLL

`dotnet publish` 时，Godot 导出过程会内部再次执行 `dotnet publish`，
可能用 x86_64 架构覆盖之前正确的 AnyCPU DLL。

**解决：** 在 `CopyToModsFolderOnBuild` target 上添加条件 `Condition="'$(IsInnerGodotExport)' != 'true'"`，
防止内部构建覆盖已复制的 DLL。

### 5. Harmony 在 ARM64 上有严重限制

Harmony 的 `PatchFunctions.UpdateWrapper` 在 ARM64 macOS 上对以下方法类型抛出 `NotImplementedException`：

- **属性 getter**（如 `get_CanonicalVars`）
- **async 方法**（返回 `Task` 的虚方法，如 `AfterCombatVictory`）

**解决：** 放弃 Harmony，改用**延迟模型修改**：

```csharp
// 在 Initialize() 中用 SceneTree.ProcessFrame 延迟到下一帧执行
// 此时 ModelDb 已经初始化完毕，可以直接修改 canonical 模型
var tree = (SceneTree)Engine.GetMainLoop();
Action callback = null;
callback = () =>
{
    tree.ProcessFrame -= callback;
    var relic = ModelDb.GetById<RelicModel>(new ModelId("RELIC", "BURNING_BLOOD"));
    relic.DynamicVars.Heal.BaseValue = 100m;
};
tree.ProcessFrame += callback;
```

### 6. Mod 加载后存档会切换到 modded 目录

游戏检测到 mod 加载后，存档路径从 `profile2/` 变为 `modded/profile2/`，
这是游戏的存档隔离机制，防止 mod 修改污染原版存档。移除 mod 后原版存档会恢复。

### 7. 推荐的开发模式

参考 [STS2RouteSuggest](https://github.com/) 等成熟 mod 的做法：

| 方式 | 适用场景 | ARM64 兼容 |
|---|---|---|
| `SceneTree.ProcessFrame` 延迟执行 | 修改游戏模型数据（遗物数值等） | ✅ |
| 游戏事件订阅（`RunManager.Instance.RunStarted +=`） | 响应游戏状态变化 | ✅ |
| 反射访问私有字段 | 读取/修改 UI 内部状态 | ✅ |
| Harmony 补丁简单 void 方法 | hook 简单的非 async 方法 | ⚠️ 部分可用 |
| Harmony 补丁复杂方法 | hook 属性 getter / async Task | ❌ 不可用 |
