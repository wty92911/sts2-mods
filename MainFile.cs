using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;

namespace MyFirstMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "MyFirstMod";

    public static void Initialize()
    {
        Log.Info($"[{ModId}] Mod loaded, deferring model patch to next frame...");

        var tree = (SceneTree)Engine.GetMainLoop();
        Action? callback = null;
        callback = () =>
        {
            tree.ProcessFrame -= callback!;
            ApplyModelChanges();
        };
        tree.ProcessFrame += callback;
    }

    static void ApplyModelChanges()
    {
        try
        {
            var burningBlood = ModelDb.GetById<RelicModel>(new ModelId("RELIC", "BURNING_BLOOD"));
            burningBlood.DynamicVars.Heal.BaseValue = 100m;
            Log.Info($"[{ModId}] BurningBlood heal changed to 100 HP.");
        }
        catch (Exception ex)
        {
            Log.Error($"[{ModId}] Failed to patch BurningBlood: {ex.Message}");
        }
    }
}
