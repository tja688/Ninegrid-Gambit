var arch = NineGrid.Core.NineGridArchitecture.Current;
var player = arch.GetModel<NineGrid.Core.PlayerModel>();
player.Coins.Value = 35;
NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance().SyncFromCore(animate: true);
return "gold set 35 displayed="+NineGrid.Flow.GoldGainFxManagerSingleton.Instance.DisplayedGold
  +" target="+NineGrid.Flow.GoldGainFxManagerSingleton.Instance.TargetGold
  +" presenting="+NineGrid.Flow.GoldGainFxManagerSingleton.Instance.IsPresenting;
