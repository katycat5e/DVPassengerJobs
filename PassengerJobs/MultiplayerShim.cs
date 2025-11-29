using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;

namespace PassengerJobs;

internal static class MultiplayerShim
{
    const string MULTIPLAYER_MOD_ID = "Multiplayer";
    const string MPAPI_ASSEMBLY_NAME = "MultiplayerAPI";
    const string MPAPI_TYPE_NAME = "MPAPI.MultiplayerAPI";
    const string MPAPI_INSTANCE_PROPERTY = "Instance";

    const string MP_INTEGRATION_DLL = "PassengerJobs.MP.dll";
    const string MP_INTEGRATION_BOOTSTRAP = "PassengerJobs.MP.Bootstrap";
    const string MP_INTEGRATION_INIT_METHOD = "Initialize";

    const string IS_HOST_PROPERTY = "IsHost";

    private static object? _mpApiInstance;
    private static PropertyInfo? _isHost;

    internal static bool IsInitialized { get; private set; } = false;

    internal static bool IsHost
    {
        get
        {
            if (_isHost == null)
                return false;

            return (bool)_isHost.GetValue(_mpApiInstance)!;
        }
    }

    internal static void TryInitialise(UnityModManager.ModEntry modEntry)
    {
        UnityModManager.ModEntry? multiplayer = UnityModManager.FindMod(MULTIPLAYER_MOD_ID);
        var mpapiAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == MPAPI_ASSEMBLY_NAME);
        var path = Path.Combine(modEntry.Path, MP_INTEGRATION_DLL);

        try
        {
            if (multiplayer?.Enabled == true && mpapiAssembly != null)
            {
                PJMain.Log("Multiplayer mod detected");
                if (!File.Exists(path))
                {
                    PJMain.Warning($"{MP_INTEGRATION_DLL} Was not found, unable to activate multiplayer integration");
                    return;
                }

                var mpAssembly = Assembly.LoadFile(path);
                var bootstrap = mpAssembly.GetType(MP_INTEGRATION_BOOTSTRAP);

                if (bootstrap == null)
                {
                    PJMain.Warning($"Failed to find {MP_INTEGRATION_BOOTSTRAP} in {MP_INTEGRATION_DLL}, multiplayer support will be disabled.");
                    return;
                }

                var init = bootstrap.GetMethod(MP_INTEGRATION_INIT_METHOD, BindingFlags.Public | BindingFlags.Static);
                init?.Invoke(null, null);

                var mpApiType = mpapiAssembly.GetType(MPAPI_TYPE_NAME);
                var instanceProp = mpApiType?.GetProperty(MPAPI_INSTANCE_PROPERTY, BindingFlags.Public | BindingFlags.Static);

                _mpApiInstance = instanceProp!.GetValue(null);

                _isHost = _mpApiInstance.GetType().GetProperty(IS_HOST_PROPERTY, BindingFlags.Public | BindingFlags.Instance);

                IsInitialized = _mpApiInstance != null && _isHost != null;

                PJMain.Log("Multiplayer integration loaded successfully.");
            }
        }
        catch (Exception ex)
        {
            PJMain.Warning($"Failed to load {MP_INTEGRATION_DLL}, multiplayer support will be disabled.\r\n{ex.Message}\r\n{ex.StackTrace}");
        }
    }
}