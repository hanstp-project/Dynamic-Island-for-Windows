import Foundation

/// Service for fetching and syncing real-time karaoke lyrics via LRCLIB
@MainActor
public final class LyricsService: ObservableObject {
    public static let shared = LyricsService()

    @Published public private(set) var lines: [LyricLine] = []
    @Published public private(set) var activeIndex: Int = -1
    @Published public private(set) var isLoading: Bool = false
    @Published public private(set) var hasLyrics: Bool = false

    private var currentSongKey: String = ""

    // 250ms vocal lead anticipation offset
    private let vocalLeadOffset: Double = 0.25

    public init() {}

    public func fetchLyrics(trackTitle: String, artist: String, duration: Double) async {
        let key = "\(trackTitle.lowercased()) - \(artist.lowercased())"
        guard key != currentSongKey, !trackTitle.isEmpty else { return }

        currentSongKey = key
        isLoading = true
        lines = []
        activeIndex = -1
        hasLyrics = false

        defer { isLoading = false }

        // Clean track title (strip out '(Remastered)', '[Live]', etc. for initial match)
        let cleanedTitle = cleanTitle(trackTitle)

        guard let url = buildLrcLibURL(title: cleanedTitle, artist: artist, duration: duration) else {
            return
        }

        do {
            var request = URLRequest(url: url)
            request.setValue("MacDynamicIsland/1.0 (https://github.com/hanstp-project)", forHTTPHeaderField: "User-Agent")
            request.timeoutInterval = 8.0

            let (data, response) = try await URLSession.shared.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else {
                // Fallback to fuzzy search query
                await searchFallback(title: cleanedTitle, artist: artist, duration: duration)
                return
            }

            if let json = try JSONSerialization.jsonObject(with: data) as? [String: Any],
               let syncedLyrics = json["syncedLyrics"] as? String, !syncedLyrics.isEmpty {
                parseLrcString(syncedLyrics)
            } else {
                await searchFallback(title: cleanedTitle, artist: artist, duration: duration)
            }
        } catch {
            await searchFallback(title: cleanedTitle, artist: artist, duration: duration)
        }
    }

    private func searchFallback(title: String, artist: String, duration: Double) async {
        guard let query = "\(title) \(artist)".addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
              let url = URL(string: "https://lrclib.net/api/search?q=\(query)") else { return }

        do {
            var request = URLRequest(url: url)
            request.setValue("MacDynamicIsland/1.0", forHTTPHeaderField: "User-Agent")
            request.timeoutInterval = 6.0

            let (data, response) = try await URLSession.shared.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse, httpResponse.statusCode == 200 else { return }

            if let items = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] {
                // Find item closest to song duration
                var bestCandidate: String?
                var minDiff: Double = 999.0

                for item in items {
                    guard let synced = item["syncedLyrics"] as? String, !synced.isEmpty else { continue }
                    if let itemDur = item["duration"] as? Double, duration > 0 {
                        let diff = abs(itemDur - duration)
                        if diff < minDiff {
                            minDiff = diff
                            bestCandidate = synced
                        }
                    } else if bestCandidate == nil {
                        bestCandidate = synced
                    }
                }

                if let chosenLrc = bestCandidate {
                    parseLrcString(chosenLrc)
                }
            }
        } catch { }
    }

    private func parseLrcString(_ lrc: String) {
        var parsed: [LyricLine] = []
        let rawLines = lrc.components(separatedBy: .newlines)

        for line in rawLines {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            guard trimmed.hasPrefix("[") else { continue }

            if let closeBracket = trimmed.firstIndex(of: "]") {
                let timeStr = String(trimmed[trimmed.index(after: trimmed.startIndex)..<closeBracket])
                let text = String(trimmed[trimmed.index(after: closeBracket)...]).trimmingCharacters(in: .whitespaces)

                if let seconds = parseLrcTimestamp(timeStr), !text.isEmpty {
                    parsed.append(LyricLine(timestamp: seconds, text: text))
                }
            }
        }

        self.lines = parsed.sorted(by: { $0.timestamp < $1.timestamp })
        self.hasLyrics = !parsed.isEmpty
    }

    private func parseLrcTimestamp(_ stamp: String) -> Double? {
        let parts = stamp.components(separatedBy: ":")
        guard parts.count == 2,
              let minutes = Double(parts[0]),
              let seconds = Double(parts[1]) else { return nil }
        return (minutes * 60.0) + seconds
    }

    public func updatePosition(_ currentSeconds: Double) {
        guard !lines.isEmpty else { return }
        let effectiveTime = currentSeconds + vocalLeadOffset

        var newIdx = -1
        for (i, line) in lines.enumerated() {
            if line.timestamp <= effectiveTime {
                newIdx = i
            } else {
                break
            }
        }

        if newIdx != activeIndex {
            activeIndex = newIdx
            for i in 0..<lines.count {
                lines[i].isActive = (i == activeIndex)
            }
        }
    }

    private func buildLrcLibURL(title: String, artist: String, duration: Double) -> URL? {
        var components = URLComponents(string: "https://lrclib.net/api/get")
        var queryItems = [
            URLQueryItem(name: "track_name", value: title),
            URLQueryItem(name: "artist_name", value: artist)
        ]
        if duration > 0 {
            queryItems.append(URLQueryItem(name: "duration", value: String(Int(duration))))
        }
        components?.queryItems = queryItems
        return components?.url
    }

    private func cleanTitle(_ title: String) -> String {
        var res = title
        let patterns = [
            #"\s*[\(\[].*?(remaster|live|official|deluxe|version|audio|video|edit).*?[\)\]]"#,
            #"\s*-\s*(remastered|live|radio edit).*"#
        ]
        for pat in patterns {
            if let reg = try? NSRegularExpression(pattern: pat, options: .caseInsensitive) {
                res = reg.stringByReplacingMatches(in: res, range: NSRange(res.startIndex..., in: res), withTemplate: "")
            }
        }
        return res.trimmingCharacters(in: .whitespaces)
    }
}
