using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using Scout.Models;

namespace Scout.Services;

public class AgentMonitorService
{
    private readonly DispatcherTimer _timer;
    public AgentStatus CurrentStatus { get; private set; } = new AgentStatus();
    public event Action<AgentStatus>? StatusChanged;

    private readonly string _antigravityBrainDir;
    private readonly string _claudeProjectsDir;
    private DateTime _lastCheckedTime = DateTime.MinValue;

    public AgentMonitorService()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _antigravityBrainDir = Path.Combine(userProfile, ".gemini", "antigravity-cli", "brain");
        _claudeProjectsDir = Path.Combine(userProfile, ".claude", "projects");

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _timer.Tick += (s, e) => CheckAgentActivity();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void CheckAgentActivity()
    {
        try
        {
            // 1. Check Antigravity first
            if (Directory.Exists(_antigravityBrainDir))
            {
                var mostRecentDir = Directory.GetDirectories(_antigravityBrainDir)
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (mostRecentDir != null)
                {
                    string transcriptPath = Path.Combine(mostRecentDir.FullName, ".system_generated", "logs", "transcript.jsonl");
                    if (File.Exists(transcriptPath))
                    {
                        var fileInfo = new FileInfo(transcriptPath);
                        if (fileInfo.LastWriteTimeUtc > _lastCheckedTime)
                        {
                            _lastCheckedTime = fileInfo.LastWriteTimeUtc;
                            ParseTranscriptLine(transcriptPath);
                            return;
                        }
                        else if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc < TimeSpan.FromSeconds(15))
                        {
                            // Active recently
                            return;
                        }
                    }
                }
            }

            // 2. Fallback to idle if no agent activity in last 30s
            if (DateTime.Now - CurrentStatus.LastActivity > TimeSpan.FromSeconds(30) && CurrentStatus.State != AgentState.Idle)
            {
                CurrentStatus = new AgentStatus
                {
                    Agent = AgentKind.Antigravity,
                    State = AgentState.Idle,
                    Summary = "Ready for work",
                    Detail = "All tasks idle",
                    LastActivity = DateTime.Now
                };
                StatusChanged?.Invoke(CurrentStatus);
            }
        }
        catch { }
    }

    private void ParseTranscriptLine(string transcriptPath)
    {
        try
        {
            using var fs = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            
            // Read last non-empty line
            string? lastLine = null;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line)) lastLine = line;
            }

            if (!string.IsNullOrEmpty(lastLine))
            {
                using var doc = JsonDocument.Parse(lastLine);
                var root = doc.RootElement;

                string type = root.TryGetProperty("type", out var pType) ? pType.GetString() ?? "" : "";
                string source = root.TryGetProperty("source", out var pSrc) ? pSrc.GetString() ?? "" : "";

                var status = new AgentStatus
                {
                    Agent = AgentKind.Antigravity,
                    LastActivity = DateTime.Now
                };

                if (root.TryGetProperty("tool_calls", out var pCalls) && pCalls.GetArrayLength() > 0)
                {
                    var firstCall = pCalls[0];
                    string toolName = firstCall.TryGetProperty("name", out var pName) ? pName.GetString() ?? "" : "";

                    if (toolName.Contains("write_file") || toolName.Contains("edit_file"))
                    {
                        status.State = AgentState.Editing;
                        status.Summary = "Modifying files...";
                        status.Detail = toolName;
                    }
                    else if (toolName.Contains("run_command"))
                    {
                        status.State = AgentState.Command;
                        status.Summary = "Executing command...";
                        status.Detail = "Running build/command";
                    }
                    else if (toolName.Contains("ask_question"))
                    {
                        status.State = AgentState.WaitingForUser;
                        status.Summary = "Needs your input!";
                        status.Detail = "Awaiting decision";
                    }
                    else
                    {
                        status.State = AgentState.Thinking;
                        status.Summary = "Analyzing solution...";
                        status.Detail = toolName;
                    }
                }
                else if (type == "PLANNER_RESPONSE")
                {
                    status.State = AgentState.WaitingForUser;
                    status.Summary = "Awaiting instructions";
                    status.Detail = "Completed step";
                }
                else
                {
                    status.State = AgentState.Thinking;
                    status.Summary = "Agent active";
                    status.Detail = type;
                }

                CurrentStatus = status;
                StatusChanged?.Invoke(status);
            }
        }
        catch { }
    }
}
