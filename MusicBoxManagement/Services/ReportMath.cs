using System;

namespace MusicBoxManagement.Services
{
    public static class ReportMath
    {
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        public static double OpeningMinutes(DateTimeOffset start, DateTimeOffset end)
        {
            if (end <= start) return 0;
            var firstDay = start.ToOffset(VietnamOffset).Date;
            var lastDay = end.ToOffset(VietnamOffset).Date;
            if (firstDay == lastDay) return DayMinutes(start, end, firstDay);
            var middleDays = (lastDay - firstDay).Days - 1;
            return DayMinutes(start, end, firstDay) + middleDays * 780d +
                DayMinutes(start, end, lastDay);
        }

        public static double UsedMinutes(DateTimeOffset sessionStart, DateTimeOffset sessionEnd,
            DateTimeOffset windowStart, DateTimeOffset windowEnd)
        {
            var start = sessionStart > windowStart ? sessionStart : windowStart;
            var end = sessionEnd < windowEnd ? sessionEnd : windowEnd;
            return OpeningMinutes(start, end);
        }

        private static double OverlapMinutes(DateTimeOffset start, DateTimeOffset end,
            DateTimeOffset shiftStart, DateTimeOffset shiftEnd)
        {
            var overlapStart = start > shiftStart ? start : shiftStart;
            var overlapEnd = end < shiftEnd ? end : shiftEnd;
            return overlapEnd > overlapStart ? (overlapEnd - overlapStart).TotalMinutes : 0;
        }

        private static double DayMinutes(DateTimeOffset start, DateTimeOffset end, DateTime day)
        {
            var morningStart = new DateTimeOffset(day.AddHours(9), VietnamOffset).ToUniversalTime();
            var morningEnd = new DateTimeOffset(day.AddHours(12), VietnamOffset).ToUniversalTime();
            var afternoonStart = new DateTimeOffset(day.AddHours(13), VietnamOffset).ToUniversalTime();
            var afternoonEnd = new DateTimeOffset(day.AddHours(23), VietnamOffset).ToUniversalTime();
            return OverlapMinutes(start, end, morningStart, morningEnd) +
                OverlapMinutes(start, end, afternoonStart, afternoonEnd);
        }
    }
}
