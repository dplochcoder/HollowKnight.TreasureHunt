using System.Collections;
using TMPro;
using UnityEngine;
using HutongGames.PlayMaker;
using System.Collections.Generic;

namespace TreasureHunt;

// Largely copied from ItemChanger.DialogueCenter, with some changes.
internal static class DialogueUtil
{
    private static GameObject DialogueManager => FsmVariables.GlobalVariables.FindFsmGameObject("DialogueManager").Value;
    private static PlayMakerFSM BoxOpenFsm => DialogueManager.LocateMyFSM("Box Open");
    private static GameObject DialogueText => FsmVariables.GlobalVariables.FindFsmGameObject("DialogueText").Value;

    private static DialogueBox DialogueBox => DialogueText.GetComponent<DialogueBox>();

    internal static Coroutine StartCoroutine(IEnumerator iter) => HeroController.instance.StartCoroutine(iter);

    private static IEnumerator ShowTextsImpl(List<string> texts, bool useTypewriter)
    {
        BoxOpenFsm.Fsm.Event("BOX UP DREAM");
        yield return new WaitForSeconds(0.15f); // orig: 0.3f

        DialogueText.LocateMyFSM("Dialogue Page Control").FsmVariables.GetFsmGameObject("Requester").Value = null;
        DialogueText.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Top;

        convoEnded = false;
        DialogueBox box = DialogueBox;
        box.useTypeWriter = useTypewriter;
        box.currentPage = 1;
        TextMeshPro textMesh = box.GetComponent<TextMeshPro>();
        textMesh.text = string.Join("<br><page>", texts);
        textMesh.ForceMeshUpdate();
        box.ShowPage(1);
        yield return new WaitUntil(ConvoEnded);

        BoxOpenFsm.Fsm.Event("BOX DOWN");
        yield return new WaitForSeconds(0.15f); // orig: 0.5f

        DialogueText.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.TopLeft;
    }

    internal static YieldInstruction ShowTexts(List<string> texts, bool useTypewriter = true) => StartCoroutine(ShowTextsImpl(texts, useTypewriter));

    internal static void Hook() => On.DialogueBox.HideText += OnHideText;

    internal static void Unhook() => On.DialogueBox.HideText -= OnHideText;

    private static void OnHideText(On.DialogueBox.orig_HideText orig, DialogueBox self)
    {
        convoEnded = true;
        orig(self);
    }

    static bool ConvoEnded() => convoEnded;
    static bool convoEnded = false;
}
