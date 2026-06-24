namespace NineGrid.Presentation.FSM
{
    public enum SelectionOverlaySessionKind
    {
        None,
        RewardChoice,
        RoomChoice,
        EnterRoom,
        ItemOption,
    }

    public sealed class SelectionOverlayOptionDescriptor
    {
        public SelectionOverlayOptionDescriptor(string label, string payload = null, bool isPass = false)
        {
            Label = label ?? string.Empty;
            Payload = payload ?? string.Empty;
            IsPass = isPass;
        }

        public string Label { get; private set; }
        public string Payload { get; private set; }
        public bool IsPass { get; private set; }
    }
}
