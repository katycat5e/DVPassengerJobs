using DV.Simulation.Cars;
using DV.ThingTypes;
using System.Collections.Generic;
using UnityEngine;

namespace PassengerJobs
{
    internal static class LampHelper
    {
        private static Dictionary<Material, Material> s_cache = new();
        private static TrainCarLivery? _de2;
        private static TrainCarLivery? _pax;
        private static Headlight? s_redGlare;
        private static MeshRenderer? s_redLamp;
        private static Material? s_litMat;
        private static Color? s_litColor;
        private static Texture2D? s_smallLit;

        public static TrainCarLivery DE2
        {
            get
            {
                if (_de2 == null)
                {
                    DV.Globals.G.Types.TryGetLivery("LocoDE2", out _de2);
                }

                return _de2;
            }
        }
        public static TrainCarLivery Passenger
        {
            get
            {
                if (_pax == null)
                {
                    DV.Globals.G.Types.TryGetLivery("PassengerBlue", out _pax);
                }

                return _pax;
            }
        }
        public static Headlight RedGlare
        {
            get
            {
                if (s_redGlare == null)
                {
                    s_redGlare = DE2.prefab.transform.Find(
                        "[headlights_de2]/FrontSide/HeadlightTop").GetComponent<Headlight>();
                }

                return s_redGlare;
            }
        }
        public static MeshRenderer RedLamp
        {
            get
            {
                if (s_redLamp == null)
                {
                    s_redLamp = DE2.prefab.transform.Find(
                        "[headlights_de2]/FrontSide/ext headlights_glass_red_F").GetComponent<MeshRenderer>();
                }

                return s_redLamp;
            }
        }
        public static Material RedLitMaterial => RedGlare.emissionMaterialLit;
        public static Material RedUnlitMaterial => RedGlare.emissionMaterialUnlit;
        public static Material PassengerUnlit => Passenger.prefab.transform.Find("CarPassenger/CarPassengerInterior_LOD0")
            .GetComponent<MeshRenderer>().sharedMaterials[3];
        public static Material PassengerLit
        {
            get
            {
                if (s_litMat == null)
                {
                    s_litMat = GetLitMaterialFromModular(PassengerUnlit);
                }

                return s_litMat;
            }
        }
        public static Color LitColour
        {
            get
            {
                if (PJMain.Settings.UseCustomCoachLightColour)
                {
                    return PJMain.Settings.CustomCoachLightColour;
                }

                if (!s_litColor.HasValue)
                {
                    s_litColor = DE2.prefab.GetComponentInChildren<CabLightsController>().lightsLit.GetColor("_EmissionColor") * 0.6f;
                }

                return s_litColor.Value;
            }
        }
        public static Texture2D SmallLitTexture
        {
            get
            {
                if (s_smallLit == null)
                {
                    s_smallLit = new Texture2D(1, 2)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Point
                    };

                    s_smallLit.SetPixels(new[] { Color.white, Color.black });
                    s_smallLit.Apply();
                }

                return s_smallLit;
            }
        }

        public static Material GetLitMaterialFromModular(Material unlit)
        {
            if (s_cache.TryGetValue(unlit, out var lit) && lit != null)
            {
                return lit;
            }

            var mainTex = unlit.GetTexture("_t1");
            var mso = unlit.GetTexture("_t1_mso");

            // Because Unity may compile multiple different versions of the same shader based
            // on the active features, use a material that is know to have all required parts
            // (metallic, smoothness, normals and emission) active.
            lit = new Material(RedLitMaterial);

            lit.SetTexture("_MainTex", mainTex);
            lit.SetTexture("_MetallicGlossMap", mso);
            lit.SetTexture("_BumpMap", unlit.GetTexture("_t1_normal"));
            lit.SetTexture("_OcclusionMap", mso);
            lit.SetTexture("_EmissionMap", SmallLitTexture);

            lit.SetFloat("_Glossiness", 0.5f);
            lit.SetFloat("_BumpScale", 0.5f);
            lit.SetFloat("_OcclusionStrength", 0.5f);
            lit.SetColor("_EmissionColor", LitColour);

            lit.color = Color.white;
            lit.name = unlit.name;

            return lit;
        }

        private static Texture2D CreateLitTexture(Texture original)
        {
            int width = original.width;
            int height = original.height;
            var tex = new Texture2D(width, height);

            // I want a texture copy and I want it painted black.
            Graphics.CopyTexture(original, tex);

            for (int y = height / 2; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, Color.black);
                }
            }

            tex.Apply();

            return tex;
        }

        public static bool RemoveFromCache(Material unlit)
        {
            return s_cache.Remove(unlit);
        }
    }
}