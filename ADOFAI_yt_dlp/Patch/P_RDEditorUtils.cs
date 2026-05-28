
namespace ADOFAI_yt_dlp.Patch;

public static class P_RDEditorUtils__CheckModsDependency {
    public static void Prefix(ref object[] mods) {
        if(mods == null) {
            return;
        }

        bool hasTarget = mods.Any(m => m is string s &&
            (s.Equals("YouTubeStream", StringComparison.OrdinalIgnoreCase) ||
             s.Equals(Info.Name, StringComparison.OrdinalIgnoreCase)));

        if(!hasTarget) {
            return;
        }

        mods = [.. mods.Where(m => !(m is string s &&
            (s.Equals("YouTubeStream", StringComparison.OrdinalIgnoreCase) ||
             s.Equals(Info.Name, StringComparison.OrdinalIgnoreCase))))];
    }
}