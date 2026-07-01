using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugCatalog
    {
        private readonly List<IPerformanceDebugModule> modules = new();

        public IReadOnlyList<IPerformanceDebugModule> Modules => modules;

        public static PerformanceDebugCatalog Discover()
        {
            var catalog = new PerformanceDebugCatalog();
            catalog.LoadFromAssembly(typeof(PerformanceDebugCatalog).Assembly);
            catalog.RegisterBatchFixtures();
            return catalog;
        }

        public void LoadFromAssembly(Assembly assembly)
        {
            modules.Clear();
            if (assembly == null)
            {
                return;
            }

            IEnumerable<Type> types = assembly
                .GetTypes()
                .Where(type => !type.IsAbstract
                    && typeof(IPerformanceDebugModule).IsAssignableFrom(type)
                    && type.GetConstructor(Type.EmptyTypes) != null);

            foreach (Type type in types)
            {
                try
                {
                    if (Activator.CreateInstance(type) is IPerformanceDebugModule module)
                    {
                        modules.Add(module);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PerformanceDebugCatalog] Failed to create module {type.Name}: {ex.Message}");
                }
            }

            modules.Sort((a, b) =>
            {
                int category = a.Category.CompareTo(b.Category);
                return category != 0 ? category : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });
        }

        public IPerformanceDebugModule FindById(string id)
        {
            for (var i = 0; i < modules.Count; i++)
            {
                if (modules[i].Id == id)
                {
                    return modules[i];
                }
            }

            return null;
        }

        public IReadOnlyList<IPerformanceDebugModule> GetByCategory(PerformanceDebugCategory category)
        {
            return modules.Where(module => module.Category == category).ToList();
        }

        public void RegisterBatchFixtures()
        {
            IReadOnlyList<BatchFixtureEntry> fixtures = PresentationBatchFixtureLibrary.All;
            for (var i = 0; i < fixtures.Count; i++)
            {
                modules.Add(new Modules.BatchFixtureDebugModule(fixtures[i]));
            }

            modules.Sort((a, b) =>
            {
                int category = a.Category.CompareTo(b.Category);
                return category != 0 ? category : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });
        }
    }
}
