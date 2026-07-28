using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 盘面级 Avatar 朝向：指针在玩家右侧 → 朝右，左侧 → 朝左；与单卡 hover 无关。
    /// </summary>
    public sealed class AvatarBoardFacingController : PresentationController
    {
        private int _cachedAvatarUid = int.MinValue;
        private CardSpriteAnimPlayer _cachedAnimPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            AvatarBoardFacingHook.WireController = WireToField;
        }

        protected override void OnUnbind()
        {
            ClearAnimCache();
            AvatarBoardFacingState.Reset();
        }

        private void Update()
        {
            TickFacing();
        }

        private void TickFacing()
        {
            if (!TryResolveAvatar(out var avatar) || avatar.Transform == null)
            {
                return;
            }

            if (!WorldPointerUtility.TryGetPointerWorld(null, out var pointer))
            {
                return;
            }

            var facing = AvatarBoardFacingState.ResolveFromPointerX(
                pointer.x,
                avatar.Transform.position.x);
            AvatarBoardFacingState.SetFacing(facing);
            ApplyFacingVisual(avatar);
        }

        private bool TryResolveAvatar(out ManagedCard avatar)
        {
            avatar = null;
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            if (geometry != null && geometry.IsBound)
            {
                return geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out avatar)
                       && avatar != null
                       && avatar.CoreKind == CardPresentationKind.Avatar;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            return field != null
                   && field.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out avatar)
                   && avatar != null
                   && avatar.CoreKind == CardPresentationKind.Avatar;
        }

        private void ApplyFacingVisual(ManagedCard avatar)
        {
            if (_cachedAvatarUid != avatar.Uid || _cachedAnimPlayer == null)
            {
                _cachedAvatarUid = avatar.Uid;
                _cachedAnimPlayer = avatar.Transform.GetComponentInChildren<CardSpriteAnimPlayer>(true);
            }

            _cachedAnimPlayer?.SetMirrorX(AvatarBoardFacingState.MirrorX);
        }

        private void ClearAnimCache()
        {
            _cachedAvatarUid = int.MinValue;
            _cachedAnimPlayer = null;
        }

        private static void WireToField(GroundFieldView field)
        {
            if (field == null)
            {
                return;
            }

            var existing = field.GetComponentInChildren<AvatarBoardFacingController>(true);
            if (existing == null)
            {
                existing = Object.FindObjectOfType<AvatarBoardFacingController>();
            }

            if (existing == null)
            {
                var host = new GameObject(nameof(AvatarBoardFacingController));
                host.transform.SetParent(field.transform, false);
                existing = host.AddComponent<AvatarBoardFacingController>();
            }
        }
    }
}
