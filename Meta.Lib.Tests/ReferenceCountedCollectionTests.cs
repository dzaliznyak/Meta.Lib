using Meta.Lib.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Meta.Lib.Tests
{
    [TestClass]
    public class ReferenceCountedCollectionTests
    {
        [TestMethod]
        public void Add_IncreasesReferenceCount()
        {
            // Arrange
            var collection = new ReferenceCountedCollection<string>();

            // Act
            collection.Add("foo");

            // Assert
            Assert.AreEqual(1, collection.GetReferenceCount("foo"));
        }

        [TestMethod]
        public void Add_MultipleTimes_IncrementsReferenceCount()
        {
            // Arrange
            var collection = new ReferenceCountedCollection<string>();

            // Act
            collection.Add("foo");
            collection.Add("foo");

            // Assert
            Assert.AreEqual(2, collection.GetReferenceCount("foo"));
        }

        [TestMethod]
        public void Remove_DecreasesReferenceCount()
        {
            // Arrange
            var collection = new ReferenceCountedCollection<string>();
            collection.Add("foo");
            collection.Add("foo");

            // Act
            collection.Remove("foo");

            // Assert
            Assert.AreEqual(1, collection.GetReferenceCount("foo"));
        }

        [TestMethod]
        public void Remove_RemovesItemWhenReferenceCountIsZero()
        {
            // Arrange
            var collection = new ReferenceCountedCollection<string>();
            collection.Add("foo");

            // Act
            collection.Remove("foo");

            // Assert
            Assert.IsFalse(collection.GetItems().Contains("foo"));
        }

        [TestMethod]
        public void GetItems_ReturnsAllItemsWithNonZeroReferenceCounts()
        {
            // Arrange
            var collection = new ReferenceCountedCollection<string>();
            collection.Add("foo");
            collection.Add("bar");
            collection.Remove("foo");

            // Act
            var items = collection.GetItems();

            // Assert
            Assert.AreEqual(1, items.Count());
            Assert.IsTrue(items.Contains("bar"));
        }
    }
}