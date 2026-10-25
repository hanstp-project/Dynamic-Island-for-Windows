using System;

namespace WindowsDynamicIsland.Animations;

/// <summary>Explicit visual states of the island.</summary>
public enum IslandState
{
    Idle,
    Hovered,
    Expanded,
    MusicPlaying,
    Notification,
    SystemInformation,
    Settings,
    TemporarilyHidden,
}

/// <summary>Pages available inside the expanded island.</summary>
public enum ExpandedPage { Dashboard, Media, System, Clock, Tools, Weather, Camera, Empty }

/// <summary>
/// Pure, UI-free reducer that derives the island state from a set of inputs.
/// Because the state is always *derived* (never pushed imperatively), rapid or conflicting
/// triggers (hover + click + notification arriving mid-animation) can never leave the island
/// in an inconsistent state: the latest inputs always map to exactly one target state.
///
/// Priority (highest first): Hidden → Notification → user-expanded → Settings → Hovered → MusicPlaying → Idle.
/// </summary>
public sealed class IslandStateMachine
{
    private bool _userHidden, _fullscreenHidden, _settingsOpen, _notificationActive,
                 _notificationDetails, _expanded, _hovered, _musicPlaying;
    private bool _mediaAvailable, _systemAvailable = true, _clockAvailable = true;
    private ExpandedPage _page = ExpandedPage.Dashboard;
    private bool _pageChosenByUser;

    public IslandState State { get; private set; } = IslandState.Idle;
    public ExpandedPage Page => _page;
    public bool NotificationDetailsVisible => _notificationDetails && _notificationActive;
    public bool IsMusicPlaying => _musicPlaying;
    public bool IsExpandedByUser => _expanded;
    public bool IsHovered => _hovered;

    /// <summary>Raised whenever State, Page or notification-details visibility changes.</summary>
    public event Action<IslandState, IslandState>? StateChanged;

    // ----------------- Inputs -----------------
    public void SetUserHidden(bool value) => Update(ref _userHidden, value);
    public void SetFullscreenHidden(bool value) => Update(ref _fullscreenHidden, value);
    public void SetSettingsOpen(bool value) => Update(ref _settingsOpen, value);
    public void SetHovered(bool value) => Update(ref _hovered, value);
    public void SetMusicPlaying(bool value) => Update(ref _musicPlaying, value);

    public void SetNotificationActive(bool value)
    {
        if (!value) _notificationDetails = false;
        Update(ref _notificationActive, value);
    }

    public void SetNotificationDetails(bool value)
    {
        if (!_notificationActive) value = false;
        Update(ref _notificationDetails, value);
    }

    public void SetExpanded(bool value)
    {
        if (value && !_expanded && !_pageChosenByUser) _page = DefaultPage();
        if (!value) _pageChosenByUser = false;
        Update(ref _expanded, value);
    }

    public void ToggleExpanded() => SetExpanded(!_expanded);

    public void SetAvailability(bool media, bool system, bool clock)
    {
        _mediaAvailable = media; _systemAvailable = system; _clockAvailable = clock;
        if (!IsPageAvailable(_page))
        {
            _page = DefaultPage();
            _pageChosenByUser = false;
        }
        Recompute(force: true);
    }

    public void SelectPage(ExpandedPage page)
    {
        if (!IsPageAvailable(page)) return;
        _pageChosenByUser = true;
        if (_page == page) return;
        _page = page;
        Recompute(force: true);
    }

    public bool IsPageAvailable(ExpandedPage page) => page switch
    {
        ExpandedPage.Media => _mediaAvailable,
        ExpandedPage.System => _systemAvailable,
        ExpandedPage.Clock => _clockAvailable,
        ExpandedPage.Tools => true,
        ExpandedPage.Weather => true,
        ExpandedPage.Empty => false,
        _ => true,
    };

    public ExpandedPage DefaultPage()
    {
        if (_mediaAvailable && _musicPlaying) return ExpandedPage.Media;
        return ExpandedPage.Dashboard;
    }

    // ----------------- Reducer -----------------
    public static IslandState Reduce(bool hidden, bool notification, bool expanded, ExpandedPage page,
                                     bool settingsOpen, bool hovered, bool musicPlaying)
    {
        if (hidden) return IslandState.TemporarilyHidden;
        if (notification) return IslandState.Notification;
        if (expanded) return IslandState.Expanded;
        if (settingsOpen) return IslandState.Settings;
        if (hovered) return IslandState.Hovered;
        if (musicPlaying) return IslandState.MusicPlaying;
        return IslandState.Idle;
    }

    private void Update(ref bool field, bool value)
    {
        if (field == value) return;
        field = value;
        Recompute(force: true);
    }

    private IslandState _lastRaisedState = IslandState.Idle;
    private ExpandedPage _lastRaisedPage = ExpandedPage.Dashboard;
    private bool _lastRaisedDetails;
    private bool _lastRaisedMusic;

    private void Recompute(bool force)
    {
        var next = Reduce(_userHidden || _fullscreenHidden, _notificationActive, _expanded, _page,
                          _settingsOpen, _hovered, _musicPlaying);
        var old = State;
        State = next;
        bool visualChange = next != _lastRaisedState
                            || (_page != _lastRaisedPage && (next is IslandState.Expanded or IslandState.SystemInformation))
                            || NotificationDetailsVisible != _lastRaisedDetails
                            || (next == IslandState.Hovered && _musicPlaying != _lastRaisedMusic);
        if (!visualChange) return;
        _lastRaisedState = next;
        _lastRaisedPage = _page;
        _lastRaisedDetails = NotificationDetailsVisible;
        _lastRaisedMusic = _musicPlaying;
        StateChanged?.Invoke(old, next);
    }
}
