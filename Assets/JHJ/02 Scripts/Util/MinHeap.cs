using System;
using System.Collections.Generic;

public class MinHeap<T, TPriority> where TPriority : IComparable<TPriority>
{
    private struct HeapNode
    {
        public T Item;
        public TPriority Priority;
    }

    private List<HeapNode> heap = new List<HeapNode>();

    public int Count => heap.Count;

    // 힙을 재사용할 때 내부 리스트만 비움 (백킹 배열의 Capacity는 유지되어 재할당 없이 재사용 가능)
    public void Clear()
    {
        heap.Clear();
    }

    public void Enqueue(T item, TPriority priority)
    {
        HeapNode node = new HeapNode { Item = item, Priority = priority  };
        heap.Add(node);
        int index = heap.Count - 1;

        while (index > 0 && heap[index].Priority.CompareTo(heap[(index - 1) / 2].Priority) < 0)
        {
            int parentIndex = (index - 1) / 2;

            Swap(index, parentIndex);

            index = parentIndex;
        }
    }

    public T Dequeue()
    {

        if (heap.Count == 0)
        {
            throw new InvalidOperationException("힙이 비어있음");
        }

        T result = heap[0].Item;

        int lastIndex = heap.Count - 1;
        heap[0] = heap[lastIndex];
        heap.RemoveAt(lastIndex);

        int index = 0;
        int count = heap.Count;

        while (true)
        {
            int leftChild = 2 * index + 1;
            int rightChild = 2 * index + 2;
            int smallest = index;

            if (leftChild < count && heap[leftChild].Priority.CompareTo(heap[smallest].Priority) < 0)
            {
                smallest = leftChild;
            }

            if (rightChild < count && heap[rightChild].Priority.CompareTo(heap[smallest].Priority) < 0)
            {
                smallest = rightChild;
            }

            if (smallest == index)
            {
                break;
            }

            Swap(index, smallest);

            index = smallest;
        }
        return result;
    }

    public bool TryPeek(out T item, out TPriority priority)
    {
        if(heap.Count == 0)
        {
            item = default;
            priority = default;
            return false;
        }

        item = heap[0].Item;
        priority = heap[0].Priority;
        return true;
    }

    private void Swap(int a, int b)
    {
        HeapNode tmp = heap[a];
        heap[a] = heap[b];
        heap[b] = tmp;
    }
}
