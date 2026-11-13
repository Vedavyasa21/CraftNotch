using System;

namespace CraftNotch.Models;

public enum AgentKind
{
    Antigravity,
    ClaudeCode,
    Codex,
    Custom
}

public enum AgentState
{
    Idle,
    Working,
    Thinking,
    Editing,
    Command,
    WaitingForUser,
    Done,
    Error
}

public class AgentStatus
{
    public AgentKind Agent { get; set; } = AgentKind.Antigravity;
    public string AgentName { get; set; } = "Antigravity";
    public AgentState State { get; set; } = AgentState.Idle;
    public string Summary { get; set; } = "Ready";
    public string Detail { get; set; } = "";
    public DateTime LastActivity { get; set; } = DateTime.Now;

    public string StateBadge => State switch
    {
        AgentState.Working => "Working...",
        AgentState.Thinking => "Thinking...",
        AgentState.Editing => "Editing files",
        AgentState.Command => "Running command",
        AgentState.WaitingForUser => "Needs your input!",
        AgentState.Done => "Completed",
        AgentState.Error => "Attention required",
        _ => "Idle"
    };

    public string StateColor => State switch
    {
        AgentState.Working => "#34D399",       // Mint
        AgentState.Thinking => "#60A5FA",      // Blue
        AgentState.Editing => "#A78BFA",       // Purple
        AgentState.Command => "#34D399",       // Green
        AgentState.WaitingForUser => "#FBBF24", // Amber
        AgentState.Done => "#10B981",          // Emerald
        AgentState.Error => "#F87171",         // Red
        _ => "#9CA3AF"                         // Gray
    };
}
