# DamageTracker — Slay the Spire 2 伤害统计 Mod

战斗结束后在屏幕右上角显示伤害统计面板，支持单人和多人模式。

## 功能

- **战斗中实时面板** — 战斗中右上角显示精简伤害数字，每次造成伤害后实时更新
- **战斗后详细汇总** — 战斗结束后切换为详细统计面板，显示本场伤害和击杀
- **累计统计** — 从第二场战斗起，额外显示本局 run 的伤害总计
- **多人排序** — 按伤害从高到低排列，方便比较
- **Steam ID 显示** — 联机模式下自动显示每个玩家的 Steam64 ID，便于识别
- **非侵入式 UI** — 战斗中面板更透明更紧凑；进入下一场战斗时自动隐藏上一场汇总

## 安装

```bash
cd DamageTracker
dotnet publish
```

构建成功后会自动复制到游戏的 `mods/DamageTracker-<版本号>/` 目录（如 `DamageTracker-v0.1.0/`）。重启游戏即可生效。

版本号只需在 `DamageTracker.json` 的 `version` 字段中维护，构建时会自动读取并生成 `mod_manifest.json`。

## 技术实现

| 机制 | 说明 |
|------|------|
| 数据源 | `CombatManager.Instance.History.Entries` 中的 `DamageReceivedEntry` |
| 多人检测 | `RunManager.Instance.IsSinglePlayerOrFakeMultiplayer` 判断联机模式 |
| Steam ID | `Player.NetId`（Steam 传输时为 `SteamUser.GetSteamID().m_SteamID`） |
| 实时更新 | 订阅 `CombatHistory.Changed` 事件，每次伤害后重新聚合 |
| 汇总触发 | 订阅 `CombatManager.CombatEnded` 事件 |
| UI 渲染 | Godot `CanvasLayer` + 双 `PanelContainer`（实时/汇总），代码动态创建 |
| 生命周期 | `CombatSetUp` 隐藏 → 战斗中实时更新 → `CombatEnded` 切换汇总 → `RunStarted` 重置 |

## 版本

- `v0.1.0` — 初始版本，基础伤害统计与 UI 显示

## 后续规划

- [ ] 显示更多维度：格挡伤害、卡牌使用次数、药水使用等
- [ ] 可配置面板位置和透明度
- [ ] 战斗内实时伤害显示（DPS 面板）
- [ ] 导出 run 完整战斗报告
