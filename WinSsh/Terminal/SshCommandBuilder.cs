using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinSsh.Terminal;

public static class SshCommandBuilder
{
    public static string BuildCommandLine(
        string sshExePath
        , Profiles.HostProfile profile
        , List<Profiles.HostProfile>? allProfiles = null)
    {
        // Quote everything carefully.
        var sb = new StringBuilder();
        sb.Append('"').Append(sshExePath).Append('"');

        if (profile.RequestTty) sb.Append(" -tt");
        if (profile.Port != 22) sb.Append(" -p ").Append(profile.Port);

        if (!string.IsNullOrWhiteSpace(profile.IdentityFile))
            sb.Append(" -i ").Append(Quote(profile.IdentityFile!));

        if (profile.AgentForwarding)
            sb.Append(" -A");

        if (!string.IsNullOrWhiteSpace(profile.HostKeyChecking))
            sb.Append(" -o StrictHostKeyChecking=").Append(profile.HostKeyChecking);

        // Nice default: keep connections alive a bit
        sb.Append(" -o ServerAliveInterval=30 -o ServerAliveCountMax=3");

        // Jump host support
        if (!string.IsNullOrWhiteSpace(profile.JumpHostId) && allProfiles != null)
        {
            var jumpHost = allProfiles.FirstOrDefault(h => h.Id == profile.JumpHostId);
            if (jumpHost is not null && string.IsNullOrEmpty(jumpHost.IdentityFile) == false)
            {
                sb.Append(" -o ")
                  .Append($"ProxyCommand=\"ssh -i {jumpHost.IdentityFile} -W %h:%p ")
                  .Append(jumpHost.Username)
                  .Append('@')
                  .Append(jumpHost.Host);

                if (jumpHost.Port != 22)
                    sb.Append(':').Append(jumpHost.Port);
                sb.Append('\"');
            }
            else if (jumpHost is not null)
            {
                sb.Append(" -J ")
                  .Append(jumpHost.Username)
                  .Append('@')
                  .Append(jumpHost.Host);
                
                if (jumpHost.Port != 22)
                    sb.Append(':').Append(jumpHost.Port);
            }
        }

        // Target
        sb.Append(' ')
          .Append(profile.Username)
          .Append('@')
          .Append(profile.Host);

        if (!string.IsNullOrWhiteSpace(profile.ExtraArgs))
            sb.Append(' ').Append(profile.ExtraArgs);

        return sb.ToString();
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "\\\"") + "\"";
}
