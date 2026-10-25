using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsDynamicIsland.Services;

public sealed record LyricLine(TimeSpan Timestamp, string Text);

public sealed class SongLyrics
{
    public static readonly SongLyrics Empty = new() { Title = "", Artist = "", PlainLyrics = "" };

    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public List<LyricLine> Lines { get; init; } = new();
    public string PlainLyrics { get; init; } = "";
    public bool HasSyncedLyrics => Lines.Count > 0;

    public int GetActiveIndexAt(TimeSpan position, TimeSpan? customLead = null)
    {
        if (Lines.Count == 0) return -1;

        // 250ms lead anticipation so the line highlights in sync with the vocalist's first syllable
        var lead = customLead ?? TimeSpan.FromMilliseconds(250);
        var effectivePos = position + lead;

        int activeIdx = -1;
        for (int i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].Timestamp <= effectivePos)
            {
                activeIdx = i;
            }
            else
            {
                break;
            }
        }
        return activeIdx;
    }

    public (LyricLine? Current, LyricLine? Next, LyricLine? Prev) GetLinesAtIndex(int activeIdx)
    {
        if (Lines.Count == 0) return (null, null, null);

        if (activeIdx == -1)
        {
            return (new LyricLine(TimeSpan.Zero, "♪ Intro ♪"), Lines.Count > 0 ? Lines[0] : null, null);
        }

        if (activeIdx < 0 || activeIdx >= Lines.Count) activeIdx = Lines.Count - 1;

        var prev = activeIdx > 0 ? Lines[activeIdx - 1] : null;
        var curr = Lines[activeIdx];
        var next = (activeIdx + 1 < Lines.Count) ? Lines[activeIdx + 1] : null;

        return (curr, next, prev);
    }

    public (LyricLine? Current, LyricLine? Next, LyricLine? Prev) GetLinesAt(TimeSpan position, TimeSpan? customLead = null)
    {
        int idx = GetActiveIndexAt(position, customLead);
        return GetLinesAtIndex(idx);
    }
}

/// <summary>
/// Fetches and caches synchronized LRC lyrics from LRCLIB.
/// Real-time synchronization is matched against current playback timestamp.
/// </summary>
public sealed class LyricsService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    static LyricsService()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsDynamicIsland/1.0 (https://github.com/myproject)");
        }
        catch { }
    }

    private readonly Dictionary<string, SongLyrics> _cache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private string _currentKey = "";

    public SongLyrics? CurrentLyrics { get; private set; }
    public event Action<SongLyrics>? LyricsUpdated;

    public async Task UpdateTrackAsync(string rawTitle, string rawArtist, string rawAlbum = "", double durationSeconds = 0)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            CurrentLyrics = null;
            LyricsUpdated?.Invoke(SongLyrics.Empty);
            return;
        }

        string cleanTitle = CleanTrackTitle(rawTitle);
        string cleanArtist = CleanArtistName(rawArtist);
        string key = $"{cleanArtist}|{cleanTitle}";

        if (key == _currentKey && CurrentLyrics != null)
        {
            return;
        }

        _currentKey = key;

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                CurrentLyrics = cached;
                LyricsUpdated?.Invoke(cached);
                return;
            }
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var lyrics = await FetchLyricsAsync(rawTitle, cleanTitle, cleanArtist, rawAlbum, durationSeconds, token);
            if (!token.IsCancellationRequested && lyrics != null)
            {
                lock (_cache)
                {
                    _cache[key] = lyrics;
                }
                CurrentLyrics = lyrics;
                LyricsUpdated?.Invoke(lyrics);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Warn("Lyrics fetch error for " + key, ex);
        }
    }

    private static async Task<SongLyrics?> FetchLyricsAsync(string rawTitle, string cleanTitle, string artist, string album, double duration, CancellationToken ct)
    {
        // 1. If raw title has distinct version tag (e.g. "Live" / "Acoustic"), try exact match first
        if (!string.Equals(rawTitle, cleanTitle, StringComparison.OrdinalIgnoreCase))
        {
            string rawGetUrl = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(rawTitle)}";
            if (duration > 1) rawGetUrl += $"&duration={(int)Math.Round(duration)}";
            var rawResult = await QueryLrcLibAsync(rawGetUrl, ct);
            if (rawResult != null && rawResult.HasSyncedLyrics) return rawResult;
        }

        // 2. Exact match attempt via /api/get with clean title and duration
        string getUrl = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(cleanTitle)}";
        if (duration > 1)
        {
            getUrl += $"&duration={(int)Math.Round(duration)}";
        }

        var result = await QueryLrcLibAsync(getUrl, ct);
        if (result != null && result.HasSyncedLyrics)
        {
            return result;
        }

        // 3. Exact match without duration
        string noDurUrl = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(cleanTitle)}";
        var noDurResult = await QueryLrcLibAsync(noDurUrl, ct);
        if (noDurResult != null && noDurResult.HasSyncedLyrics)
        {
            return noDurResult;
        }

        // 4. Search fallback via /api/search with duration scoring
        string searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(artist + " " + cleanTitle)}";
        var searchResult = await QueryLrcLibSearchAsync(searchUrl, duration, ct);
        if (searchResult != null)
        {
            return searchResult;
        }

        return result ?? noDurResult;
    }

    private static async Task<SongLyrics?> QueryLrcLibAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("WindowsDynamicIsland/1.0");
            using var res = await Http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            return ParseLrcLibJson(json);
        }
        catch { return null; }
    }

    private static async Task<SongLyrics?> QueryLrcLibSearchAsync(string url, double targetDuration, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("WindowsDynamicIsland/1.0");
            using var res = await Http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            JsonElement? bestSyncedCandidate = null;
            double bestDiff = double.MaxValue;
            JsonElement? firstSynced = null;
            JsonElement? firstAny = null;

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (firstAny == null) firstAny = elem;

                bool hasSynced = elem.TryGetProperty("syncedLyrics", out var syn) && !string.IsNullOrWhiteSpace(syn.GetString());
                if (hasSynced)
                {
                    if (firstSynced == null) firstSynced = elem;

                    if (targetDuration > 1 && elem.TryGetProperty("duration", out var dProp) && dProp.TryGetDouble(out double dVal))
                    {
                        double diff = Math.Abs(dVal - targetDuration);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestSyncedCandidate = elem;
                        }
                    }
                }
            }

            // Pick closest candidate in duration if within 20s
            if (bestSyncedCandidate.HasValue && bestDiff < 20.0)
            {
                return ParseLrcLibJson(bestSyncedCandidate.Value.GetRawText());
            }

            if (bestSyncedCandidate.HasValue)
            {
                return ParseLrcLibJson(bestSyncedCandidate.Value.GetRawText());
            }

            if (firstSynced.HasValue)
            {
                return ParseLrcLibJson(firstSynced.Value.GetRawText());
            }

            if (firstAny.HasValue)
            {
                return ParseLrcLibJson(firstAny.Value.GetRawText());
            }
        }
        catch { }
        return null;
    }

    private static SongLyrics? ParseLrcLibJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string title = root.TryGetProperty("trackName", out var t) ? (t.GetString() ?? "") : "";
            string artist = root.TryGetProperty("artistName", out var a) ? (a.GetString() ?? "") : "";
            string plain = root.TryGetProperty("plainLyrics", out var p) ? (p.GetString() ?? "") : "";
            string synced = root.TryGetProperty("syncedLyrics", out var s) ? (s.GetString() ?? "") : "";

            var lines = ParseLrcString(synced);

            return new SongLyrics
            {
                Title = title,
                Artist = artist,
                PlainLyrics = plain,
                Lines = lines
            };
        }
        catch { return null; }
    }

    public static List<LyricLine> ParseLrcString(string lrc)
    {
        var list = new List<LyricLine>();
        if (string.IsNullOrWhiteSpace(lrc)) return list;

        var regex = new Regex(@"\[(\d{1,2}):(\d{1,2})(?:[\.:](\d{1,3}))?\](.*)");
        var rawLines = lrc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in rawLines)
        {
            var match = regex.Match(raw);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int min) &&
                    int.TryParse(match.Groups[2].Value, out int sec))
                {
                    int ms = 0;
                    if (match.Groups[3].Success)
                    {
                        string msStr = match.Groups[3].Value;
                        if (msStr.Length == 2) msStr += "0";
                        int.TryParse(msStr, out ms);
                    }

                    var ts = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec) + TimeSpan.FromMilliseconds(ms);
                    string text = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        list.Add(new LyricLine(ts, text));
                    }
                }
            }
        }

        list.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));
        return list;
    }

    public static string CleanTrackTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        string s = Regex.Replace(title, @"\s*[\(\[](?:feat|ft|with|official|remastered|explicit|audio|video|lyrics|live|acoustic|deluxe|version).*?[\)\]]", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*-\s*(?:single|ep|live|remastered|bonus track|acoustic|deluxe)$", "", RegexOptions.IgnoreCase);
        return s.Trim();
    }

    public static string CleanArtistName(string artist)
    {
        if (string.IsNullOrWhiteSpace(artist)) return "";
        int dash = artist.IndexOf('—');
        if (dash < 0) dash = artist.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0) artist = artist[..dash];
        return artist.Trim();
    }
}
