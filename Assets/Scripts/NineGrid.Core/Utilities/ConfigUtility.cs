using System;
using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core.Utilities
{
    public interface IConfigUtility : IUtility
    {
        void Set<T>(string key, T value);
        bool TryGet<T>(string key, out T value);
        T Get<T>(string key);
        void Clear();
    }

    public sealed class InMemoryConfigUtility : IConfigUtility
    {
        private readonly Dictionary<string, object> mValues = new Dictionary<string, object>();

        public void Set<T>(string key, T value)
        {
            mValues[BuildKey<T>(key)] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            object rawValue;
            if (mValues.TryGetValue(BuildKey<T>(key), out rawValue) && rawValue is T)
            {
                value = (T)rawValue;
                return true;
            }

            value = default(T);
            return false;
        }

        public T Get<T>(string key)
        {
            T value;
            if (TryGet(key, out value))
            {
                return value;
            }

            throw new KeyNotFoundException("Config not found: " + BuildKey<T>(key));
        }

        public void Clear()
        {
            mValues.Clear();
        }

        private static string BuildKey<T>(string key)
        {
            return typeof(T).FullName + "::" + (key ?? string.Empty);
        }
    }
}
