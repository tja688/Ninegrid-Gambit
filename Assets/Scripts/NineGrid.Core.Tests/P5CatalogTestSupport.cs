using System;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Core.Tests
{
    internal static class P5CatalogTestSupport
    {
        private static GameContentCatalog sCatalog;

        public static GameContentCatalog Catalog
        {
            get { return sCatalog ?? (sCatalog = TableNineContentCatalog.CreateDefault()); }
        }

        public static void RegisterCatalog(IArchitecture architecture)
        {
            RegisterCatalog(architecture.GetUtility<IConfigUtility>(), Catalog);
            architecture.GetSystem<IContentSystem>().Load(Catalog);
        }

        public static void RegisterCatalog(IConfigUtility configUtility, GameContentCatalog catalog)
        {
            configUtility.Set(ContentConfigKeys.DefaultCatalog, catalog);
        }

        public static string RequireEffectJson(string effectId)
        {
            ContentEffectDefinition effect;
            if (!Catalog.TryGetEffect(effectId, out effect))
            {
                throw new InvalidOperationException("Missing catalog effect: " + effectId);
            }

            if (effect.State != ContentImplementationState.Implemented)
            {
                throw new InvalidOperationException("Catalog effect is not implemented: " + effectId);
            }

            return effect.Json;
        }

        public static EffectInstance ActivateCatalogEffect(IArchitecture architecture, string effectId, EffectOwner owner)
        {
            var effectSystem = architecture.GetSystem<IEffectSystem>();
            return effectSystem.Activate(effectSystem.ParseJson(RequireEffectJson(effectId)), owner);
        }

        public static void ActivateWoodSet(IArchitecture architecture)
        {
            var player = architecture.GetModel<PlayerModel>();
            player.AddRelic("relic.wood_shield");
            player.AddRelic("relic.wood_sword");
            player.AddRelic("relic.wood_armor");

            ActivateCatalogEffect(architecture, "relic.wood_shield.base", new EffectOwner(EffectContainerType.Relic, "relic.wood_shield", 0));
            ActivateCatalogEffect(architecture, "relic.wood_sword.base", new EffectOwner(EffectContainerType.Relic, "relic.wood_sword", 0));
            ActivateCatalogEffect(architecture, "relic.wood_armor.base", new EffectOwner(EffectContainerType.Relic, "relic.wood_armor", 0));
            ActivateCatalogEffect(architecture, "relic.wood_sword.set", new EffectOwner(EffectContainerType.Relic, "relic.wood_sword", 0));
            ActivateCatalogEffect(architecture, "relic.wood_shield.set", new EffectOwner(EffectContainerType.Relic, "relic.wood_shield", 0));
            ActivateCatalogEffect(architecture, "relic.wood_armor.set", new EffectOwner(EffectContainerType.Relic, "relic.wood_armor", 0));
        }
    }
}
