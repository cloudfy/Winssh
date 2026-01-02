using Microsoft.UI.Xaml.Controls;
using System;

namespace WinSsh.Profiles;

public sealed class HostProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public Symbol Symbol { get; set; } = Symbol.Globe;
    public string Name { get; set; } = "New Host";
    public string UserAndHost => $"{Username}@{Host}";

    public string Host { get; set; } = "example.com";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "user";

    public string? IdentityFile { get; set; } // -i
    public bool RequestTty { get; set; } = true; // -tt
    public bool AgentForwarding { get; set; } = false; // -A

    // accept-new | ask | yes | no
    public string HostKeyChecking { get; set; } = "ask";

    public string? JumpHostId { get; set; } // -J

    public string? ExtraArgs { get; set; }

    public int InitialCols { get; set; } = 120;
    public int InitialRows { get; set; } = 30;
}