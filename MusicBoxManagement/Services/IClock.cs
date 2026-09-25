using System;

namespace MusicBoxManagement.Services
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow { get { return DateTimeOffset.UtcNow; } }
    }
}
