using System;

namespace MusicBoxManagement.Models
{
    public sealed class GuestScheduleSlot
    {
        public string LocalStartLabel { get; set; }
        public string LocalEndLabel { get; set; }
        public DateTimeOffset StartTimeUtc { get; set; }
        public DateTimeOffset EndTimeUtc { get; set; }
        public string Status { get; set; }
    }
}
