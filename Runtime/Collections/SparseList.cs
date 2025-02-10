using System;
using System.Collections;
using System.Collections.Generic;

public class SparseArray<T>
{
    // 元素或空闲链表节点的联合体
    private class ElementOrFreeListLink
    {
        public T ElementData; // 元素数据
        public int PrevFreeIndex; // 前一个空闲节点的索引
        public int NextFreeIndex; // 下一个空闲节点的索引
    }

    private List<ElementOrFreeListLink> _data; // 存储元素或空闲链表节点
    private BitArray _allocationFlags; // 标记每个索引是否被分配
    private int _firstFreeIndex = -1; // 空闲链表的头节点索引
    private int _numFreeIndices = 0; // 空闲链表的节点数量

    // 默认构造函数
    public SparseArray()
    {
        _data = new List<ElementOrFreeListLink>();
        _allocationFlags = new BitArray(0);
    }

    // 分配一个未初始化的元素，并返回分配信息
    public int AddUninitialized()
    {
        int index;
        if (_numFreeIndices > 0)
        {
            // 从空闲链表中取出一个索引
            index = _firstFreeIndex;
            _firstFreeIndex = _data[index].NextFreeIndex;
            _numFreeIndices--;

            if (_numFreeIndices > 0)
            {
                _data[_firstFreeIndex].PrevFreeIndex = -1;
            }
        }
        else
        {
            // 添加一个新元素
            index = _data.Count;
            _data.Add(new ElementOrFreeListLink());
            _allocationFlags.Length = index + 1;
        }

        // 标记索引为已分配
        _allocationFlags[index] = true;
        return index;
    }

    // 添加一个元素
    public int Add(T element)
    {
        int index = AddUninitialized();
        _data[index].ElementData = element;
        return index;
    }

    // 删除指定索引的元素
    public void RemoveAt(int index)
    {
        if (!_allocationFlags[index])
        {
            throw new ArgumentException("Index is not allocated.");
        }

        // 标记索引为未分配
        _allocationFlags[index] = false;

        // 将索引添加到空闲链表
        if (_numFreeIndices > 0)
        {
            _data[_firstFreeIndex].PrevFreeIndex = index;
        }

        _data[index].PrevFreeIndex = -1;
        _data[index].NextFreeIndex = _numFreeIndices > 0 ? _firstFreeIndex : -1;
        _firstFreeIndex = index;
        _numFreeIndices++;
    }

    // 获取或设置指定索引的元素
    public T this[int index]
    {
        get
        {
            if (!_allocationFlags[index])
            {
                throw new ArgumentException("Index is not allocated.");
            }
            return _data[index].ElementData;
        }
        set
        {
            if (!_allocationFlags[index])
            {
                throw new ArgumentException("Index is not allocated.");
            }
            _data[index].ElementData = value;
        }
    }

    // 检查索引是否有效
    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < _data.Count && _allocationFlags[index];
    }

    // 获取已分配元素的数量
    public int Count => _data.Count - _numFreeIndices;

    // 获取最大索引
    public int MaxIndex => _data.Count;

    // 压缩数组，将已分配的元素移动到数组的前部
    public void Compact()
    {
        int targetIndex = 0;
        for (int i = 0; i < _data.Count; i++)
        {
            if (_allocationFlags[i])
            {
                if (i != targetIndex)
                {
                    // 移动元素
                    _data[targetIndex] = _data[i];
                    _allocationFlags[targetIndex] = true;
                    _allocationFlags[i] = false;
                }
                targetIndex++;
            }
        }

        // 移除多余的元素
        _data.RemoveRange(targetIndex, _data.Count - targetIndex);
        _allocationFlags.Length = targetIndex;
        _firstFreeIndex = -1;
        _numFreeIndices = 0;
    }

    // 清空数组
    public void Clear()
    {
        _data.Clear();
        _allocationFlags.Length = 0;
        _firstFreeIndex = -1;
        _numFreeIndices = 0;
    }

    // 迭代器
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _data.Count; i++)
        {
            if (_allocationFlags[i])
            {
                yield return _data[i].ElementData;
            }
        }
    }
}
