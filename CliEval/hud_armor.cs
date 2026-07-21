var arch = NineGrid.Core.NineGridArchitecture.Current;
var board = arch.GetModel<NineGrid.Core.BoardModel>();
var reg = arch.GetModel<NineGrid.Core.CardRegistry>();
reg.TryGet(board.AvatarUid.Value, out var avatar);
avatar.Stats.SetBase(NineGrid.Core.StatId.Armor, 7);
NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance().SyncFromCore(animate: true);
var armorTxt = new UnityEditor.SerializedObject(NineGrid.Flow.PlayerInfoHudPresenter.TryGetInstance()).FindProperty("armorText").objectReferenceValue as TMPro.TMP_Text;
return "armorTxt="+armorTxt.text;
