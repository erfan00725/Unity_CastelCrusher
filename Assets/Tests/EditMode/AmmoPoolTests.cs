using System.Collections.Generic;
using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class AmmoPoolTests
    {
        private AmmoPool<string> _pool;

        [SetUp]
        public void SetUp() => _pool = new AmmoPool<string>();

        [Test]
        public void Set_ThenGet_ReturnsCount()
        {
            _pool.Set("wood", 7);
            Assert.AreEqual(7, _pool.Get("wood"));
        }

        [Test]
        public void Get_UnknownKey_ReturnsZero()
        {
            Assert.AreEqual(0, _pool.Get("nothing"));
        }

        [Test]
        public void Has_MeansCountAboveZero()
        {
            _pool.Set("wood", 2);
            Assert.IsTrue(_pool.Has("wood"));

            _pool.Set("stone", 0);
            Assert.IsFalse(_pool.Has("stone"));       // exists but empty → not "has ammo"

            Assert.IsFalse(_pool.Has("never-added")); // unknown key → not "has ammo"
        }

        [Test]
        public void Consume_DecrementsAndReturnsNewValue()
        {
            _pool.Set("wood", 3);
            Assert.AreEqual(2, _pool.Consume("wood"));
            Assert.AreEqual(2, _pool.Get("wood"));
        }

        [Test]
        public void Consume_AtZero_ReturnsZeroAndStaysZero()
        {
            _pool.Set("wood", 0);
            Assert.AreEqual(0, _pool.Consume("wood"));
            Assert.AreEqual(0, _pool.Get("wood"));    // never goes negative
        }

        [Test]
        public void Consume_UnknownKey_ReturnsZeroWithoutThrowing()
        {
            Assert.DoesNotThrow(() => _pool.Consume("nothing"));
            Assert.AreEqual(0, _pool.Consume("nothing"));
        }

        [Test]
        public void Consume_RaisesOnChanged_WithNewValue()
        {
            string lastKey = null;
            int lastValue = -1;
            _pool.OnChanged += (key, value) => { lastKey = key; lastValue = value; };

            _pool.Set("wood", 2);
            Assert.AreEqual(("wood", 2), (lastKey, lastValue));   // Set raises too

            _pool.Consume("wood");
            Assert.AreEqual(("wood", 1), (lastKey, lastValue));
        }

        [Test]
        public void TotalRemaining_SumsAllKeys()
        {
            _pool.Set("wood", 2);
            _pool.Set("stone", 5);
            Assert.AreEqual(7, _pool.TotalRemaining());
        }

        [Test]
        public void CopyFrom_ReplacesContents()
        {
            _pool.Set("old", 9);
            _pool.CopyFrom(new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("wood", 3),
                new KeyValuePair<string, int>("stone", 1),
            });

            Assert.AreEqual(0, _pool.Get("old"));     // old contents are gone
            Assert.AreEqual(3, _pool.Get("wood"));
            Assert.AreEqual(4, _pool.TotalRemaining());
        }
    }
}