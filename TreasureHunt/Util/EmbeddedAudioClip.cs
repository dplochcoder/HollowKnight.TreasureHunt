using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TreasureHunt.Util;

internal static class EmbeddedAudioClip
{
    private static Dictionary<string, AudioClip> clips = [];

    internal static AudioClip Load(string name)
    {
        if (clips.TryGetValue(name, out var clip)) return clip;

        using Stream s = typeof(EmbeddedAudioClip).Assembly.GetManifestResourceStream($"TreasureHunt.Resources.Sounds.{name}.wav");
        clip = SFCore.Utils.WavUtils.ToAudioClip(s, name);
        clips.Add(name, clip);
        return clip;
    }
}
