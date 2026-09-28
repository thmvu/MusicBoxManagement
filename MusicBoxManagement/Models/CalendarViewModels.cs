using System.Collections.Generic;

namespace MusicBoxManagement.Models
{
    public sealed class CalendarViewModel
    {
        public int? RoomId { get; set; }
        public IList<CalendarRoomViewModel> Rooms { get; set; }
    }

    public sealed class CalendarRoomViewModel
    {
        public int RoomId { get; set; }
        public string RoomCode { get; set; }
    }

    public sealed class CalendarEventViewModel
    {
        public string Title { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public string Url { get; set; }
        public string ClassName { get; set; }
    }
}
