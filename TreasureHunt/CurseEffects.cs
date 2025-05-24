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
        hudEffect.transform.localScale = new(-scale.x, -scale.y, scale.z);
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

    private int CurseDamageHook(ref int hazardType, int damage) => (curseActive && module.Settings.CurseOfWeakness) ? (damage + 1) : damage;

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

            if (revek == null && module!.Settings.CurseOfTheDamned && timeCursed >= initialRevekWait && timeInScene >= sceneRevekWait) (revek, revekAudioSrc) = SpawnRevek(module);
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

    private static (GameObject, GameObject) SpawnRevek(TreasureHuntModule module)
    {
        var rituals = module!.CompletedRituals;

        var revek = Instantiate(Preloader.Instance.Revek!);
        GameObject revekAudioSrc = new("Revek Audio Src");
        revekAudioSrc.transform.parent = HeroController.instance.gameObject.transform;
        revekAudioSrc.transform.localPosition = new(0, -12, 0);

        revek.transform.position = new(-100, -100);
        revek.SetActive(true);

        revek.FindChild("Slash Hit").LocateMyFSM("damages_hero").FsmVariables.GetFsmInt("damageDealt").Value = rituals + 1;

        var fsm = revek.LocateMyFSM("Control");
        fsm.FsmVariables.GetFsmFloat("Speed").Value = 180f + 10 * rituals;

        var gcp = fsm.GetState("Ghost Check Pause");
        gcp.ClearTransitions();
        gcp.AddTransition("FINISHED", "Set Angle");

        fsm.GetState("Set Angle").AddFirstAction(new Lambda(() =>
        {
            if (!module.IsCurseActive()) Destroy(revek);
        }));

        // Set position first for proper SFX.
        var teleIn = fsm.GetState("Slash Tele In");
        var flash = revek.GetOrAddComponent<SpriteFlash>();
        teleIn.AddFirstAction(new Lambda(() => SpriteFlashUtil.CursedFlash(flash)));
        teleIn.GetFirstActionOfType<AudioPlayerOneShotSingle>().spawnPoint = revekAudioSrc;

        var wait = fsm.GetState("Slash Idle").GetFirstActionOfType<WaitRandom>();
        wait.timeMin.Value = Mathf.Max(0.5f * Mathf.Pow(0.9f, rituals), 0.25f);
        wait.timeMax.Value = Mathf.Max(0.75f * Mathf.Pow(0.9f, rituals), wait.timeMin.Value);

        var slash = fsm.GetState("Slash");
        var audio = slash.GetFirstActionOfType<AudioPlayerOneShot>();
        audio.pitchMin.Value = 0.6f;
        audio.pitchMax.Value = 0.8f;

        var attackPause = fsm.GetState("Attack Pause");
        wait = attackPause.GetFirstActionOfType<WaitRandom>();
        wait.timeMin.Value = Mathf.Max(1.5f * Mathf.Pow(0.9f, rituals), 0.5f);
        wait.timeMax.Value = Mathf.Max(2f * Mathf.Pow(0.9f, rituals), 1f);

        // Parries buy more downtime over time.
        List<float> increase = [0];
        var damagedPause = fsm.GetState("Damaged Pause");
        var damagedWait = damagedPause.GetFirstActionOfType<WaitRandom>();
        damagedPause.AddFirstAction(new Lambda(() =>
        {
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
