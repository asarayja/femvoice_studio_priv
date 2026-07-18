using UIKit;

namespace FemVoice.iOS;

/// <summary>iOS managed entry point — hands off to <see cref="AppDelegate"/> which hosts the shared Avalonia App.</summary>
public static class Application
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
