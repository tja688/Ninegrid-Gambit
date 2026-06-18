namespace NineGrid.Core
{
    public sealed class CoreCommandResult
    {
        private CoreCommandResult(bool accepted, string reason, int resolvedActions)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            ResolvedActions = resolvedActions;
        }

        public bool Accepted { get; private set; }
        public string Reason { get; private set; }
        public int ResolvedActions { get; private set; }

        public static CoreCommandResult Accept(int resolvedActions)
        {
            return new CoreCommandResult(true, string.Empty, resolvedActions);
        }

        public static CoreCommandResult Reject(string reason)
        {
            return new CoreCommandResult(false, reason, 0);
        }
    }
}
