var gt = new UnityEditor.SerializedObject(NineGrid.Flow.GoldGainFxManagerSingleton.Instance).FindProperty("goldText").objectReferenceValue as TMPro.TMP_Text;
return "goldText="+gt.text+" displayed="+NineGrid.Flow.GoldGainFxManagerSingleton.Instance.DisplayedGold+" presenting="+NineGrid.Flow.GoldGainFxManagerSingleton.Instance.IsPresenting;
