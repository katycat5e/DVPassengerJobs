using System;
using System.IO;
using System.Reflection;

namespace PassengerJobs
{
    internal static class Bootstrapper
    {
        private static string ModPath => PJMain.ModEntry.Path;

        public static Assembly? TryLoadAssembly(string relativeDllPath)
        {
            try

            {
                string dllPath = Path.Combine(ModPath, relativeDllPath);
                if (!File.Exists(dllPath))
                {
                    PJMain.Warning($"Assembly file {relativeDllPath} was not found");
                    return null;
                }

                return Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                PJMain.Error($"Failed to load assembly from {relativeDllPath}", ex);
                return null;
            }
        }

        public static Type? TryLoadBootstrapClass(string relativeDllPath, string fullClassName)
        {
            try
            {
                var assembly = TryLoadAssembly(relativeDllPath);
                if (assembly is null) return null;

                return assembly.GetType(fullClassName);
            }
            catch (Exception ex)
            {
                PJMain.Error($"Failed to load bootstrap class {fullClassName} from {relativeDllPath}", ex);
                return null;
            }
        }

        public static MethodInfo? TryLoadInitializeMethod(string relativeDllPath, string fullClassName, string initMethodName = "Initialize")
        {
            try
            {
                var type = TryLoadBootstrapClass(relativeDllPath, fullClassName);
                if (type is null) return null;

                return type.GetMethod(initMethodName, BindingFlags.Public | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                PJMain.Error($"Failed to load method {initMethodName} on {fullClassName} from {relativeDllPath}", ex);
                return null;
            }
        }
    }
}