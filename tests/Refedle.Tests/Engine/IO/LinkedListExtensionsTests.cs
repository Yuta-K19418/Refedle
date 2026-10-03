using AwesomeAssertions;
using Refedle.Engine.IO;

namespace Refedle.Tests.Engine.IO;

public sealed class LinkedListExtensionsTests
{
    [Fact]
    public void MoveToFront_WhenNodeIsAlreadyAtHead_ListRemainsUnchanged()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        var headNode = list.AddLast(new CacheEntry<int> { RowIndex = 1, Value = 10 });
        list.AddLast(new CacheEntry<int> { RowIndex = 2, Value = 20 });

        // Act
        list.MoveToFront(headNode);

        // Assert
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(1);
        list.Last.Should().NotBeNull();
        list.Last.Value.RowIndex.Should().Be(2);
        list.Count.Should().Be(2);
    }

    [Fact]
    public void MoveToFront_WithSingleElementList_ListRemainsUnchanged()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        var onlyNode = list.AddLast(new CacheEntry<int> { RowIndex = 1, Value = 10 });

        // Act
        list.MoveToFront(onlyNode);

        // Assert
        list.Count.Should().Be(1);
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(1);
    }

    [Fact]
    public void MoveToFront_WhenNodeIsInMiddle_MovesToHead()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        list.AddLast(new CacheEntry<int> { RowIndex = 1, Value = 10 });
        var middleNode = list.AddLast(new CacheEntry<int> { RowIndex = 2, Value = 20 });
        list.AddLast(new CacheEntry<int> { RowIndex = 3, Value = 30 });

        // Act
        list.MoveToFront(middleNode);

        // Assert
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(2);

        list.First.Next.Should().NotBeNull();
        list.First.Next.Value.RowIndex.Should().Be(1);

        list.Last.Should().NotBeNull();
        list.Last.Value.RowIndex.Should().Be(3);
    }

    [Fact]
    public void MoveToFront_WhenNodeIsAtTail_MovesToHead()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        list.AddLast(new CacheEntry<int> { RowIndex = 1, Value = 10 });
        list.AddLast(new CacheEntry<int> { RowIndex = 2, Value = 20 });
        var tailNode = list.AddLast(new CacheEntry<int> { RowIndex = 3, Value = 30 });

        // Act
        list.MoveToFront(tailNode);

        // Assert
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(3);
        list.First.Next.Should().NotBeNull();
        list.First.Next.Value.RowIndex.Should().Be(1);
        list.Last.Should().NotBeNull();
        list.Last.Value.RowIndex.Should().Be(2);
    }

    [Fact]
    public void ReuseTail_WithSingleElementList_NodeIsReusedAtFront()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        var cache = new Dictionary<long, LinkedListNode<CacheEntry<int>>>();
        var node1 = new LinkedListNode<CacheEntry<int>>(new CacheEntry<int> { RowIndex = 1, Value = 10 });
        list.AddLast(node1);
        cache[1] = node1;

        // Act
        list.ReuseTail(cache, rowIndex: 2, rowValue: 20, emptyValue: -1);

        // Assert
        list.Count.Should().Be(1);
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(2);
        list.First.Value.Value.Should().Be(20);
        cache.ContainsKey(1).Should().BeFalse();
        cache.ContainsKey(2).Should().BeTrue();
    }

    [Fact]
    public void ReuseTail_WithMultiElementList_EvictsTailAndMovesNewNodeToFront()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        var cache = new Dictionary<long, LinkedListNode<CacheEntry<int>>>();

        var node1 = new LinkedListNode<CacheEntry<int>>(new CacheEntry<int> { RowIndex = 1, Value = 10 });
        var node2 = new LinkedListNode<CacheEntry<int>>(new CacheEntry<int> { RowIndex = 2, Value = 20 });

        list.AddLast(node1);
        list.AddLast(node2);
        cache[1] = node1;
        cache[2] = node2;

        // Act
        list.ReuseTail(cache, rowIndex: 3, rowValue: 30, emptyValue: -1);

        // Assert
        cache.ContainsKey(2).Should().BeFalse(); // RowIndex 2 was at tail and should be evicted
        cache.ContainsKey(3).Should().BeTrue();
        list.First.Should().NotBeNull();
        list.First.Should().BeSameAs(cache[3]);
        list.First.Value.RowIndex.Should().Be(3);
        list.First.Value.Value.Should().Be(30);

        list.Last.Should().NotBeNull();
        list.Last.Value.RowIndex.Should().Be(1);
        list.Count.Should().Be(2);
    }

    [Fact]
    public void ReuseTail_WithRowIndexBeyondIntMaxValue_PreservesLongKeyAndRowIndex()
    {
        // Arrange - 2_147_483_648 (int.MaxValue + 1) must not narrow anywhere
        // in the node-reuse path.
        var list = new LinkedList<CacheEntry<int>>();
        var cache = new Dictionary<long, LinkedListNode<CacheEntry<int>>>();
        list.AddNew(cache, rowIndex: 1, rowValue: 10);
        var rowIndex = 2_147_483_648L;

        // Act
        list.ReuseTail(cache, rowIndex, rowValue: 20, emptyValue: -1);

        // Assert
        cache.ContainsKey(1).Should().BeFalse();
        cache.ContainsKey(rowIndex).Should().BeTrue();
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(rowIndex);
        list.First.Value.Value.Should().Be(20);
    }

    [Fact]
    public void AddNew_WithEmptyList_AddsNodeToFrontAndUpdatesDictionary()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        var cache = new Dictionary<long, LinkedListNode<CacheEntry<int>>>();

        // Act
        list.AddNew(cache, rowIndex: 1, rowValue: 10);

        // Assert
        list.Should().HaveCount(1);
        cache.ContainsKey(1).Should().BeTrue();
        list.First.Should().NotBeNull();
        list.First.Should().BeSameAs(cache[1]);
        list.First.Value.RowIndex.Should().Be(1);
        list.First.Value.Value.Should().Be(10);
    }

    [Fact]
    public void AddNew_WhenListIsNotEmpty_NewNodeIsAtFront()
    {
        // Arrange
        var list = new LinkedList<CacheEntry<int>>();
        var cache = new Dictionary<long, LinkedListNode<CacheEntry<int>>>();
        list.AddNew(cache, rowIndex: 1, rowValue: 10);

        // Act
        list.AddNew(cache, rowIndex: 2, rowValue: 20);

        // Assert
        list.Count.Should().Be(2);
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(2);
        list.Last.Should().NotBeNull();
        list.Last.Value.RowIndex.Should().Be(1);
    }

    [Fact]
    public void AddNew_WithRowIndexBeyondIntMaxValue_PreservesLongKeyAndRowIndex()
    {
        // Arrange - 2_147_483_648 (int.MaxValue + 1) must stay a long in both
        // the dictionary key and the cached entry.
        var list = new LinkedList<CacheEntry<int>>();
        var cache = new Dictionary<long, LinkedListNode<CacheEntry<int>>>();
        var rowIndex = 2_147_483_648L;

        // Act
        list.AddNew(cache, rowIndex, rowValue: 10);

        // Assert
        cache.ContainsKey(rowIndex).Should().BeTrue();
        list.First.Should().NotBeNull();
        list.First.Value.RowIndex.Should().Be(rowIndex);
        list.First.Value.Value.Should().Be(10);
    }
}
