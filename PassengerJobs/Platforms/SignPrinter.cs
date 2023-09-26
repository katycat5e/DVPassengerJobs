using DV.Signs;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PassengerJobs.Platforms
{
    public class SignPrinter : MonoBehaviour
    {
        public StationSignType SignType;
        private readonly List<SignRenderer> _renderers = new();

        private void TryCreateRenderer(string name, SignFormatter formatter)
        {
            TextMeshPro? tmp = transform.Find(name)?.GetComponent<TextMeshPro>();
            if (tmp)
            {
                _renderers.Add(new SignRenderer(tmp!, formatter));
            }
            else
            {
                PJMain.Warning($"Failed to find text area {name} on {gameObject.name}");
            }
        }

        public void Start()
        {
            switch (SignType)
            {
                case StationSignType.Small:
                    TryCreateRenderer("FrontText", SignFormatter.Compact);
                    TryCreateRenderer("RearText", SignFormatter.Compact);
                    TryCreateRenderer("FrontInfo", SignFormatter.PlatformInfo);
                    TryCreateRenderer("RearInfo", SignFormatter.PlatformInfo);
                    break;

                case StationSignType.Flatscreen:
                    TryCreateRenderer("FrontInfo", SignFormatter.PlatformInfo);
                    TryCreateRenderer("RearInfo", SignFormatter.PlatformInfo);
                    TryCreateRenderer("FrontText", SignFormatter.FullName);
                    TryCreateRenderer("RearText", SignFormatter.FullName);
                    break;

                case StationSignType.Lillys:
                    TryCreateRenderer("FrontInfo", SignFormatter.PlatformInfo);
                    TryCreateRenderer("RearInfo", SignFormatter.PlatformInfo);
                    TryCreateRenderer("FrontDest", SignFormatter.FullName);
                    TryCreateRenderer("RearDest", SignFormatter.FullName);
                    TryCreateRenderer("FrontIDs", SignFormatter.JobId);
                    TryCreateRenderer("RearIDs", SignFormatter.JobId);
                    break;
            }
        }

        public void UpdateDisplay(SignData data)
        {
            foreach (var renderer in _renderers)
            {
                renderer.Update(data);
            }
        }

        private class SignRenderer
        {
            private readonly TextMeshPro _textArea;
            private readonly SignFormatter _formatter;

            public SignRenderer(TextMeshPro textMesh, SignFormatter formatter)
            {
                _textArea = textMesh;
                _formatter = formatter;
            }

            public void Update(SignData signData)
            {
                _textArea.text = _formatter.Format(signData);
            }
        }
    }
}