using TMPro;
using UnityEngine;

public class RainbowText : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private string substring = "Remake";

    [Header("Wave")]
    [SerializeField] private float cycleSpeed = 1f;
    [SerializeField] private float huePerCharacter = 0.08f;
    [SerializeField, Range(0f, 1f)] private float saturation = 1f;
    [SerializeField, Range(0f, 1f)] private float brightness = 1f;

    private int _startIndex = -1;
    private int _length;

    private void Start()
    {
        if (targetText == null) targetText = GetComponent<TMP_Text>();
        _startIndex = targetText.text.IndexOf(substring, System.StringComparison.Ordinal);
        _length = substring.Length;
    }

    private void Update()
    {
        if (_startIndex < 0) return;

        targetText.ForceMeshUpdate();
        TMP_TextInfo textInfo = targetText.textInfo;

        for (int i = 0; i < _length; i++)
        {
            int charIndex = _startIndex + i;
            if (charIndex >= textInfo.characterCount) break;

            TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];
            if (!charInfo.isVisible) continue;

            float hue = Mathf.Repeat(Time.time * cycleSpeed + i * huePerCharacter, 1f);
            Color32 color = Color.HSVToRGB(hue, saturation, brightness);

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Color32[] vertexColors = textInfo.meshInfo[materialIndex].colors32;

            vertexColors[vertexIndex + 0] = color;
            vertexColors[vertexIndex + 1] = color;
            vertexColors[vertexIndex + 2] = color;
            vertexColors[vertexIndex + 3] = color;
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.colors32 = textInfo.meshInfo[i].colors32;
            targetText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}
