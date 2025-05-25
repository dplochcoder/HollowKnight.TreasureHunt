using ItemChanger;
using PurenailCore.ModUtil;
using UnityEngine;

namespace TreasureHunt;

internal class Preloader : PurenailCore.ModUtil.Preloader
{
    internal static readonly Preloader Instance = new();

    [Preload(SceneNames.RestingGrounds_08, "Ghost Battle Revek")]
    public GameObject? Revek { get; private set; }

    [Preload(SceneNames.Ruins1_24_boss, "Mage Lord/White Flash")]
    public GameObject? WhiteFlash { get; private set; }
}
