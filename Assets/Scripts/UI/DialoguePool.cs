using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.UI
{
    [CreateAssetMenu(menuName = "NineGrid/UI/Dialogue Pool", fileName = "DialoguePool")]
    public sealed class DialoguePool : ScriptableObject
    {
        [SerializeField] List<DialogueSequence> sequences = new();

        public IReadOnlyList<DialogueSequence> Sequences => sequences;

        public bool TryGet(string id, out DialogueSequence sequence)
        {
            for (var i = 0; i < sequences.Count; i++)
            {
                var entry = sequences[i];
                if (entry != null && entry.Id == id)
                {
                    sequence = entry;
                    return true;
                }
            }

            sequence = null;
            return false;
        }
    }

    [Serializable]
    public sealed class DialogueSequence
    {
        [SerializeField] string id;
        [SerializeField] List<DialogueLine> lines = new();

        public string Id => id;
        public IReadOnlyList<DialogueLine> Lines => lines;
    }

    [Serializable]
    public sealed class DialogueLine
    {
        [Tooltip("说话人物区分标签，如 Player / Captain")]
        [SerializeField] string speakerTag;
        [SerializeField] Sprite portrait;
        [TextArea(2, 6)]
        [SerializeField] string text;

        public string SpeakerTag => speakerTag;
        public Sprite Portrait => portrait;
        public string Text => text;
    }
}
