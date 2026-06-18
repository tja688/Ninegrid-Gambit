using System;

namespace NineGrid.Core
{
    public struct ModifierSource : IEquatable<ModifierSource>
    {
        public static readonly ModifierSource None = new ModifierSource("none");

        public readonly string Id;

        public ModifierSource(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Modifier source id cannot be empty.", "id");
            }

            Id = id;
        }

        public bool Equals(ModifierSource other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ModifierSource && Equals((ModifierSource)obj);
        }

        public override int GetHashCode()
        {
            return Id == null ? 0 : Id.GetHashCode();
        }

        public override string ToString()
        {
            return Id ?? string.Empty;
        }

        public static bool operator ==(ModifierSource left, ModifierSource right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModifierSource left, ModifierSource right)
        {
            return !left.Equals(right);
        }
    }
}
