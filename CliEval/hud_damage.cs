var arch = NineGrid.Core.NineGridArchitecture.Current;
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
var uid = board.AvatarUid.Value;
reg.TryGet(uid, out var avatar);
avatar.Stats.SetBase(NineGrid.Core.StatId.MaxHp, 10);
avatar.Stats.SetBase(NineGrid.Core.StatId.Hp, 5);
NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance().SyncFromCore(animate: true);
return "damaged to 5/10";
