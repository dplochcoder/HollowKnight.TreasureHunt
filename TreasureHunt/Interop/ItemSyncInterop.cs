using MultiWorldLib;
using PurenailCore.SystemUtil;
using System.IO;
using TreasureHunt.IC;

namespace TreasureHunt.Interop;

internal static class ItemSyncInterop
{
    internal static void HookItemSync() => ItemSyncMod.ItemSyncMod.Connection.OnDataReceived += ReceiveCurse;

    internal static void UnhookItemSync() => ItemSyncMod.ItemSyncMod.Connection.OnDataReceived -= ReceiveCurse;

    private const string CURSE_LABEL = "TreasureHunt-Curse";

    private static void ReceiveCurse(DataReceivedEvent data)
    {
        if (data.Label != CURSE_LABEL) return;

        var curse = JsonUtil<TreasureHuntMod>.DeserializeFromString<Curse>(data.Content);
        TreasureHuntModule.Get()!.ReceiveCurse(curse);
        data.Handled = true;
    }

    internal static void MaybeSendCurse(Curse curse)
    {
        if (!ItemSyncMod.ItemSyncMod.ISSettings.IsItemSync) return;

        StringWriter sw = new();
        RandomizerCore.Json.JsonUtil.GetNonLogicSerializer().Serialize(sw, curse);
        ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(CURSE_LABEL, sw.ToString(), 300);
    }
}
