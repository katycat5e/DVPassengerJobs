using DV.UI;
using DV.UIFramework;
using DV.Utils;
using HarmonyLib;
using PassengerJobs.Injectors;
using System.Collections;

namespace PassengerJobs.Patches
{
    [HarmonyPatch(typeof(WorldStreamingInit))]
    public static class WorldStreamingInitLoadingRoutinePatch
    {
        [HarmonyPatch(nameof(WorldStreamingInit.LoadingRoutine))]
        [HarmonyPostfix]
        static IEnumerator ShowMessages(IEnumerator __result)
        {
            while (__result.MoveNext())
            {
                yield return __result.Current;
            }

            foreach (LocalizationKey messageKey in SaveDataInjector.PostLoadMessageKeys)
            {
                while (!SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.PopupManager.CanShowPopup())
                {
                    yield return null;
                }

                yield return WaitFor.Seconds(2f);

                Popup popupPrefab = SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.uiReferences.popupOk;
                PopupLocalizationKeys localizationKeys = new() { labelKey = messageKey.K() };
                SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.PopupManager.ShowPopup(popupPrefab, localizationKeys, null, false);
            }
        }
    }
}
