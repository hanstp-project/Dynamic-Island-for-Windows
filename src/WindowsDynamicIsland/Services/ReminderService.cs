using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WindowsDynamicIsland.Services;

public sealed record ReminderItem
{
    public long Id { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string Text { get; init; } = "";
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class ReminderService
{
    private readonly string _filePath;
    private readonly List<ReminderItem> _items = new();
    private readonly object _sync = new();

    public IReadOnlyList<ReminderItem> Items
    {
        get
        {
            lock (_sync) return _items.ToList();
        }
    }

    public event Action? Changed;

    public ReminderService(string dataDir)
    {
        _filePath = Path.Combine(dataDir, "reminders.json");
        Load();
    }

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        lock (_sync)
        {
            _items.Insert(0, new ReminderItem { Text = text.Trim() });
            // Cap at 20 items
            if (_items.Count > 20) _items.RemoveAt(_items.Count - 1);
            Save();
        }
        Changed?.Invoke();
    }

    public void Remove(long id)
    {
        lock (_sync)
        {
            _items.RemoveAll(x => x.Id == id);
            Save();
        }
        Changed?.Invoke();
    }

    public void Toggle(long id)
    {
        lock (_sync)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.IsCompleted = !item.IsCompleted;
                Save();
            }
        }
        Changed?.Invoke();
    }

    private void Load()
    {
        lock (_sync)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<List<ReminderItem>>(json);
                    if (loaded != null)
                    {
                        _items.Clear();
                        _items.AddRange(loaded);
                        return;
                    }
                }
            }
            catch { /* fallback to defaults */ }

            // Default starter reminders if empty
            if (_items.Count == 0)
            {
                _items.Add(new ReminderItem { Text = "Drink water" });
                _items.Add(new ReminderItem { Text = "Focus session" });
            }
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to save reminders.", ex);
        }
    }
}
