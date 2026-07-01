namespace NineGrid.Presentation.Orchestration
{
    public interface IReactionBinding
    {
        ReactionId Id { get; }
        void Play(IViewRegistry registry, FlowPayload payload);
        void Stop();
    }
}
