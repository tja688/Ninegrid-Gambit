using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.UI
{
    public enum NoticeChannel
    {
        Notice = 0,
        Factory = 1,
    }

    [CreateAssetMenu(menuName = "NineGrid/UI/Notice Pool", fileName = "NoticePool")]
    public sealed class NoticePool : ScriptableObject
    {
        [SerializeField] List<NoticeMessage> messages = new();

        public IReadOnlyList<NoticeMessage> Messages => messages;

        public bool TryGet(string id, out NoticeMessage message)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                var entry = messages[i];
                if (entry != null && entry.Id == id)
                {
                    message = entry;
                    return true;
                }
            }

            message = null;
            return false;
        }
    }

    [Serializable]
    public sealed class NoticeMessage
    {
        [SerializeField] string id;
        [SerializeField] NoticeChannel channel = NoticeChannel.Notice;
        [TextArea(2, 4)]
        [SerializeField] string text;
        [Tooltip("自动隐藏秒数；<=0 表示不自动隐藏")]
        [SerializeField] float duration = 2f;

        public string Id => id;
        public NoticeChannel Channel => channel;
        public string Text => text;
        public float Duration => duration;
    }
}
