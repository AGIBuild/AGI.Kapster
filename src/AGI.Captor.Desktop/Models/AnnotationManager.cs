using Avalonia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AGI.Captor.Desktop.Models;

/// <summary>
/// 标注管理器 - 负责管理所有标注项的生命周期
/// </summary>
public class AnnotationManager
{
    private readonly List<IAnnotationItem> _items = new();
    private readonly List<IAnnotationItem> _selectedItems = new();
    private int _nextZIndex = 1;

    /// <summary>
    /// 所有标注项（只读）
    /// </summary>
    public IReadOnlyList<IAnnotationItem> Items => _items.AsReadOnly();

    /// <summary>
    /// 当前选中的标注项（只读）
    /// </summary>
    public IReadOnlyList<IAnnotationItem> SelectedItems => _selectedItems.AsReadOnly();

    /// <summary>
    /// 总数量
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// 是否有选中项
    /// </summary>
    public bool HasSelection => _selectedItems.Count > 0;

    /// <summary>
    /// 标注项变更事件
    /// </summary>
    public event EventHandler<AnnotationChangedEventArgs>? ItemChanged;

    /// <summary>
    /// 选择变更事件
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// 添加标注项
    /// </summary>
    public void AddItem(IAnnotationItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        item.ZIndex = _nextZIndex++;
        _items.Add(item);

        ItemChanged?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeType.Added, item));
    }

    /// <summary>
    /// 批量添加标注项
    /// </summary>
    public void AddItems(IEnumerable<IAnnotationItem> items)
    {
        foreach (var item in items)
        {
            AddItem(item);
        }
    }

    /// <summary>
    /// 移除标注项
    /// </summary>
    public bool RemoveItem(IAnnotationItem item)
    {
        if (item == null) return false;

        var removed = _items.Remove(item);
        if (removed)
        {
            _selectedItems.Remove(item);
            ItemChanged?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeType.Removed, item));

            if (_selectedItems.Count == 0)
                SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(new IAnnotationItem[0], new IAnnotationItem[0]));
        }

        return removed;
    }

    /// <summary>
    /// 移除多个标注项
    /// </summary>
    public void RemoveItems(IEnumerable<IAnnotationItem> items)
    {
        var itemsToRemove = items.ToList();
        foreach (var item in itemsToRemove)
        {
            RemoveItem(item);
        }
    }

    /// <summary>
    /// 清空所有标注项
    /// </summary>
    public void Clear()
    {
        var oldItems = _items.ToList();
        _items.Clear();

        var oldSelection = _selectedItems.ToList();
        _selectedItems.Clear();

        foreach (var item in oldItems)
        {
            ItemChanged?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeType.Removed, item));
        }

        if (oldSelection.Count > 0)
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(oldSelection, new IAnnotationItem[0]));

        _nextZIndex = 1;
    }

    /// <summary>
    /// 点击测试 - 查找指定点下的标注项
    /// </summary>
    public IAnnotationItem? HitTest(Point point)
    {
        // 从上到下（Z轴从高到低）查找
        return _items
            .Where(item => item.IsVisible && item.HitTest(point))
            .OrderByDescending(item => item.ZIndex)
            .FirstOrDefault();
    }

    /// <summary>
    /// 区域测试 - 查找与指定矩形相交的标注项
    /// </summary>
    public IEnumerable<IAnnotationItem> HitTest(Rect region)
    {
        return _items
            .Where(item => item.IsVisible && item.Bounds.Intersects(region))
            .OrderByDescending(item => item.ZIndex);
    }

    /// <summary>
    /// 选择单个标注项
    /// </summary>
    public void SelectItem(IAnnotationItem item, bool addToSelection = false)
    {
        if (item == null) return;

        var oldSelection = _selectedItems.ToList();

        if (!addToSelection)
        {
            // 清除旧选择
            foreach (var oldItem in _selectedItems)
            {
                oldItem.State = AnnotationState.Normal;
            }
            _selectedItems.Clear();
        }

        if (!_selectedItems.Contains(item))
        {
            _selectedItems.Add(item);
            item.State = AnnotationState.Selected;
        }

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(oldSelection, _selectedItems));
    }

    /// <summary>
    /// 选择多个标注项
    /// </summary>
    public void SelectItems(IEnumerable<IAnnotationItem> items, bool addToSelection = false)
    {
        var itemsToSelect = items.ToList();
        if (itemsToSelect.Count == 0) return;

        var oldSelection = _selectedItems.ToList();

        if (!addToSelection)
        {
            // 清除旧选择
            foreach (var oldItem in _selectedItems)
            {
                oldItem.State = AnnotationState.Normal;
            }
            _selectedItems.Clear();
        }

        foreach (var item in itemsToSelect)
        {
            if (!_selectedItems.Contains(item))
            {
                _selectedItems.Add(item);
                item.State = AnnotationState.Selected;
            }
        }

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(oldSelection, _selectedItems));
    }

    /// <summary>
    /// 取消选择
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedItems.Count == 0) return;

        var oldSelection = _selectedItems.ToList();

        foreach (var item in _selectedItems)
        {
            item.State = AnnotationState.Normal;
        }
        _selectedItems.Clear();

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(oldSelection, new IAnnotationItem[0]));
    }

    /// <summary>
    /// 取消选择指定项目
    /// </summary>
    public void DeselectItem(IAnnotationItem item)
    {
        if (item == null || !_selectedItems.Contains(item)) return;

        var oldSelection = _selectedItems.ToList();

        _selectedItems.Remove(item);
        item.State = AnnotationState.Normal;

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(oldSelection, _selectedItems));
    }

    /// <summary>
    /// Select all annotations
    /// </summary>
    public void SelectAll()
    {
        if (_items.Count == 0) return;

        var oldSelection = _selectedItems.ToList();

        // Add all items to selection
        foreach (var item in _items)
        {
            if (!_selectedItems.Contains(item))
            {
                _selectedItems.Add(item);
                item.State = AnnotationState.Selected;
            }
        }

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(oldSelection, _selectedItems));
    }

    /// <summary>
    /// 切换项目选择状态
    /// </summary>
    public void ToggleSelection(IAnnotationItem item)
    {
        if (_selectedItems.Contains(item))
        {
            _selectedItems.Remove(item);
            item.State = AnnotationState.Normal;
        }
        else
        {
            _selectedItems.Add(item);
            item.State = AnnotationState.Selected;
        }

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(_selectedItems, _selectedItems));
    }

    /// <summary>
    /// 提升选中项的层级（向前）
    /// </summary>
    public void BringSelectedToFront()
    {
        if (_selectedItems.Count == 0) return;

        var maxZ = _items.Max(item => item.ZIndex);
        foreach (var item in _selectedItems.OrderBy(item => item.ZIndex))
        {
            item.ZIndex = ++maxZ;
        }
        _nextZIndex = maxZ + 1;

        foreach (var item in _selectedItems)
        {
            ItemChanged?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeType.Modified, item));
        }
    }

    /// <summary>
    /// 降低选中项的层级（向后）
    /// </summary>
    public void SendSelectedToBack()
    {
        if (_selectedItems.Count == 0) return;

        var minZ = _items.Min(item => item.ZIndex);
        foreach (var item in _selectedItems.OrderByDescending(item => item.ZIndex))
        {
            item.ZIndex = --minZ;
        }

        foreach (var item in _selectedItems)
        {
            ItemChanged?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeType.Modified, item));
        }
    }

    /// <summary>
    /// 删除选中的标注项
    /// </summary>
    public void DeleteSelected()
    {
        var itemsToDelete = _selectedItems.ToList();
        RemoveItems(itemsToDelete);
    }

    /// <summary>
    /// 复制选中的标注项
    /// </summary>
    public List<IAnnotationItem> CloneSelected()
    {
        return _selectedItems.Select(item => item.Clone()).ToList();
    }

    /// <summary>
    /// 序列化所有标注项
    /// </summary>
    public List<Dictionary<string, object>> SerializeAll()
    {
        return _items.Select(item => item.Serialize()).ToList();
    }

    /// <summary>
    /// 从序列化数据加载标注项
    /// </summary>
    public void LoadFromData(List<Dictionary<string, object>> data)
    {
        Clear();

        foreach (var itemData in data)
        {
            var item = AnnotationFactory.CreateFromData(itemData);
            if (item != null)
            {
                _items.Add(item);
                _nextZIndex = Math.Max(_nextZIndex, item.ZIndex + 1);
            }
        }

        foreach (var item in _items)
        {
            ItemChanged?.Invoke(this, new AnnotationChangedEventArgs(AnnotationChangeType.Added, item));
        }
    }
}

/// <summary>
/// 标注变更类型
/// </summary>
public enum AnnotationChangeType
{
    Added,
    Removed,
    Modified
}

/// <summary>
/// 标注变更事件参数
/// </summary>
public class AnnotationChangedEventArgs : EventArgs
{
    public AnnotationChangeType ChangeType { get; }
    public IAnnotationItem Item { get; }

    public AnnotationChangedEventArgs(AnnotationChangeType changeType, IAnnotationItem item)
    {
        ChangeType = changeType;
        Item = item;
    }
}

/// <summary>
/// 选择变更事件参数
/// </summary>
public class SelectionChangedEventArgs : EventArgs
{
    public IReadOnlyList<IAnnotationItem> OldSelection { get; }
    public IReadOnlyList<IAnnotationItem> NewSelection { get; }

    public SelectionChangedEventArgs(IEnumerable<IAnnotationItem> oldSelection, IEnumerable<IAnnotationItem> newSelection)
    {
        OldSelection = oldSelection.ToList().AsReadOnly();
        NewSelection = newSelection.ToList().AsReadOnly();
    }
}
