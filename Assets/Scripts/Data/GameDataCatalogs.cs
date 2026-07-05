using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>
    /// 运行时统一加载 ScriptableObject 数据目录（编辑器 AssetDatabase，构建后 FindObjectsOfTypeAll）。
    /// </summary>
    public static class GameDataCatalogs
    {
        const string OreCatalogPath = "Assets/ScriptableObjects/Data/OreCatalog.asset";
        const string HullModCatalogPath = "Assets/ScriptableObjects/Data/HullModCatalog.asset";
        const string EnemyShipCatalogPath = "Assets/ScriptableObjects/Data/EnemyShipCatalog.asset";

        static OreCatalog _ore;
        static HullModCatalog _hullMod;
        static EnemyShipCatalog _enemyShip;

        public static OreCatalog Ore => _ore ??= LoadCatalog<OreCatalog>(OreCatalogPath);
        public static HullModCatalog HullMod => _hullMod ??= LoadCatalog<HullModCatalog>(HullModCatalogPath);
        public static EnemyShipCatalog EnemyShip => _enemyShip ??= LoadCatalog<EnemyShipCatalog>(EnemyShipCatalogPath);

        static T LoadCatalog<T>(string assetPath) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var fromAssetDatabase = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (fromAssetDatabase != null)
            {
                return fromAssetDatabase;
            }
#endif
            var found = Resources.FindObjectsOfTypeAll<T>();
            return found.Length > 0 ? found[0] : null;
        }
    }
}
