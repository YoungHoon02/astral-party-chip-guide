using ChipGuide.Plugin.Runtime;

namespace ChipGuide.Plugin.View;

internal static class TitleBand
{
    // 제목 띠 배경은 이름 없는 자식이라, 칩 획득 확률 글자를 세로로 감싸면서 창 폭 대부분을 덮는 자식을 띠로 본다.
    internal static (float Top, float Bottom)? Find(RuntimeObject container)
    {
        var probability = container.Field("txt_Probability");
        if (probability == null || !probability.Get<bool>("visible")) return null;
        var textTop = probability.Get<float>("y");
        var textBottom = textTop + probability.Get<float>("height");
        var minWidth = container.Get<float>("width") * 0.8f;
        // 창 전체를 덮는 어두운 배경도 같은 조건에 걸리므로 높이로 거른다.
        var maxHeight = container.Get<float>("height") * 0.5f;
        (float Top, float Bottom)? band = null;
        var count = container.Get<int>("numChildren");
        for (var i = 0; i < count; i++)
        {
            var child = container.Call("GetChildAt", i);
            if (child == null || child.Pointer == probability.Pointer || !child.Get<bool>("visible")) continue;
            var height = child.Get<float>("height");
            if (child.Get<float>("width") < minWidth || height > maxHeight) continue;
            var y = child.Get<float>("y");
            if (y <= textTop && y + height >= textBottom && (band == null || y > band.Value.Top)) band = (y, y + height);
        }
        if (Plugin.Diagnostics.Value)
            Plugin.Logger.LogInfo($"[진단] 제목 띠 위쪽={band?.Top.ToString() ?? "못 찾음"} 아래쪽={band?.Bottom.ToString() ?? "못 찾음"} 확률 글자 위쪽={textTop}");
        return band;
    }
}
