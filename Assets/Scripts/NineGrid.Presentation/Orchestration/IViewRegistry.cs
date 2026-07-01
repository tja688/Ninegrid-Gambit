using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    public interface IViewRegistry
    {
        Transform ResolveActor(int cardUid);
        Transform ResolveAnchor(SlotId slot);
        void RegisterActor(int cardUid, Transform actor);
        void RegisterAnchor(SlotId slot, Transform anchor);
    }
}
