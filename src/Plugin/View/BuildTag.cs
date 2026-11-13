using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal static class BuildTag
{
    private static readonly Color A = new(0.27f, 0.52f, 0.95f, 0.95f), B = new(0.85f, 0.33f, 0.62f, 0.95f), C = new(0.13f, 0.66f, 0.72f, 0.95f);

    internal static Color Fill(char letter) => letter switch
    {
        'A' => A,
        'B' => B,
        _ => C,
    };

    internal static RuntimeObject Create(char letter, float size, int textSize)
    {
        var tag = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        RuntimeObject? label = null;
        try
        {
            tag.Set("touchable", false);
            tag.Call("AddChild", Ui.Graph(size, size, Fill(letter), size * 0.2f));
            label = Ui.Text(letter.ToString(), textSize, Color.white, 0f, wrap: false);
            label.Call("SetXY", (size - label.Get<float>("width")) / 2, (size - label.Get<float>("height")) / 2);
            tag.Call("AddChild", label);
            tag.Call("SetSize", size, size);
            return tag;
        }
        catch
        {
            GameUi.Dispose(label);
            GameUi.Dispose(tag);
            throw;
        }
    }
}
