using System.Reflection;
using GameData.Domains;
using GameData.Domains.Global.Inscription;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;

namespace InscribeTaiwu.Backend;

[PluginConfig("InscribeTaiwu.Backend", "isekai gaycho", "1.0.0.0")]
public sealed class InscribeTaiwuBackendPlugin : TaiwuRemakePlugin
{
    private const string HarmonyId = "isekai_gaycho.inscribe_taiwu.backend";
    private Harmony? _harmony;

    public override void Initialize()
    {
        MethodInfo? getInscriptionStatus =
            AccessTools.Method("GameData.Domains.Character.CharacterDomain:GetInscriptionStatus");
        if (getInscriptionStatus == null)
        {
            return;
        }

        _harmony = new Harmony(HarmonyId);
        _harmony.Patch(
            getInscriptionStatus,
            postfix: new HarmonyMethod(typeof(CharacterDomainGetInscriptionStatusPatch), nameof(CharacterDomainGetInscriptionStatusPatch.Postfix)));
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }
}

internal static class CharacterDomainGetInscriptionStatusPatch
{
    public static void Postfix(int charId, ref sbyte __result)
    {
        if (charId != DomainManager.Taiwu.GetTaiwuCharId())
        {
            return;
        }

        uint worldId = DomainManager.World.GetWorldId();
        var key = new InscribedCharacterKey(worldId, charId);
        __result = DomainManager.Global.IsCharacterInscribed(key) ? (sbyte)2 : (sbyte)0;
    }
}
