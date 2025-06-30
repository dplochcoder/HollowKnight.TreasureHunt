using System.Reflection;
using UnityEngine;

namespace TreasureHunt.Util;

internal static class SpriteFlashUtil
{
    private static FieldInfo flashColourField = typeof(SpriteFlash).GetField("flashColour", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo amountField = typeof(SpriteFlash).GetField("amount", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo timeUpField = typeof(SpriteFlash).GetField("timeUp", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo stayTimeField = typeof(SpriteFlash).GetField("stayTime", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo timeDownField = typeof(SpriteFlash).GetField("timeDown", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo blockField = typeof(SpriteFlash).GetField("block", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo flashingStateField = typeof(SpriteFlash).GetField("flashingState", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo flashTimerField = typeof(SpriteFlash).GetField("flashTimer", BindingFlags.NonPublic | BindingFlags.Instance);
    private static FieldInfo repeatFlashField = typeof(SpriteFlash).GetField("repeatFlash", BindingFlags.NonPublic | BindingFlags.Instance);

    internal static void CursedFlash(this SpriteFlash flash)
    {
        Color red = new(1f, 0.2f, 0.2f);
        flashColourField.SetValue(flash, red);
        amountField.SetValue(flash, 1f);
        timeUpField.SetValue(flash, 0.1f);
        stayTimeField.SetValue(flash, 9999f);
        timeDownField.SetValue(flash, 2f);
        flashingStateField.SetValue(flash, 1);
        flashTimerField.SetValue(flash, 0f);
        repeatFlashField.SetValue(flash, true);

        var block = (blockField.GetValue(flash) as MaterialPropertyBlock)!;
        block.Clear();
        block.SetColor("_FlashColor", red);
    }
}
