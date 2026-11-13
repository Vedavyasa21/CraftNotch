namespace Scout.Models;

public enum IslandMode
{
    Idle,
    Compact,
    Expanded,
    Slim,
    Hud,
    Hidden
}

public enum IslandShape
{
    Island,
    Notch
}

public enum IslandPosition
{
    Top,
    Bottom,
    Center,
    Left,
    Right
}

public enum CardPanel
{
    Media,
    Widgets,
    Controls,
    Clipboard,
    Shelf,
    Agents
}

public enum HudKind
{
    Volume,
    Brightness,
    Charging,
    LowBattery,
    MicMuted,
    MicLive,
    Copied,
    LockKey
}

public enum IdleFaceKind
{
    Time,
    Date,
    Battery,
    SystemStats,
    Pet,
    Clean
}

public enum PetKind
{
    Cat,
    Dog,
    Fox
}

public enum PetAction
{
    Sit,
    Walk,
    Look,
    Sleep
}
