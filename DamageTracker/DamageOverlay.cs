using System.Collections.Generic;
using Godot;

namespace DamageTracker;

/// <summary>
/// Two-mode overlay: a compact live panel during combat, and a detailed summary panel afterwards.
/// Multiplayer: "SteamName · Class"; Single player: just class name.
/// </summary>
public partial class DamageOverlay : CanvasLayer
{
    static readonly Color TitleColor = new(0.95f, 0.8f, 0.3f);
    static readonly Color SectionColor = new(0.6f, 0.6f, 0.65f);
    static readonly Color NameColor = Colors.White;
    static readonly Color LiveNameColor = new(0.82f, 0.82f, 0.88f);
    static readonly Color DmgColor = new(1f, 0.4f, 0.35f);
    static readonly Color DmgCumulativeColor = new(1f, 0.65f, 0.25f);
    static readonly Color KillColor = new(0.55f, 0.55f, 0.6f);

    readonly PanelContainer _livePanel;
    readonly VBoxContainer _liveContent;
    readonly PanelContainer _summaryPanel;
    readonly VBoxContainer _summaryContent;

    public DamageOverlay()
    {
        Layer = 10;

        _livePanel = BuildPanel(
            out _liveContent,
            bgAlpha: 0.65f,
            cornerRadius: 4,
            margin: 10,
            borderColor: new Color(0.5f, 0.5f, 0.55f, 0.3f),
            separation: 2);

        _summaryPanel = BuildPanel(
            out _summaryContent,
            bgAlpha: 0.88f,
            cornerRadius: 6,
            margin: 14,
            borderColor: new Color(0.9f, 0.75f, 0.3f, 0.5f),
            separation: 4);

        AddChild(_livePanel);
        AddChild(_summaryPanel);
    }

    // ── Public API ──────────────────────────────────────────

    public void ShowLive(List<PlayerCombatStats> stats, bool multiplayer)
    {
        Rebuild(_liveContent, () =>
        {
            foreach (var s in stats)
            {
                var row = MakeRow(8);
                string label = FormatPlayerName(s.DisplayName, s.CharacterClass, multiplayer);
                var name = MakeLabel(label, 13, LiveNameColor);
                name.CustomMinimumSize = new Vector2(multiplayer ? 160 : 90, 0);
                row.AddChild(name);
                row.AddChild(MakeLabel($"{s.Damage:N0}", 13, DmgColor));
                if (s.Kills > 0)
                    row.AddChild(MakeLabel($"💀{s.Kills}", 11, KillColor));
                _liveContent.AddChild(row);
            }
        });

        _livePanel.Visible = true;
        _summaryPanel.Visible = false;
    }

    public void ShowSummary(
        List<PlayerCombatStats> combat,
        List<PlayerRunStats> run,
        bool multiplayer)
    {
        Rebuild(_summaryContent, () =>
        {
            AddLabelTo(_summaryContent, "⚔ 伤害统计", 18, TitleColor);
            AddSepTo(_summaryContent);

            if (combat.Count > 0)
            {
                AddLabelTo(_summaryContent, "本场战斗", 13, SectionColor);
                foreach (var s in combat)
                {
                    string name = FormatPlayerName(s.DisplayName, s.CharacterClass, multiplayer);
                    AddPlayerRow(_summaryContent, name, s.Damage, s.Kills, false);
                }
            }

            int totalCombats = run.Count > 0 ? run[0].Combats : 0;
            if (totalCombats > 0)
            {
                if (combat.Count > 0) AddSepTo(_summaryContent);
                string header = totalCombats == 1 ? "累计 (1 场)" : $"累计 ({totalCombats} 场)";
                AddLabelTo(_summaryContent, header, 13, SectionColor);
                foreach (var s in run)
                {
                    string name = FormatPlayerName(s.DisplayName, s.CharacterClass, multiplayer);
                    AddPlayerRow(_summaryContent, name, s.Damage, s.Kills, true);
                }
            }
        });

        _livePanel.Visible = false;
        _summaryPanel.Visible = true;
    }

    public void HideAll()
    {
        _livePanel.Visible = false;
        _summaryPanel.Visible = false;
    }

    // ── Display helpers ─────────────────────────────────────

    static string FormatPlayerName(string displayName, string charClass, bool multiplayer)
    {
        return multiplayer ? $"{displayName} · {charClass}" : charClass;
    }

    void AddPlayerRow(VBoxContainer target, string name, int damage, int kills, bool cumulative)
    {
        var row = MakeRow(10);

        var nameLabel = MakeLabel(name, 15, NameColor);
        nameLabel.CustomMinimumSize = new Vector2(110, 0);
        row.AddChild(nameLabel);

        row.AddChild(MakeLabel(
            $"{damage:N0}", 15,
            cumulative ? DmgCumulativeColor : DmgColor));

        if (kills > 0)
            row.AddChild(MakeLabel($"💀{kills}", 13, KillColor));

        target.AddChild(row);
    }

    // ── Panel factory ───────────────────────────────────────

    static PanelContainer BuildPanel(
        out VBoxContainer content,
        float bgAlpha,
        int cornerRadius,
        int margin,
        Color borderColor,
        int separation)
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 1.0f;
        panel.AnchorTop = 0.0f;
        panel.AnchorRight = 1.0f;
        panel.AnchorBottom = 0.0f;
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.End;
        panel.OffsetTop = 90;
        panel.OffsetRight = -16;

        var bg = new StyleBoxFlat();
        bg.BgColor = new Color(0.07f, 0.07f, 0.11f, bgAlpha);
        bg.SetCornerRadiusAll(cornerRadius);
        bg.ContentMarginLeft = margin;
        bg.ContentMarginRight = margin;
        bg.ContentMarginTop = margin;
        bg.ContentMarginBottom = margin;
        bg.BorderColor = borderColor;
        bg.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", bg);

        content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", separation);
        panel.AddChild(content);

        panel.Visible = false;
        return panel;
    }

    // ── Shared helpers ──────────────────────────────────────

    static void Rebuild(VBoxContainer container, System.Action build)
    {
        foreach (var c in container.GetChildren())
        {
            container.RemoveChild(c);
            c.QueueFree();
        }
        build();
    }

    static void AddLabelTo(VBoxContainer target, string text, int size, Color color) =>
        target.AddChild(MakeLabel(text, size, color));

    static void AddSepTo(VBoxContainer target)
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        target.AddChild(sep);
    }

    static HBoxContainer MakeRow(int spacing)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", spacing);
        return row;
    }

    static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }
}
