using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Logging;

namespace MyFirstSts2Mod;

[ModInitializer("Initialize")]
public static class ModMain
{
    public static void Initialize()
    {
        Log.Info("Hello, Slay the Spire 2 Modding World!");
    }
}
