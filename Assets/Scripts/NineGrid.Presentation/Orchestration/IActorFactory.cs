using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    public interface IActorFactory
    {
        Transform Spawn(string defId, int cardUid, Transform parent = null);
        void Despawn(int cardUid);
        bool TryGet(int cardUid, out Transform actor);
    }
}
