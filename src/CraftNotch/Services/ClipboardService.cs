using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CraftNotch.Models;

namespace CraftNotch.Services;

public class ClipboardService
{
    public ObservableCollection<ClipboardItem> History { get; } = new ObservableCollection<ClipboardItem>();
    public event Action<ClipboardItem>? ItemAdded;

    private string _lastText = "";

    public void OnClipboardUpdated()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText().Trim();
                if (!string.IsNullOrEmpty(text) && text != _lastText)
                {
                    _lastText = text;
                    var existing = History.FirstOrDefault(x => x.Content == text);
                    if (existing != null)
                    {
                        History.Remove(existing);
                        existing.Timestamp = DateTime.Now;
                        History.Insert(0, existing);
                        ItemAdded?.Invoke(existing);
                    }
                    else
                    {
                        var newItem = new ClipboardItem
                        {
                            Content = text,
                            Timestamp = DateTime.Now
                        };
                        History.Insert(0, newItem);
                        if (History.Count > 50)
                        {
                            History.RemoveAt(History.Count - 1);
                        }
                        ItemAdded?.Invoke(newItem);
                    }
                }
            }
        }
        catch { }
    }

    public void CopyItem(ClipboardItem item)
    {
        try
        {
            _lastText = item.Content;
            Clipboard.SetText(item.Content);
        }
        catch { }
    }

    public void TogglePin(ClipboardItem item)
    {
        item.IsPinned = !item.IsPinned;
    }

    public void Remove(ClipboardItem item)
    {
        History.Remove(item);
    }

    public void Clear()
    {
        var pinned = History.Where(x => x.IsPinned).ToList();
        History.Clear();
        foreach (var p in pinned) History.Add(p);
    }
}
