using UnityEngine;

[CreateAssetMenu(fileName = "ClipGroup", menuName = "AudioManager/ClipGroup")]
public class ClipGroup : ScriptableObject
{
    public AudioClip[] Clips;
}
