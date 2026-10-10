using System;
using System.Reflection;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace InscribeTaiwu.Frontend;

[PluginConfig("InscribeTaiwu.Frontend", "isekai gaycho", "1.0.1.0")]
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

/// <summary>
/// 角色信息页里那些「互动按钮」（交谈 / 铭刻 / 齿轮同伴 / 踢出）的反射工具。
/// </summary>
/// <remarks>
/// 1.1.x（实测 1.1.23）把角色信息页的互动按钮整体挪进了
/// <c>ViewCharacterMenuInfo/AnimRoot/ElementsRoot/TopArea/InteractiveButtonRoot</c> 这个容器，
/// 并且 <c>RefreshInteractionButton()</c> 只在「当前角色是太吾的队友 / 特殊同伴」时才把这个容器
/// <c>SetActive(true)</c>（太吾本人不算自己的队友，所以太吾那一页容器是关掉的）。
/// 铭刻按钮 <c>InscribeBtn</c> 就在这个容器下面 —— 只把按钮本身 <c>SetActive(true)</c> 是没用的，
/// 父节点不显示，按钮一样看不见（这就是「铭刻位置变了 / 按钮不见了」的原因）。
/// 所以必须连容器一起打开。
/// 1.0.x 里这个字段叫 <c>interactionButton</c>，1.1.x 改名成 <c>interactiveButtonRoot</c>；
/// 两个名字都试一遍，以后游戏再改名也只是这段失效而不是整块崩。
/// </remarks>
internal static class CharacterMenuInteractionRow
{
    private static readonly FieldInfo?[] ContainerFields =
    {
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:interactiveButtonRoot"),
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:interactionButton"),
    };

    private static readonly FieldInfo? CharacterMenuInfoDisplayDataField =
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:_characterMenuInfoDisplayData");

    private static readonly FieldInfo? InscriptionStatusField =
        AccessTools.Field("GameData.Domains.Character.Display.CharacterMenuInfoDisplayData:InscriptionStatus");

    /// <summary>把互动按钮容器打开（新旧字段名都试）。</summary>
    public static void EnsureVisible(object viewCharacterMenuInfo)
    {
        foreach (FieldInfo? field in ContainerFields)
        {
            SetActive(field?.GetValue(viewCharacterMenuInfo), true);
        }
    }

    /// <summary>对 Component 或 GameObject 都能用的 SetActive。</summary>
    public static void SetActive(object? target, bool active)
    {
        if (target == null)
        {
            return;
        }

        object? gameObject =
            AccessTools.Property(target.GetType(), "gameObject")?.GetValue(target) ?? target;
        AccessTools.Method(gameObject.GetType(), "SetActive", new[] { typeof(bool) })
            ?.Invoke(gameObject, new object[] { active });
    }

    /// <summary>当前查看的角色是不是太吾本人。</summary>
    public static bool IsCurrentCharacterTaiwu(object viewCharacterMenuInfo)
    {
        object? characterMenu = AccessTools.Property(viewCharacterMenuInfo.GetType(), "CharacterMenu")
            ?.GetValue(viewCharacterMenuInfo);

        return characterMenu != null &&
               (bool)(AccessTools.Property(characterMenu.GetType(), "CurrentCharacterIsTaiwu")
                   ?.GetValue(characterMenu) ?? false);
    }

    /// <summary>当前角色是否处于「可以铭刻」的状态（后端 patch 之后太吾也会返回 &gt;= 0）。</summary>
    public static bool CanInscribeCurrentCharacter(object viewCharacterMenuInfo)
    {
        object? displayData = CharacterMenuInfoDisplayDataField?.GetValue(viewCharacterMenuInfo);
        sbyte inscriptionStatus = (sbyte)(InscriptionStatusField?.GetValue(displayData) ?? (sbyte)-1);

        return inscriptionStatus >= 0;
    }
}

internal static class ViewCharacterMenuInfoRefreshInscribeButtonPatch
{
    private static readonly FieldInfo? InscribeButtonField =
        AccessTools.Field("Game.Views.CharacterMenu.ViewCharacterMenuInfo:inscribeButton");

    public static void Postfix(object __instance)
    {
        try
        {
            if (!CharacterMenuInteractionRow.IsCurrentCharacterTaiwu(__instance) ||
                !CharacterMenuInteractionRow.CanInscribeCurrentCharacter(__instance))
            {
                return;
            }

            // 1.1.x：铭刻按钮的父容器 InteractiveButtonRoot 对太吾是关着的，先打开容器，
            // 按钮才会真的出现在「互动按钮」那一排（而不是被父节点吃掉）。
            CharacterMenuInteractionRow.EnsureVisible(__instance);

            object? inscribeButton = InscribeButtonField?.GetValue(__instance);
            CharacterMenuInteractionRow.SetActive(inscribeButton, true);
        }
        catch
        {
            // Keep the original menu refresh intact if a future game update changes these internals.
        }
    }
}

internal static class ViewCharacterMenuInfoRefreshAllPatch
{
    public static void Postfix(object __instance)
    {
        // RefreshAll 会依次调用 RefreshInscribeButton / RefreshInteractionButton，
        // 这里再兜一次，保证最后一次状态一定是「太吾也能看见铭刻按钮」。
        ViewCharacterMenuInfoRefreshInteractionButtonPatch.Postfix(__instance);
        ViewCharacterMenuInfoRefreshInscribeButtonPatch.Postfix(__instance);
    }
}

internal static class ViewCharacterMenuInfoRefreshInteractionButtonPatch
{
    public static void Postfix(object __instance)
    {
        try
        {
            if (!CharacterMenuInteractionRow.IsCurrentCharacterTaiwu(__instance) ||
                !CharacterMenuInteractionRow.CanInscribeCurrentCharacter(__instance))
            {
                return;
            }

            // 游戏把整个互动按钮容器对太吾关掉了，这里替太吾把它打开。
            // 容器里的其它按钮（交谈 / 踢出 / 齿轮同伴）各自会被游戏按身份关掉，
            // 所以打开容器不会多出别的按钮，只会让铭刻按钮露出来。
            CharacterMenuInteractionRow.EnsureVisible(__instance);
        }
        catch
        {
            // Keep the original menu refresh intact if a future game update changes these internals.
        }
    }
}
