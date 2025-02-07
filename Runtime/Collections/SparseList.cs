using System;
using System.Collections;
using System.Collections.Generic;

public class SparseArray<T>
{
    private class ElementOrFreeListLink
    {
        public T Element;
        public int PrevFreeIndex;
        public int NextFreeIndex;
    }

    private List<ElementOrFreeListLink> data;
    private BitArray allocationFlags;
    private int firstFreeIndex = -1;
    private int numFreeIndices = 0;

    public SparseArray(int capacity = 0)
    {
        data = new List<ElementOrFreeListLink>(capacity);
        allocationFlags = new BitArray(capacity);
    }

    public int Add(T item)
    {
        int index;
        if (numFreeIndices > 0)
        {
            index = firstFreeIndex;
            var link = data[firstFreeIndex];
            firstFreeIndex = link.NextFreeIndex;
            if (firstFreeIndex != -1)
            {
                data[firstFreeIndex].PrevFreeIndex = -1;
            }
            numFreeIndices--;
        }
        else
        {
            index = data.Count;
            data.Add(new ElementOrFreeListLink());
            allocationFlags.Length = data.Count;
        }

        data[index] = new ElementOrFreeListLink { Element = item };
        allocationFlags[index] = true;
        return index;
    }

    public void RemoveAt(int index)
    {
        if (!IsValidIndex(index))
            throw new ArgumentOutOfRangeException(nameof(index));

        // 将位置添加到空闲列表
        data[index] = new ElementOrFreeListLink
        {
            PrevFreeIndex = -1,
            NextFreeIndex = firstFreeIndex
        };

        if (firstFreeIndex != -1)
        {
            data[firstFreeIndex].PrevFreeIndex = index;
        }

        firstFreeIndex = index;
        allocationFlags[index] = false;
        numFreeIndices++;
    }

    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < data.Count && allocationFlags[index];
    }

    public T this[int index]
    {
        get
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index));
            return data[index].Element;
        }
        set
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index));
            data[index].Element = value;
        }
    }

    public int Count => data.Count - numFreeIndices;

    public void Clear()
    {
        data.Clear();
        allocationFlags = new BitArray(0);
        firstFreeIndex = -1;
        numFreeIndices = 0;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < data.Count; i++)
        {
            if (allocationFlags[i])                       
            {
                yield return data[i].Element;
            }
        }
    }
}