using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// the fortune envelope's art and the Wuchang's taking of the player (Tools/VFX/fortune, under
// NewSprites/Asesprites/VFX/Fortune and VFX/Wuchang), and their sounds (Sounds/Fortune): fills the
// VfxLibrary with them and gives the level up's two gifts their icons. part of Tools > VFX > Build
// Weapon FX (WeaponFxBuilder), which sets the files up first; it also runs once on its own the
// first time the art is in the project
public static class FortuneFxBuilder
{
    private const string Root = "Assets/### Different Engine/";
    internal const string FortuneArt = Root + "NewSprites/Asesprites/VFX/Fortune/";
    internal const string WuchangArt = Root + "NewSprites/Asesprites/VFX/Wuchang/";
    private const string Sounds = Root + "Sounds/Fortune/";
    private const string Gifts = Root + "Data/UpgradeAssets/Gifts/";

    internal static void Build()
    {
        if (!Directory.Exists(FortuneArt)) return;
        var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(Root + "Resources/VfxLibrary.asset");
        if (library == null) { Debug.LogWarning("Fortune FX: no Resources/VfxLibrary.asset, build the weapon FX first"); return; }

        Sprite[] F(string name) => WeaponFxBuilder.FramesAt(FortuneArt + name + ".aseprite");
        Sprite One(string name) => F(name)[0];

        library.envelope = F("fe_envelope");
        library.envelopeGlow = F("fe_glow");
        library.envelopePillar = F("fe_pillar");
        library.envelopeMarker = F("fe_marker");
        library.envelopeCarry = F("fe_carry");
        library.envelopePickup = F("fe_pickup");

        library.envelopeBig = F("fe_big");
        library.envelopeFlap = F("fe_flap");
        library.envelopeFront = One("fe_front");
        library.aura = One("fe_aura");
        library.rays = One("fe_rays");
        library.mote = F("fe_mote");
        library.burstCommon = F("fe_burst_common");
        library.burstRare = F("fe_burst_rare");
        library.burstLegendary = F("fe_burst_legendary");
        library.scrollRoller = One("fe_roller");
        library.scroll = One("fe_scroll");
        library.rewardSlot = One("fe_slot");
        library.rewardSlotEvolution = F("fe_slot_evo");
        library.banner = One("fe_banner");
        library.coinIcon = F("fe_coin");
        library.peachIcon = F("fe_peach");

        if (Directory.Exists(WuchangArt))
        {
            Sprite[] Wc(string name) => WeaponFxBuilder.FramesAt(WuchangArt + name + ".aseprite");
            library.chainLink = Wc("wc_link");
            library.chainHook = Wc("wc_hook")[0];
            library.soul = Wc("wc_soul");
            library.maw = Wc("wc_maw");
            library.inkWipe = Wc("wc_ink");
        }

        AudioClip Sound(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + name + ".wav");
        library.envelopePickupSound = Sound("fe_pickup");
        library.envelopeIdle = Sound("fe_idle");
        library.envelopeClick = Sound("fe_click");
        library.chargeCommon = Sound("fe_charge_common");
        library.chargeRare = Sound("fe_charge_rare");
        library.chargeLegendary = Sound("fe_charge_legendary");
        library.envelopeShake = Sound("fe_shake");
        library.envelopeTier = Sound("fe_tier");
        library.envelopeOpen = Sound("fe_open");
        library.revealCommon = Sound("fe_reveal_common");
        library.revealRare = Sound("fe_reveal_rare");
        library.revealLegendary = Sound("fe_reveal_legendary");
        library.rewardPop = Sound("fe_reward");
        library.chainSound = Sound("wc_chain");
        library.swallowSound = Sound("wc_swallow");
        EditorUtility.SetDirty(library);

        // the level up's gifts: a string of wen and a peach of immortality
        Gift("Gift_Coins", library.coinIcon);
        Gift("Gift_Heal", library.peachIcon);

        AssetDatabase.SaveAssets();
        Debug.Log("WEAPONFX: the fortune envelope, its opening, the Wuchang's taking and the level up's gifts pointed at their art");
    }

    private static void Gift(string asset, Sprite[] frames)
    {
        var gift = AssetDatabase.LoadAssetAtPath<GiftUpgrade>(Gifts + asset + ".asset");
        if (gift == null || frames == null || frames.Length == 0) return;
        gift.icon = frames[0];
        gift.iconFrames = frames;
        gift.iconFps = 7f;
        EditorUtility.SetDirty(gift);
    }

    // ------------------------------------------------------------------ the first time

    private static string AutoBuiltKey => "FortuneFxBuilder.v2." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void ScheduleAutoBuild()
    {
        if (EditorPrefs.GetBool(AutoBuiltKey)) return;
        EditorApplication.delayCall -= AutoBuild;
        EditorApplication.delayCall += AutoBuild;
    }

    private static void AutoBuild()
    {
        if (EditorPrefs.GetBool(AutoBuiltKey) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuild;
            return;
        }
        if (!File.Exists(FortuneArt + "fe_envelope.aseprite")) return;
        var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(Root + "Resources/VfxLibrary.asset");
        // the art is already set up: only what's been added since (the opening's riser sounds).
        // done once they're in; a sound not imported yet is tried again next time
        if (library != null && library.envelope != null && library.envelope.Length > 0)
        {
            if (library.chargeCommon == null && AssetDatabase.LoadAssetAtPath<AudioClip>(Sounds + "fe_charge_common.wav") != null)
            {
                try { Build(); }
                catch (Exception e) { Debug.LogError("couldn't point the fortune envelope at its new sounds, try Tools > VFX > Build Weapon FX\n" + e); }
            }
            if (library.chargeCommon != null) EditorPrefs.SetBool(AutoBuiltKey, true);
            return;
        }
        EditorPrefs.SetBool(AutoBuiltKey, true);
        try { WeaponFxBuilder.Build(); }
        catch (Exception e) { Debug.LogError("couldn't set the fortune envelope's art up automatically, try Tools > VFX > Build Weapon FX\n" + e); }
    }
}
