using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 盘面级 Avatar 朝向：指针相对玩家卡牌图标世界 X；与单卡 hover / 固定中线无关。
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

            // 以卡面图标当前世界位置为基准（跳格途中也会跟着动），不用盘面固定中线。
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
            var board = this.GetModel<BoardModel>()
                        ?? NineGridArchitecture.Interface?.GetModel<BoardModel>();

            // 优先：Core 当前 Avatar 占格（跳格后不再锁死格 5）。
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                var slot = board.AvatarSlot.Value.Index;
                if (geometry != null && geometry.IsBound
                    && geometry.TryGetCardAt(slot, out avatar)
                    && avatar != null
                    && avatar.CoreKind == CardPresentationKind.Avatar)
                {
                    return true;
                }

                var field = GroundFieldGeometryHook.FieldOrNull();
                if (field != null
                    && field.TryGetCardAt(slot, out avatar)
                    && avatar != null
                    && avatar.CoreKind == CardPresentationKind.Avatar)
                {
                    return true;
                }
            }

            // 回退：按 AvatarUid 反查表现占格。
            if (board != null && board.AvatarUid.Value > 0
                && geometry != null && geometry.IsBound
                && geometry.TryGetSlotOf(board.AvatarUid.Value, out var uidSlot)
                && geometry.TryGetCardAt(uidSlot, out avatar)
                && avatar != null
                && avatar.CoreKind == CardPresentationKind.Avatar)
            {
                return true;
            }

            return false;
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
