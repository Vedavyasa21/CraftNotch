using System;
using CraftNotch.Models;

namespace CraftNotch.Models;

public class AppSettings
{
    public IslandShape Shape { get; set; } = IslandShape.Island;
    public IslandPosition Position { get; set; } = IslandPosition.Center;
    public bool StartWithWindows { get; set; } = false;
    public bool DeckServerEnabled { get; set; } = true;
    public int DeckPort { get; set; } = 48823;
    public bool VirtualPetEnabled { get; set; } = true;
    public PetKind PetKind { get; set; } = PetKind.Cat;
    public bool AutoHideInFullscreen { get; set; } = true;
    public bool AudioVisualizerEnabled { get; set; } = true;
    public bool GlobalHotkeyEnabled { get; set; } = true;
    public bool AgentMonitorEnabled { get; set; } = true;
}
