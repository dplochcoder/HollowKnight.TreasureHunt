using RandoSettingsManager.SettingsManagement;
using RandoSettingsManager.SettingsManagement.Versioning;

namespace TreasureHunt;

internal class SettingsProxy : RandoSettingsProxy<RandomizationSettings, string>
{
    internal static void Setup() => RandoSettingsManager.RandoSettingsManagerMod.Instance.RegisterConnection(new SettingsProxy());

    public override string ModKey => nameof(TreasureHuntMod);

    public override VersioningPolicy<string> VersioningPolicy => new StrictModVersioningPolicy(TreasureHuntMod.Instance!);

    public override bool TryProvideSettings(out RandomizationSettings? settings)
    {
        settings = TreasureHuntMod.GS.RS;
        return settings.IsEnabled;
    }

    public override void ReceiveSettings(RandomizationSettings? settings) => ConnectionMenu.Instance!.ApplySettings(settings ?? new());
}
