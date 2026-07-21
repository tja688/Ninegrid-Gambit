var arch = NineGrid.Core.NineGridArchitecture.Current;
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
reg.TryGet(board.AvatarUid.Value, out var avatar);
avatar.Stats.SetBase(NineGrid.Core.StatId.MaxHp, 20);
avatar.Stats.SetBase(NineGrid.Core.StatId.Hp, 20);
NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance().SyncFromCore(animate: true);
return "grow to 20/20";
