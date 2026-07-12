using System;
using System.Reflection;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace InscribeTaiwu.Frontend;

[PluginConfig("InscribeTaiwu.Frontend", "isekai gaycho", "1.0.0.0")]
public sealed class InscribeTaiwuFrontendPlugin : TaiwuRemakePlugin
{
    private const string HarmonyId = "isekai_gaycho.inscribe_taiwu.frontend";
    private Harmony? _harmony;

    public override void Initialize()
    {
        MethodInfo? refreshInscribeButton =
            AccessTools.Method("Game.Views.CharacterMenu.ViewCharacterMenuInfo:RefreshInscribeButton");
        MethodInfo? refreshInteractionButton =
            AccessTools.Method("Game.Views.CharacterMenu.ViewCharacterMenuInfo:RefreshInteractionButton");
        MethodInfo? refreshAll =
            AccessTools.Method("Game.Views.CharacterMenu.ViewCharacterMenuInfo:RefreshAll");
        if (refreshInscribeButton == null && refreshInteractionButton == null && refreshAll == null)
        {
            return;
        }

        _harmony = new Harmony(HarmonyId);
        if (refreshInscribeButton != null)
        {
            _harmony.Patch(
                refreshInscribeButton,
                postfix: new HarmonyMethod(typeof(ViewCharacterMenuInfoRefreshInscribeButtonPatch), nameof(ViewCharacterMenuInfoRefreshInscribeButtonPatch.Postfix)));
        }

        if (refreshInteractionButton != null)
        {
            _harmony.Patch(
                refreshInteractionButton,
                postfix: new HarmonyMethod(typeof(ViewCharacterMenuInfoRefreshInteractionButtonPatch), nameof(ViewCharacterMenuInfoRefreshInteractionButtonPatch.Postfix)));
        }

        if (refreshAll != null)
        {
            _harmony.Patch(
                refreshAll,
                postfix: new HarmonyMethod(typeof(ViewCharacterMenuInfoRefreshAllPatch), nameof(ViewCharacterMenuInfoRefreshAllPatch.Postfix)));
        }
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}

internal static class ViewCharacterMenuInfoRefreshInscribeButtonPatch
{
    private static readonly FieldInfo? CharacterMenuInfoDisplayDataField =
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:_characterMenuInfoDisplayData");

    private static readonly FieldInfo? InscriptionStatusField =
        AccessTools.Field("GameData.Domains.Character.Display.CharacterMenuInfoDisplayData:InscriptionStatus");

    private static readonly FieldInfo? InscribeButtonField =
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:inscribeButton");

    public static void Postfix(object __instance)
    {
        try
        {
            if (!IsCurrentCharacterTaiwu(__instance))
            {
                return;
            }

            object? displayData = CharacterMenuInfoDisplayDataField?.GetValue(__instance);
            if (displayData == null)
            {
                return;
            }

            sbyte inscriptionStatus = (sbyte)(InscriptionStatusField?.GetValue(displayData) ?? (sbyte)-1);
            if (inscriptionStatus < 0)
            {
                return;
            }

            object? inscribeButton = InscribeButtonField?.GetValue(__instance);
            object? gameObject = AccessTools.Property(inscribeButton?.GetType(), "gameObject")?.GetValue(inscribeButton);
            AccessTools.Method(gameObject?.GetType(), "SetActive", new[] { typeof(bool) })?.Invoke(gameObject, new object[] { true });
        }
        catch
        {
            // Keep the original menu refresh intact if a future game update changes these internals.
        }
    }

    private static bool IsCurrentCharacterTaiwu(object viewCharacterMenuInfo)
    {
        object? characterMenu = AccessTools.Property(viewCharacterMenuInfo.GetType(), "CharacterMenu")
            ?.GetValue(viewCharacterMenuInfo);

        return characterMenu != null &&
               (bool)(AccessTools.Property(characterMenu.GetType(), "CurrentCharacterIsTaiwu")
                   ?.GetValue(characterMenu) ?? false);
    }
}

internal static class ViewCharacterMenuInfoRefreshAllPatch
{
    public static void Postfix(object __instance)
    {
        ViewCharacterMenuInfoRefreshInteractionButtonPatch.Postfix(__instance);
        ViewCharacterMenuInfoRefreshInscribeButtonPatch.Postfix(__instance);
    }
}

internal static class ViewCharacterMenuInfoRefreshInteractionButtonPatch
{
    private static readonly FieldInfo? InteractionButtonField =
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:interactionButton");

    private static readonly FieldInfo? CharacterMenuInfoDisplayDataField =
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:_characterMenuInfoDisplayData");

    private static readonly FieldInfo? InscriptionStatusField =
        AccessTools.Field("GameData.Domains.Character.Display.CharacterMenuInfoDisplayData:InscriptionStatus");

    public static void Postfix(object __instance)
    {
        try
        {
            if (!IsCurrentCharacterTaiwu(__instance) || !CanInscribeCurrentCharacter(__instance))
            {
                return;
            }

            object? interactionButton = InteractionButtonField?.GetValue(__instance);
            object? gameObject = AccessTools.Property(interactionButton?.GetType(), "gameObject")?.GetValue(interactionButton);
            AccessTools.Method(gameObject?.GetType(), "SetActive", new[] { typeof(bool) })?.Invoke(gameObject, new object[] { true });
        }
        catch
        {
            // Keep the original menu refresh intact if a future game update changes these internals.
        }
    }

    private static bool IsCurrentCharacterTaiwu(object viewCharacterMenuInfo)
    {
        object? characterMenu = AccessTools.Property(viewCharacterMenuInfo.GetType(), "CharacterMenu")
            ?.GetValue(viewCharacterMenuInfo);

        return characterMenu != null &&
               (bool)(AccessTools.Property(characterMenu.GetType(), "CurrentCharacterIsTaiwu")
                   ?.GetValue(characterMenu) ?? false);
    }

    private static bool CanInscribeCurrentCharacter(object viewCharacterMenuInfo)
    {
        object? displayData = CharacterMenuInfoDisplayDataField?.GetValue(viewCharacterMenuInfo);
        sbyte inscriptionStatus = (sbyte)(InscriptionStatusField?.GetValue(displayData) ?? (sbyte)-1);

        return inscriptionStatus >= 0;
    }
}
