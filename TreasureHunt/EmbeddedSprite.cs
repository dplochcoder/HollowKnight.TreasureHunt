using ItemChanger.Internal;

namespace TreasureHunt;

public class EmbeddedSprite : ItemChanger.EmbeddedSprite
{
    private static readonly SpriteManager manager = new(typeof(EmbeddedSprite).Assembly, "TreasureHunt.Resources.Sprites.");

    public EmbeddedSprite(string key) => this.key = key;

    public override SpriteManager SpriteManager => manager;
}
