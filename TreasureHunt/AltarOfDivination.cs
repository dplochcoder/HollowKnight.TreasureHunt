using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using ItemChanger.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreasureHunt;

internal class AltarOfDivination
{
    private static readonly EmbeddedSprite altar = new("altar");

    public static void Spawn(Scene scene)
    {
        scene.FindGameObject("rg_gate")?.SetActive(false);

        var tablet = TabletUtility.InstantiateTablet(nameof(AltarOfDivination));

        tablet.transform.position = new(33.5f, 5.7f, 2.5f);
        tablet.SetActive(true);
        foreach (Transform child in tablet.transform) child.gameObject.SetActive(false);

        GameObject sprite = new("AltarSprite");
        sprite.transform.SetParent(tablet.transform);
        sprite.transform.position = new(33.5f, 9.1f, 0.01f);
        sprite.transform.localScale = new(3.2f, 3.2f, 3.2f);
        var spriteRenderer = sprite.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = altar.Value;
        spriteRenderer.color = new(0.85f, 0.85f, 0.85f, 1f);

        HookTabletControl(tablet.LocateMyFSM("Tablet Control"));
        HookInspectState(tablet.LocateMyFSM("Inspection"));
    }

    private const string RITUAL_END = "RITUAL_END";

    private static void HookTabletControl(PlayMakerFSM fsm)
    {
        var init = fsm.GetState("Init");
        init.SetActions(init.Actions[0], init.Actions[4]);

        var away = fsm.GetState("Away");
        away.RemoveActionsOfType<SetParticleEmission>();
        away.RemoveActionsOfType<FadeColorFader>();

        var close = fsm.GetState("Close");
        close.RemoveActionsOfType<SetParticleEmissionRate>();
        close.RemoveActionsOfType<PlayParticleEmitter>();
        close.RemoveActionsOfType<SetParticleEmission>();
        close.RemoveActionsOfType<FadeColorFader>();
    }

    // Logic largely copied from ItemChanger's TabletUtility.
    private static void HookInspectState(PlayMakerFSM fsm)
    {
        var promptUp = fsm.GetState("Prompt Up");
        promptUp.SetActions(
            promptUp.Actions[0],
            promptUp.Actions[1],
            promptUp.Actions[3],
            new AsyncLambda(cb => PerformRitual(fsm.gameObject, cb), RITUAL_END));

        var setBool = fsm.GetState("Set Bool");
        var turnBack = fsm.GetState("Turn Back");
        promptUp.ClearTransitions();
        promptUp.AddTransition(RITUAL_END, turnBack);
        foreach (var t in setBool.Transitions) t.SetToState(turnBack);
    }

    internal static void QueueDirectDamage(int damage) => HeroController.instance.StartCoroutine(DealDirectDamage(damage));

    private static IEnumerator DealDirectDamage(int damage)
    {
        yield return new WaitUntil(() => HeroController.instance.acceptingInput);
        HeroController.instance.TakeDamage(null, GlobalEnums.CollisionSide.top, damage, 1);
    }

    private static string ShowTime(float secs)
    {
        if (secs > 9 * 60)
        {
            int minutes = Mathf.CeilToInt(secs / 60);
            return $"{minutes} minutes";
        }
        else
        {
            int minutes = Mathf.FloorToInt(secs / 60);
            int seconds = Mathf.FloorToInt(secs % 60);
            if (minutes > 0 && seconds > 0) return $"{minutes} minutes and {seconds} seconds";
            else if (minutes > 0) return $"{minutes} minutes";
            else return $"{(seconds > 0 ? seconds : 1)} seconds";
        }
    }

    private const float SINCE_BEGINNING = 30 * 60;
    private const float SINCE_LAST = 5 * 60;

    private static bool XeroActive()
    {
        var warrior = GameObject.Find("Warrior");
        if (warrior == null) return false;
        if (warrior.transform.childCount == 0) return false;

        return warrior.transform.GetChild(0).gameObject.activeSelf;
    }

    private static void PlayAngryVoice(GameObject src)
    {
        var clip = EmbeddedAudioClip.Load("anger");

        var audioSource = src.GetOrAddComponent<AudioSource>();
        audioSource.pitch = 0.5f;
        audioSource.PlayOneShot(clip);
    }

    private static IEnumerator PerformRitualInnerImpl(GameObject src)
    {
        if (XeroActive())
        {
            yield return DialogueUtil.ShowTexts(["...are you serious? Now, with this riff-raff?<br>Finish what you started, we cannot divine with this noise."]);
            QueueDirectDamage(2);
            yield break;
        }

        var mod = ItemChangerMod.Modules.Get<TreasureHuntModule>()!;
        if (mod.IsCurseActive())
        {
            yield return DialogueUtil.ShowTexts(["There is no escape. This is the price you must pay.<br>You'd best move along now. Quickly."], false);
            QueueDirectDamage(1);
            yield break;
        }

        // Check completion.
        if (mod.Finished())
        {
            yield return DialogueUtil.ShowTexts(["The spirits of the altar sleep."], false);
            yield break;
        }

        // Check time.
        if (mod.CompletedRituals == 0 && mod.GameTime < SINCE_BEGINNING)
        {
            yield return DialogueUtil.ShowTexts([$"Impatient vessel, we are not ready. Go, explore, collect.<br><br>Return to bargain in {ShowTime(SINCE_BEGINNING - mod.GameTime)}."]);
            yield break;
        }
        if (mod.CompletedRituals > 0 && mod.GameTime < mod.LastCompletedRitual + SINCE_LAST)
        {
            var wait = mod.LastCompletedRitual + SINCE_LAST - mod.GameTime;
            yield return DialogueUtil.ShowTexts([$"Tarnished one, you would return so soon? We will not be so kind next time.<br><br>Go, return in {ShowTime(wait)} if you must."]);
            yield break;
        }

        // Check accessibility.
        var accessible = mod.GetVisibleAccessibleTreasure();
        if (accessible != null)
        {
            PlayAngryVoice(src);
            yield return DialogueUtil.ShowTexts([
                "Impatient, petulant, dishonorable.<br>Does it not know that with which it bargains?",
                $"Vessel of blindness, seek the {accessible} before you seek us.<br>Cursed are thee who gaze beyond the veil."]);
            QueueDirectDamage(2);
            yield break;
        }

        // Check health and shade.
        var pd = PlayerData.instance;
        if (pd.GetBool(nameof(PlayerData.equippedCharm_27)))
        {
            PlayAngryVoice(src);
            yield return DialogueUtil.ShowTexts([
                "Wretched!<br>Vile street ant!<br>Unspeakable!",
                "It injects its blood with the forbidden nectar, we shall <b>not</b> abide it.<br><br>Begone, filth!"]);
            QueueDirectDamage(2);
            yield break;
        }
        if (pd.GetInt(nameof(PlayerData.health)) < pd.GetInt(nameof(PlayerData.maxHealth)))
        {
            yield return DialogueUtil.ShowTexts(["It is wounded, it lacks resolve.<br>Go, mend your wounds before your offering.", "And this one too."]);
            QueueDirectDamage(1);
            yield break;
        }
        if (pd.GetBool(nameof(PlayerData.soulLimited)))
        {
            yield return DialogueUtil.ShowTexts(["It leaks of regret, it is not whole.<br>The offering must leave nothing behind."]);
            QueueDirectDamage(1);
            yield break;
        }

        // Check geo.
        int cost = mod.GetRitualCost();
        if (pd.GetInt(nameof(PlayerData.geo)) < cost)
        {
            int missing = cost - pd.GetInt(nameof(PlayerData.geo));
            yield return DialogueUtil.ShowTexts([$"Poor vessel, meager vessel. It arrives with spirit, but not enough.<br><br>Return with {missing} more geo and we might aid you still."]);
            yield break;
        }

        // TODO: Higher costs.
        yield return DialogueUtil.ShowTexts([
            "It arrives.<br>Whole, resolved, pure, and without recourse.<br>Its need is true and its tithings are grand.",
            "We shall scour the world, that it might have purpose again."]);

        // TODO: Big shaky animation.
        HeroController.instance.TakeGeo(cost);
        yield return new WaitForSeconds(5);

        var cursedIndices = mod.Curse();
        if (cursedIndices.Count == 0)
        {
            yield return DialogueUtil.ShowTexts(["...", "... ...", "Troubling. Your world is complex.", "You need more guidance than we can provide. Best keep searching."]);
            FlingGeoAction.SpawnGeo(cost - 300, true, FlingType.Everywhere, new(33.5f, 7.75f));
            yield break;
        }

        yield return DialogueUtil.ShowTexts(["It is done.", "Pierce the veil."]);

        var remainingGeo = pd.GetInt(nameof(PlayerData.geo));
        QueueDirectDamage(9999);

        ritualResetAction = _ => SetShadeAfterRitual(cursedIndices, remainingGeo);
        Events.OnSceneChange += ritualResetAction;
    }

    private static Action<Scene>? ritualResetAction;
    private const string SHADE_SCENE = "Neverwhere";

    private static void SetShadeAfterRitual(List<int> cursedIndices, int geo)
    {
        Events.OnSceneChange -= ritualResetAction;
        ritualResetAction = null;

        var pd = PlayerData.instance;
        if (pd.GetString(nameof(pd.shadeScene)) != SceneNames.RestingGrounds_02) return;  // Cheater!

        pd.SetInt(nameof(pd.geo), geo);
        pd.SetInt(nameof(pd.geoPool), 0);
        pd.SetString(nameof(pd.shadeScene), SHADE_SCENE);
        pd.SetString(nameof(pd.shadeMapZone), "NULL");

        var mod = ItemChangerMod.Modules.Get<TreasureHuntModule>()!;
        mod.CursedIndices = cursedIndices;
        mod.UpdateDisplayData();

        GameManager.instance.SaveGame();
    }

    internal static void MaybeRestoreShade()
    {
        var pd = PlayerData.instance;
        if (pd.GetString(nameof(pd.shadeScene)) != SHADE_SCENE) return;

        pd.SetString(nameof(pd.shadeScene), SceneNames.RestingGrounds_02);
        pd.SetFloat(nameof(pd.shadePositionX), 33.5f);
        pd.SetFloat(nameof(pd.shadePositionY), 11.5f);
    }

    private static IEnumerator PerformRitualImpl(GameObject src, Action callback)
    {
        yield return DialogueUtil.StartCoroutine(PerformRitualInnerImpl(src));
        callback();
    }

    private static void PerformRitual(GameObject src, Action callback) => DialogueUtil.StartCoroutine(PerformRitualImpl(src, callback));
}
