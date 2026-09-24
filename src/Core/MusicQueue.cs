using System;
using System.Collections.Generic;

namespace greg.Mods.MusicPlayer.Core;

// Playback modes: Off = play queue once, then stop. All = endless loop
// over the queue. One = repeat current track. Shuffle = mixed.
public enum RepeatMode
{
    Off = 0,
    All = 1,
    One = 2,
    Shuffle = 3,
}

// Ordered queue. Pure decision logic (except the random source),
// no Unity/Il2Cpp access.
public sealed class MusicQueue
{
    private readonly List<MusicTrack> _items = new List<MusicTrack>();
    private readonly Random _rng;

    public MusicQueue() : this(new Random()) { }

    public MusicQueue(Random rng)
    {
        _rng = rng ?? new Random();
    }

    public int Count => _items.Count;
    public int Position { get; private set; } = -1;

    public MusicTrack Current
    {
        get
        {
            if (Position < 0 || Position >= _items.Count) return null;
            return _items[Position];
        }
    }

    public IReadOnlyList<MusicTrack> Items => _items;

    public void Enqueue(MusicTrack track)
    {
        if (track == null) return;
        _items.Add(track);
    }

    public void EnqueueNext(MusicTrack track)
    {
        if (track == null) return;
        int at = Position + 1;
        if (at < 0) at = 0;
        if (at > _items.Count) at = _items.Count;
        _items.Insert(at, track);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        _items.RemoveAt(index);
        if (_items.Count == 0) { Position = -1; return; }
        if (index < Position) Position--;
        else if (index == Position) Position = Math.Min(Position, _items.Count - 1);
    }

    public void Clear()
    {
        _items.Clear();
        Position = -1;
    }

    public void ReplaceAll(IEnumerable<MusicTrack> tracks)
    {
        _items.Clear();
        Position = -1;
        if (tracks == null) return;
        foreach (var t in tracks)
        {
            if (t != null) _items.Add(t);
        }
    }

    public MusicTrack PlayAt(int index)
    {
        if (index < 0 || index >= _items.Count) return null;
        Position = index;
        return _items[index];
    }

    // Next track by mode. manual=true: user pressed Next
    // (One then falls back to Next). Return null = stop.
    public MusicTrack Advance(RepeatMode mode, bool manual)
    {
        if (_items.Count == 0) return null;
        if (Position < 0 || Position >= _items.Count)
        {
            Position = 0;
            return _items[0];
        }
        if (mode == RepeatMode.Shuffle)
        {
            if (_items.Count == 1) { Position = 0; return _items[0]; }
            int next = Position;
            int guard = 0;
            while (next == Position && guard < 16)
            {
                next = _rng.Next(_items.Count);
                guard++;
            }
            Position = next;
            return _items[Position];
        }
        if (mode == RepeatMode.One && !manual)
        {
            return _items[Position];
        }
        int target = Position + 1;
        if (target >= _items.Count)
        {
            if (mode == RepeatMode.All || (mode == RepeatMode.One && manual)) target = 0;
            else return null;
        }
        Position = target;
        return _items[Position];
    }

    // Next track by mode WITHOUT position change (for preload).
    public MusicTrack PeekNext(RepeatMode mode)
    {
        if (_items.Count == 0) return null;
        if (Position < 0 || Position >= _items.Count) return _items[0];
        if (mode == RepeatMode.Shuffle)
        {
            if (_items.Count == 1) return _items[0];
            int next = Position;
            int guard = 0;
            while (next == Position && guard < 16)
            {
                next = _rng.Next(_items.Count);
                guard++;
            }
            return _items[next];
        }
        if (mode == RepeatMode.One) return _items[Position];
        int target = Position + 1;
        if (target >= _items.Count)
        {
            if (mode == RepeatMode.All) target = 0;
            else return null;
        }
        return _items[target];
    }

    // Previous track (manual only): one position back, stops at 0.
    public MusicTrack StepBack()
    {
        if (_items.Count == 0) return null;
        if (Position <= 0)
        {
            Position = 0;
            return _items[0];
        }
        Position--;
        return _items[Position];
    }

    public static string ModeLabel(RepeatMode mode)
    {
        switch (mode)
        {
            case RepeatMode.All: return "All";
            case RepeatMode.One: return "One";
            case RepeatMode.Shuffle: return "Shuffle";
            default: return "Off";
        }
    }
}
