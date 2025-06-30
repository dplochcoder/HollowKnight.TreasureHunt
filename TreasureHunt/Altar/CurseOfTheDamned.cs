using HutongGames.PlayMaker.Actions;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using System.Collections.Generic;
using TreasureHunt.IC;
using UnityEngine;

namespace TreasureHunt.Altar;

internal class CurseOfTheDamned : MonoBehaviour, IHitResponder
{
    internal static GameObject SpawnRevek(TreasureHuntModule module)
    {
        var rituals = module!.CompletedRituals;

        var revek = Instantiate(TreasureHuntPreloader.Instance.Revek!);
        revek.AddComponent<CurseOfTheDamned>();

        GameObject revekAudioSrc = new("Revek Audio Src");
        revekAudioSrc.transform.parent = HeroController.instance.gameObject.transform;
        revekAudioSrc.transform.localPosition = new(0, -12, 0.1f);

        revek.transform.position = new(-100, -100);
        var furyWaves = MakeFuryWaves();
        furyWaves.transform.parent = revek.transform;
        furyWaves.transform.localPosition = Vector3.zero;
        furyWaves.SetActive(true);

        var redFlash = Instantiate(TreasureHuntPreloader.Instance.WhiteFlash!);
        redFlash.transform.localScale = new(4.25f, 4.25f, 4.25f);
        var flashRenderer = redFlash.GetComponent<SpriteRenderer>();
        flashRenderer.color = new(1f, 0.3f, 0.3f);
        flashRenderer.sortingOrder = 1;
        redFlash.transform.parent = revek.transform;
        redFlash.transform.localPosition = new(0, 0, -0.1f);
        redFlash.SetActive(false);

        var furyParticles = furyWaves.GetComponent<ParticleSystem>();
        furyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        revek.SetActive(true);
        revek.FindChild("Slash Hit").LocateMyFSM("damages_hero").FsmVariables.GetFsmInt("damageDealt").Value = rituals + 1;

        var fsm = revek.LocateMyFSM("Control");
        fsm.Fsm.GlobalTransitions = [];
        fsm.FsmVariables.GetFsmFloat("Speed").Value = 180f + 10 * rituals;
        foreach (var state in fsm.FsmStates) state.RemoveTransitionsOn("TAKE DAMAGE");

        var gcp = fsm.GetState("Ghost Check Pause");
        gcp.ClearTransitions();
        gcp.AddTransition("FINISHED", "Set Angle");

        fsm.GetState("Set Angle").AddFirstAction(new Lambda(() =>
        {
            if (!module.IsCurseActive()) Destroy(revek);
            else
            {
                redFlash.SetActive(true);
                furyParticles.Play();
            }
        }));

        // Set position first for proper SFX.
        var teleIn = fsm.GetState("Slash Tele In");
        var flash = revek.GetOrAddComponent<SpriteFlash>();
        // teleIn.AddFirstAction(new Lambda(() => SpriteFlashUtil.CursedFlash(flash)));
        teleIn.GetFirstActionOfType<AudioPlayerOneShotSingle>().spawnPoint = revekAudioSrc;

        var wait = fsm.GetState("Slash Idle").GetFirstActionOfType<WaitRandom>();
        wait.timeMin.Value = Mathf.Max(0.5f * Mathf.Pow(0.9f, rituals), 0.25f);
        wait.timeMax.Value = Mathf.Max(0.75f * Mathf.Pow(0.9f, rituals), wait.timeMin.Value);

        var slash = fsm.GetState("Slash");
        var audio = slash.GetFirstActionOfType<AudioPlayerOneShot>();
        audio.pitchMin.Value = 0.6f;
        audio.pitchMax.Value = 0.8f;

        var attackPause = fsm.GetState("Attack Pause");
        attackPause.AddFirstAction(new Lambda(() => furyParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting)));
        wait = attackPause.GetFirstActionOfType<WaitRandom>();
        wait.timeMin.Value = Mathf.Max(1.5f * Mathf.Pow(0.9f, rituals), 0.5f);
        wait.timeMax.Value = Mathf.Max(2f * Mathf.Pow(0.9f, rituals), 1f);

        // Parries buy more downtime over time.
        List<float> increase = [0];
        var damagedPause = fsm.GetState("Damaged Pause");
        var damagedWait = damagedPause.GetFirstActionOfType<WaitRandom>();
        damagedPause.AddFirstAction(new Lambda(() =>
        {
            furyParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            damagedWait.timeMin.Value = 4.5f + increase[0];
            damagedWait.timeMax.Value = 5.5f + increase[0];
            increase[0] += Mathf.Pow(0.85f, rituals);
        }));

        var hit = fsm.GetState("Hit").GetFirstActionOfType<AudioPlayerOneShot>();
        hit.pitchMin.Value = 0.6f;
        hit.pitchMax.Value = 0.8f;

        return revek;
    }

    private static GameObject MakeFuryWaves()
    {
        var furyWaves = Instantiate(HeroController.instance.gameObject.FindChild("Charm Effects")!.FindChild("Fury")!);
        furyWaves.transform.localScale = new(1.8f, 1.8f, 1.8f);

        var system = furyWaves.GetComponent<ParticleSystem>();
        var emission = system.emission;
        var rateOverTime = emission.rateOverTime;
        rateOverTime.curveMultiplier *= 2f;
        emission.rateOverTime = rateOverTime;
        var main = system.main;
        var startLifetime = main.startLifetime;
        startLifetime.curveMultiplier *= 0.75f;
        main.startLifetime = startLifetime;

        var renderer = furyWaves.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerName = "Over";
        renderer.sortingOrder = 1;

        Destroy(furyWaves.LocateMyFSM("Control Audio"));
        Destroy(furyWaves.GetComponent<AudioSource>());
        return furyWaves;
    }

    private MeshRenderer? renderer;
    private PlayMakerFSM? control;

    private void Awake()
    {
        renderer = GetComponent<MeshRenderer>();
        control = gameObject.LocateMyFSM("Control");
    }

    private void LateUpdate()
    {
        renderer!.sortingLayerName = "Over";
        renderer!.sortingOrder = 1;
    }

    private static readonly HashSet<string> VULNERABLE_STATES = ["Slash Idle", "Slash Antic", "Slash"];

    public void Hit(HitInstance damageInstance)
    {
        if (damageInstance.DamageDealt <= 0) return;
        if (control == null || !VULNERABLE_STATES.Contains(control.ActiveStateName)) return;

        switch (damageInstance.AttackType)
        {
            case AttackTypes.Nail:
            case AttackTypes.NailBeam:
            case AttackTypes.SharpShadow:
            case AttackTypes.Spell:
                control.SetState("Hit");
                break;
            case AttackTypes.Acid:
            case AttackTypes.Generic:
            case AttackTypes.RuinsWater:
                break;
        }
    }
}
