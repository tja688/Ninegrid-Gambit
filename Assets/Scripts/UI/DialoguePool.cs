using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.UI
{
    [CreateAssetMenu(menuName = "NineGrid/UI/Dialogue Pool", fileName = "DialoguePool")]
    public sealed class DialoguePool : ScriptableObject
    {
        [Tooltip("说话人档案：名字唯一，立绘在此配置，台词里只填名字即可匹配。")]
        [SerializeField] List<DialogueSpeaker> speakers = new();

        [SerializeField] List<DialogueSequence> sequences = new();

        public IReadOnlyList<DialogueSpeaker> Speakers => speakers;
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

        public bool TryGetSpeaker(string speakerName, out DialogueSpeaker speaker)
        {
            if (string.IsNullOrEmpty(speakerName))
            {
                speaker = null;
                return false;
            }

            for (var i = 0; i < speakers.Count; i++)
            {
                var entry = speakers[i];
                if (entry != null && entry.Name == speakerName)
                {
                    speaker = entry;
                    return true;
                }
            }

            speaker = null;
            return false;
        }

        public Sprite ResolvePortrait(string speakerName)
        {
            return TryGetSpeaker(speakerName, out var speaker) ? speaker.Portrait : null;
        }
    }

    [Serializable]
    public sealed class DialogueSpeaker
    {
        [Tooltip("唯一名字，台词 speakerName 与此精确匹配。")]
        [SerializeField] string name;
        [SerializeField] Sprite portrait;

        public string Name => name;
        public Sprite Portrait => portrait;
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
        [Tooltip("说话人名字，须与对话池 Speakers 中的唯一名字一致。")]
        [SerializeField] string speakerName;
        [TextArea(2, 6)]
        [SerializeField] string text;

        public string SpeakerName => speakerName;
        public string Text => text;
    }
}
