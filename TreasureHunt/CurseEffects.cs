using HutongGames.PlayMaker.Actions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using Modding;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TreasureHunt;

internal class CurseEffects : MonoBehaviour
{
    internal static CurseEffects Create()
    {
        GameObject obj = new("CurseEffects");
        DontDestroyOnLoad(obj);
        
        return obj.AddComponent<CurseEffects>();
    }

    private bool curseActive = false;

    internal void SetCurseActive(bool active)
    {
        if (active && !curseActive)
        {
            curseActive = true;
            soulDrain = 0;
            timeCursed = 0;
            timeInScene = 0;
            initialRevekWait = Random.Range(REVEK_INITIAL_WAIT_MIN, REVEK_INITIAL_WAIT_MAX);
            sceneRevekWait = Random.Range(REVEK_SCENE_WAIT_MIN, REVEK_SCENE_WAIT_MAX);

            hudFadeGroup?.FadeUp();
            realParticles?.SetActive(false);
            curseParticles?.SetActive(true);
        }
        else if (!active && curseActive)
        {
            curseActive = false;
            hudFadeGroup?.FadeDown();

            if (heroLightRenderer != null) heroLightRenderer.color = GameCameras.instance.sceneColorManager.HeroLightColorA;

            var pd = PlayerData.instance;
            if (pd.GetInt(nameof(pd.health)) + pd.GetInt(nameof(pd.healthBlue)) > 1) EnableLeakParticles(false);

            realParticles?.SetActive(true);
            StopCurseParticles();
        }
    }

    private TreasureHuntModule? module;
    private GameObject? curseParticles;
    private GameObject? hudEffect;
    private FadeGroup? hudFadeGroup;
    private SpriteRenderer? heroLightRenderer;
    private ParticleSystem? leakParticles;

    private void Awake()
    {
        module = ItemChanger.ItemChangerMod.Modules.Get<TreasureHuntModule>();

        ModHooks.TakeDamageHook += CurseDamageHook;
        On.SceneParticlesController.EnableParticles += OverrideSPCEnableParticles;
        On.SceneParticlesController.DisableParticles += OverrideSPCDisableParticles;
        On.SceneParticlesController.BeginScene += OverrideSPCBeginScene;
        Events.OnBeginSceneTransition += ResetSceneTimer;

        curseParticles = CreateCurseParticles();

        var hud = GameObject.Find("_GameCameras")!.FindChild("HudCamera")!;
        hudEffect = Instantiate(hud.FindChild("fury_effects_v2"))!;
        hudEffect.name = "CurseHud";
        hudEffect.transform.SetParent(hud.transform);
        var scale = hudEffect.transform.localScale;
        hudEffect.transform.localScale = new(-scale.x * 1.4f, -scale.y * 1.2f, scale.z * 1.2f);
        hudFadeGroup = hudEffect.GetComponent<FadeGroup>();
    }

    private void OnDestroy()
    {
        ModHooks.TakeDamageHook -= CurseDamageHook;
        On.SceneParticlesController.EnableParticles -= OverrideSPCEnableParticles;
        On.SceneParticlesController.DisableParticles -= OverrideSPCDisableParticles;
        On.SceneParticlesController.BeginScene -= OverrideSPCBeginScene;
        Events.OnBeginSceneTransition -= ResetSceneTimer;

        Destroy(curseParticles);
    }

    private int CurseDamageHook(ref int hazardType, int damage) => (curseActive && module!.Settings.CurseOfWeakness && damage > 0) ? (damage + 1) : damage;

    private static GameObject CreateCurseParticles()
    {
        var ctrl = GameObject.Find("_GameCameras").FindChild("CameraParent")!.FindChild("tk2dCamera")!.FindChild("SceneParticlesController")!;
        var src = ctrl.FindChild("resting_grounds_particles")!;

        var clone = Instantiate(src)!;
        clone.SetActive(false);
        DontDestroyOnLoad(clone);

        clone.transform.parent = ctrl!.transform;
        clone.transform.localPosition = src.transform.localPosition;

        foreach (Transform t in clone.transform)
        {
            var particleSystem = t.gameObject.GetComponent<ParticleSystem>();

            var colorOverLifetime = particleSystem.colorOverLifetime;
            var color = colorOverLifetime.color;
            var grad = color.gradient;
            var newColorKeys = new GradientColorKey[grad.colorKeys.Length];
            for (int i = 0; i < newColorKeys.Length; i++) newColorKeys[i].color = CURSED_HERO_LIGHT_COLOR;
            grad.SetKeys(newColorKeys, grad.alphaKeys);
            color.gradient = grad;
            colorOverLifetime.color = color;

            if (particleSystem.name == "soul_particles")
            {
                var size = particleSystem.sizeOverLifetime;
                size.sizeMultiplier *= 1.2f;

                var main = particleSystem.main;
                main.maxParticles = main.maxParticles * 3;

                var lifetime = main.startLifetime;
                lifetime.curveMultiplier /= 1.5f;
            }
        }

        return clone;
    }

    private void StopCurseParticles()
    {
        foreach (var p in curseParticles!.GetComponentsInChildren<ParticleSystem>()) p.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        GameObject prev = curseParticles;
        IEnumerator DestroyParticles()
        {
            yield return new WaitForSeconds(18);
            Destroy(prev);
        }
        GameManager.instance.StartCoroutine(DestroyParticles());

        // Discard the old object, make a new one if re-cursed.
        curseParticles = CreateCurseParticles();
    }

    private IEnumerable<GameObject> BaseParticleObjects(SceneParticlesController ctrl)
    {
        foreach (var p in ctrl.sceneParticles) if (p.particleObject != null) yield return p.particleObject;
        if (ctrl.defaultParticles.particleObject != null) yield return ctrl.defaultParticles.particleObject;
    }

    private GameObject? realParticles;

    private void OverrideSPCEnableParticles(On.SceneParticlesController.orig_EnableParticles orig, SceneParticlesController self)
    {
        orig(self);

        realParticles = BaseParticleObjects(self).Where(o => o.activeSelf).FirstOrDefault();
        if (curseActive)
        {
            realParticles?.SetActive(false);
            curseParticles?.SetActive(true);
        }
    }

    private void OverrideSPCDisableParticles(On.SceneParticlesController.orig_DisableParticles orig, SceneParticlesController self)
    {
        orig(self);

        realParticles = null;
        curseParticles?.SetActive(false);
    }

    private void OverrideSPCBeginScene(On.SceneParticlesController.orig_BeginScene orig, SceneParticlesController self)
    {
        if (curseActive)
        {
            var sm = GameManager.instance.sm ?? FindObjectOfType<SceneManager>();
            sm.noParticles = false;
        }

        orig(self);
    }

    private const float HERO_LIGHT_BLEND = 0.5f;
    internal static readonly Color CURSED_HERO_LIGHT_COLOR = new(0.85f, 0, 0);
    private float activeTime;

    private static Color sceneColor => GameCameras.instance.sceneColorManager.HeroLightColorA;

    private static float Interp(float a, float pct, float b) => a + (b - a) * pct;
    private static Color InterpColor(Color a, float pct, Color b, float alpha) => new(Interp(a.r, pct, b.r), Interp(a.g, pct, b.g), Interp(a.b, pct, b.b), alpha);

    private void EnableLeakParticles(bool enable)
    {
        if (leakParticles == null) return;

        var emission = leakParticles.emission;
        emission.enabled = enable;
    }

    private const float REVEK_INITIAL_WAIT_MIN = 9f;
    private const float REVEK_INITIAL_WAIT_MAX = 13f;
    private const float REVEK_SCENE_WAIT_MIN = 2.5f;
    private const float REVEK_SCENE_WAIT_MAX = 4.5f;

    private float soulDrain;
    private float timeCursed;
    private float timeInScene;
    private float initialRevekWait;
    private float sceneRevekWait;
    private GameObject? revek;
    private GameObject? revekAudioSrc;

    private void ResetSceneTimer(Transition ignored)
    {
        timeInScene = 0;
        sceneRevekWait = Random.Range(REVEK_SCENE_WAIT_MIN, REVEK_SCENE_WAIT_MAX);

        revek = null;
        if (revekAudioSrc != null)
        {
            Destroy(revekAudioSrc);
            revekAudioSrc = null;
        }
    }

    private static readonly HashSet<string> INVALID_REVEK_SCENES = [
        SceneNames.Abyss_03_b,  // Deepnest Tram
        SceneNames.Abyss_05,  // Palace Grounds
        SceneNames.Abyss_18,  // Basin Toll
        SceneNames.Abyss_21,  // Monarch Wings
        SceneNames.Abyss_22,  // Hidden Station Stag
        SceneNames.Cliffs_03,  // Stag Nest Stag
        SceneNames.Cliffs_05,  // Joni's
        SceneNames.Cliffs_06,  // Grimm Lantern
        SceneNames.Crossroads_30,  // Hot Spring Bench
        SceneNames.Crossroads_38,  // Grubfather
        SceneNames.Crossroads_47,  // Crossroads Stag
        SceneNames.Crossroads_49,  // Queen's Elevator
        SceneNames.Crossroads_49b,  // Queen's Elevator
        SceneNames.Crossroads_50,  // Blue Lake
        SceneNames.Deepnest_09,  // Distant Village Stag
        SceneNames.Deepnest_East_13,  // Edge Camp Bench
        SceneNames.Deepnest_Spider_Town,
        SceneNames.Fungus1_08,  // Hunter
        SceneNames.Fungus1_16_alt,  // Greenpath Stag
        SceneNames.Fungus1_24,  // Gardens Cornifer
        SceneNames.Fungus1_37,  // Stonesanc Bench
        SceneNames.Fungus2_02,  // Queen's Station stag
        SceneNames.Fungus2_26,  // Leg Eater
        SceneNames.Fungus2_34,  // Willoh
        SceneNames.Fungus3_archive,  // Archives Bench
        SceneNames.Fungus3_39,  // Traitor's Grave
        SceneNames.Fungus3_50,  // Queen's Gardens Toll
        SceneNames.Grimm_Divine,
        SceneNames.Mines_18,  // CG1 Bench
        SceneNames.Mines_28,  // Outside Crystallized Mound
        SceneNames.Mines_30,  // CDash outside Cornifer
        SceneNames.Mines_36,  // Deep Focus
        SceneNames.RestingGrounds_07,  // Seer
        SceneNames.RestingGrounds_09,  // Stag
        SceneNames.RestingGrounds_12,  // Outside Grey Mourner
        SceneNames.Room_Bretta,
        SceneNames.Room_Bretta_Basement,
        SceneNames.Room_Colosseum_01,
        SceneNames.Room_Colosseum_02,
        SceneNames.Room_Colosseum_Spectate,
        SceneNames.Room_Charm_Shop,
        SceneNames.Room_Final_Boss_Atrium,
        SceneNames.Room_Final_Boss_Core,
        SceneNames.Room_Jinn,
        SceneNames.Room_Mansion,
        SceneNames.Room_mapper,
        SceneNames.Room_Mask_Maker,
        SceneNames.Room_Mender_House,
        SceneNames.Room_nailmaster,
        SceneNames.Room_nailmaster_02,
        SceneNames.Room_nailmaster_03,
        SceneNames.Room_Ouiji,
        SceneNames.Room_Queen,
        SceneNames.Room_ruinhouse,
        SceneNames.Room_shop,
        SceneNames.Room_Slug_Shrine,
        SceneNames.Room_Sly_Storeroom,
        SceneNames.Room_spider_small,
        SceneNames.Room_temple,
        SceneNames.Room_Town_Stag_Station,
        SceneNames.Room_Tram,
        SceneNames.Room_Tram_RG,
        SceneNames.Room_Wyrm,
        SceneNames.Ruins_Bathhouse,
        SceneNames.Ruins_House_03,  // Emilitia
        SceneNames.Ruins1_18,  // Spire Bench
        SceneNames.Ruins1_27,  // Fountain
        SceneNames.Ruins1_29,  // City Storerooms Stag
        SceneNames.Ruins1_05b,  // Lemm
        SceneNames.Ruins2_Watcher_Room,  // Lurien
        SceneNames.Ruins2_08,  // King's Station Stag
        SceneNames.Ruins2_10,  // King's Elevator
        SceneNames.Ruins2_10b,  // King's Elevator
        SceneNames.Town,
        SceneNames.Waterways_03,  // Tuk
        SceneNames.Waterways_15,  // Dung Defender relic
    ];

    private static bool IsValidScene()
    {
        var gm = GameManager.instance;
        if (!gm.IsGameplayScene() || gm.IsCinematicScene()) return false;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene.StartsWith("Dream_")) return false;
        if (scene.StartsWith("GG_") && scene != SceneNames.GG_Pipeway && scene != SceneNames.GG_Waterways) return false;
        if (scene.StartsWith("White_Palace_")) return false;

        return !INVALID_REVEK_SCENES.Contains(scene);
    }

    private void Update()
    {
        heroLightRenderer ??= HeroController.instance?.gameObject.FindChild("HeroLight")?.GetComponent<SpriteRenderer>();
        leakParticles ??= HeroController.instance?.gameObject.FindChild("Low Health Leak")?.GetComponent<ParticleSystem>();

        if (curseActive && (HeroController.instance?.acceptingInput ?? false))
        {
            timeCursed += Time.deltaTime;
            timeInScene += Time.deltaTime;
            soulDrain += Time.deltaTime * (module!.CompletedRituals + 1);

            int taken = Mathf.FloorToInt(soulDrain);
            if (taken > 0)
            {
                HeroController.instance.TakeMP(taken);
                soulDrain -= taken;
            }

            if (revek == null && IsValidScene() && module!.Settings.CurseOfTheDamned && timeCursed >= initialRevekWait && timeInScene >= sceneRevekWait) (revek, revekAudioSrc) = SpawnRevek(module);
        }

        if (curseActive)
        {
            activeTime += Time.deltaTime;
            if (activeTime > HERO_LIGHT_BLEND) activeTime = HERO_LIGHT_BLEND;

            EnableLeakParticles(true);
        }
        else if (activeTime > 0)
        {
            activeTime -= Time.deltaTime;
            if (activeTime <= 0) activeTime = 0;
        }
    }

    private void LateUpdate()
    {
        if (heroLightRenderer != null)
        {
            var pct = activeTime / HERO_LIGHT_BLEND;
            heroLightRenderer.color = InterpColor(sceneColor, pct, CURSED_HERO_LIGHT_COLOR, sceneColor.a);
        }
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

    private static (GameObject, GameObject) SpawnRevek(TreasureHuntModule module)
    {
        var rituals = module!.CompletedRituals;

        var revek = Instantiate(Preloader.Instance.Revek!);
        revek.AddComponent<FixRenderer>();

        GameObject revekAudioSrc = new("Revek Audio Src");
        revekAudioSrc.transform.parent = HeroController.instance.gameObject.transform;
        revekAudioSrc.transform.localPosition = new(0, -12, 0.1f);

        revek.transform.position = new(-100, -100);
        var furyWaves = MakeFuryWaves();
        furyWaves.transform.parent = revek.transform;
        furyWaves.transform.localPosition = Vector3.zero;
        furyWaves.SetActive(true);

        var redFlash = Instantiate(Preloader.Instance.WhiteFlash!);
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

        return (revek, revekAudioSrc);
    }
}

internal class FixRenderer : MonoBehaviour
{
    private MeshRenderer? renderer;

    private void Awake() => renderer = GetComponent<MeshRenderer>();

    private void LateUpdate()
    {
        renderer!.sortingLayerName = "Over";
        renderer!.sortingOrder = 1;
    }
}
