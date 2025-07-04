﻿using ItemChanger;
using PurenailCore.ModUtil;
using UnityEngine;

namespace TreasureHunt;

internal class TreasureHuntPreloader : PurenailCore.ModUtil.Preloader
{
    internal static readonly TreasureHuntPreloader Instance = new();

    [Preload(SceneNames.Cliffs_06, "Grimm Arrival Audio")]
    public GameObject? GrimmArrivalAudio { get; private set; }

    [Preload(SceneNames.RestingGrounds_08, "Ghost Battle Revek")]
    public GameObject? Revek { get; private set; }

    [Preload(SceneNames.Ruins1_24_boss, "Mage Lord/White Flash")]
    public GameObject? WhiteFlash { get; private set; }
}
