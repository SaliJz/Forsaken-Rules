using UnityEngine;

[System.Serializable]
public class DialogLine
{
    public string CharacterName = "NAME NPC";

    public Sprite ProfileImage;

    [TextArea(3, 5)]
    public string Text = "Texto de la línea de diálogo.";

    [Header("Behavior")]
    public bool WaitForInput = true;
    public float AutoAdvanceDelay = 0.5f;

    [Header("Voice Settings")]
    public AudioClip VoiceClip;
    [Range(0.8f, 1.2f)]
    public float VoicePitch = 1f;
    [Range(1, 5)]
    public int VoiceFrequency = 2;
}