using RandoSettingsManager.SettingsManagement;
using RandoSettingsManager.SettingsManagement.Versioning;
using TreasureHunt.Rando;

namespace TreasureHunt.Interop;

internal class SettingsProxy : RandoSettingsProxy<RandomizationSettings, string>
{
    internal static void Setup() => RandoSettingsManager.RandoSettingsManagerMod.Instance.RegisterConnection(new SettingsProxy());

    public override string ModKey => nameof(TreasureHuntMod);

    public override VersioningPolicy<string> VersioningPolicy => new StrictModVersioningPolicy(TreasureHuntMod.Instance!);

    public override bool TryProvideSettings(out RandomizationSettings? settings)
    {
        settings = TreasureHuntMod.GS.RS;
        return settings.Enabled;
    }

    public override void ReceiveSettings(RandomizationSettings? settings) => ConnectionMenu.Instance!.ApplySettings(settings ?? new());
}
