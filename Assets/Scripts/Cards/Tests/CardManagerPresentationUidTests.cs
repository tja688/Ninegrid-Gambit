using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class CardManagerPresentationUidTests
    {
        [Test]
        public void PresentationAndCoreUidAllocators_StartOnOppositeSigns()
        {
            var go = new GameObject("CardManagerPresentationUidTests");
            try
            {
                var manager = go.AddComponent<CardManagerSingleton>();
                Assert.AreEqual(-1, ReadPrivateInt(manager, "_nextPresentationUid"));
                Assert.AreEqual(1, ReadPrivateInt(manager, "_nextUid"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static int ReadPrivateInt(CardManagerSingleton manager, string fieldName)
        {
            var field = typeof(CardManagerSingleton).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return (int)field.GetValue(manager);
        }
    }
}
