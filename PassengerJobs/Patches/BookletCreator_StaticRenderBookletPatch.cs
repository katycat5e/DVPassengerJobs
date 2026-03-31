using DV.Booklets;
using DV.Booklets.Rendered;
using DV.RenderTextureSystem;
using DV.RenderTextureSystem.BookletRender;
using DV.Utils;
using HarmonyLib;
using PassengerJobs.Injectors;
using System;
using System.Reflection;
using UnityEngine;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(BookletCreator_StaticRenderBooklet))]
    internal static class BookletCreator_StaticRenderBookletPatch
    {
        private static readonly MethodInfo _resourcesLoadMethod =
            AccessTools.Method(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Type) });

        [HarmonyPatch(nameof(BookletCreator_StaticRenderBooklet.Render))]
        [HarmonyPrefix]
        public static bool RenderPrefix(GameObject existingBooklet, string renderPrefabName, ref RenderedTexturesBase __result)
        {
            GameObject renderPrefab;
            if (renderPrefabName == LicenseInjector.License1Data.RenderPrefabName)
            {
                renderPrefab = LicenseInjector.License1Data.RenderPrefab;
            }
            else if (renderPrefabName == LicenseInjector.License1Data.SampleRenderPrefabName)
            {
                renderPrefab = LicenseInjector.License1Data.SampleRenderPrefab;
            }
            else if (renderPrefabName == LicenseInjector.License2Data.RenderPrefabName)
            {
                renderPrefab = LicenseInjector.License2Data.RenderPrefab;
            }
            else if (renderPrefabName == LicenseInjector.License2Data.SampleRenderPrefabName)
            {
                renderPrefab = LicenseInjector.License2Data.SampleRenderPrefab;
            }
            else
            {
                renderPrefab = Resources.Load<GameObject>(renderPrefabName);
            }

            var instantiated = UnityEngine.Object.Instantiate(renderPrefab, SingletonBehaviour<RenderTextureSystem>.Instance.transform.position, Quaternion.identity);
            instantiated.SetActive(true);
            var staticRenderBase = instantiated.GetComponent<StaticTextureRenderBase>();
            var renderedTextures = existingBooklet.GetComponent<RenderedTexturesBase>();

            renderedTextures.RegisterTexturesGeneratedEvent(staticRenderBase);
            staticRenderBase.GenerateStaticPagesTextures();

            if (staticRenderBase is StaticLicenseBookletRender lbr && lbr.jobLicense)
            {
                PJMain.Log($"Render job license {renderPrefabName} icon {lbr.jobLicense.icon}");
            }
            
            __result = renderedTextures;

            return false;
        }
    }
}
