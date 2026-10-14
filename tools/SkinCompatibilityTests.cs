using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

static class SkinCompatibilityTests
{
    static int Main(string[] args)
    {
        string game = args[0];
        var paths = Directory.GetFiles(Path.Combine(game, "DerailValley_Data", "Managed"), "*.dll")
            .Concat(Directory.GetFiles(Path.Combine(game, "DerailValley_Data", "Managed", "UnityModManager"), "*.dll"))
            .Concat(Directory.GetFiles(Path.Combine(game, "Mods"), "*.dll", SearchOption.AllDirectories)).ToArray();
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            string path = paths.FirstOrDefault(p => Path.GetFileName(p) == name);
            return path == null ? null : Assembly.LoadFrom(path);
        };
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[1]));
            assembly.GetTypes(); // Reproduce the console's assembly scan that previously failed.
            var chooser = assembly.GetType("PassengerJobs.Skins.PassengerSkinChooser", true);
            // Harmony's SpawnCars wrapper needs Unity's Mono runtime (ECall bindings).
            // Verify target signatures here; install/runtime initialization happens in-game.
            var provider = Assembly.Load("SkinManagerMod").GetType("SkinManagerMod.SkinProvider", true);
            Assert(provider.GetMethod("GetNewSkin", new[] { Assembly.Load("DV.ThingTypes").GetType("DV.ThingTypes.TrainCarLivery", true) }) != null,
                "Skin Manager patch target exists");
            var generator = Assembly.Load("PassengerJobs").GetType("PassengerJobs.Generation.PassengerJobGenerator", true);
            Assert(generator.GetMethod("SpawnCars", BindingFlags.NonPublic | BindingFlags.Static) != null,
                "spawn cleanup target exists");
            var loader = generator.Assembly.GetType("PassengerJobs.PJMain", true)
                .GetMethod("TryLoadSkinManager", BindingFlags.NonPublic | BindingFlags.Static);
            RuntimeHelpers.PrepareMethod(loader.MethodHandle);
            Console.WriteLine("PASS: main integration loader JIT validation");
            var choose = chooser.GetMethod("ChooseConsistSkin", BindingFlags.Static | BindingFlags.NonPublic);
            var clear = chooser.GetMethod("ClearConsistSkin", BindingFlags.Static | BindingFlags.NonPublic);
            var skin = chooser.GetField("_chosenConsistSkin", BindingFlags.Static | BindingFlags.NonPublic);
            var liveries = chooser.GetField("_consistLiveries", BindingFlags.Static | BindingFlags.NonPublic);
            var liveryType = choose.GetParameters()[0].ParameterType;
            var livery = FormatterServices.GetUninitializedObject(liveryType);
            var list = Activator.CreateInstance(typeof(System.Collections.Generic.HashSet<>).MakeGenericType(liveryType));
            list.GetType().GetMethod("Add").Invoke(list, new[] { livery });
            object[] call = { livery, "original" };
            Assert((bool)choose.Invoke(null, call), "outside passenger spawning uses Skin Manager");
            skin.SetValue(null, "TestConsistSkin");
            liveries.SetValue(null, list);
            Assert(!(bool)choose.Invoke(null, call) && (string)call[1] == "TestConsistSkin", "consist selection overrides random skin");
            var other = FormatterServices.GetUninitializedObject(liveryType);
            // Clear the scope to represent a different livery / a completed or failed spawn.
            liveries.SetValue(null, Activator.CreateInstance(list.GetType()));
            Assert((bool)choose.Invoke(null, new object[] { other, "original" }), "unrelated livery keeps normal selection");
            clear.Invoke(null, null);
            Assert(skin.GetValue(null) == null && liveries.GetValue(null) == null, "finalizer clears consist state");
            Assert((bool)choose.Invoke(null, call), "selection falls back after cleanup");
            Console.WriteLine("PASS: assembly type scan, API targets, scoped selection and cleanup");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
