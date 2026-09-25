using System;

namespace MusicBoxManagement.Services
{
    public sealed class BookingTimeResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }
        public DateTimeOffset EndTimeUtc { get; private set; }

        public static BookingTimeResult Success(DateTimeOffset endTimeUtc)
        {
            return new BookingTimeResult { IsValid = true, EndTimeUtc = endTimeUtc };
        }

        public static BookingTimeResult Failure(string error)
        {
            return new BookingTimeResult { Error = error };
        }
    }

    public static class BookingTimeRules
    {
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        public static BookingTimeResult Validate(DateTimeOffset startUtc, int durationMinutes, DateTimeOffset now)
        {
            if (startUtc.Offset != TimeSpan.Zero)
                return BookingTimeResult.Failure("Giờ đặt phải được chuyển sang UTC trước khi kiểm tra.");

            if (durationMinutes != 60 && durationMinutes != 90 && durationMinutes != 120 && durationMinutes != 180)
                return BookingTimeResult.Failure("Thời lượng chỉ được là 60, 90, 120 hoặc 180 phút.");

            if (startUtc < now.ToUniversalTime())
                return BookingTimeResult.Failure("Không thể đặt giờ trong quá khứ.");

            var localStart = startUtc.ToOffset(VietnamOffset);
            var localToday = now.ToOffset(VietnamOffset).Date;
            if (localStart.Date < localToday || localStart.Date > localToday.AddDays(30))
                return BookingTimeResult.Failure("Ngày đặt phải từ hôm nay đến 30 ngày tới.");

            var onHalfHour = localStart.Minute == 0 || localStart.Minute == 30;
            if (!onHalfHour || localStart.Second != 0 || localStart.Ticks % TimeSpan.TicksPerSecond != 0)
                return BookingTimeResult.Failure("Giờ bắt đầu phải đúng slot 30 phút.");

            var localEnd = localStart.AddMinutes(durationMinutes);
            var startTime = localStart.TimeOfDay;
            var endTime = localEnd.TimeOfDay;
            var morning = startTime >= TimeSpan.FromHours(9) && startTime < TimeSpan.FromHours(12) && endTime <= TimeSpan.FromHours(12) && localEnd.Date == localStart.Date;
            var evening = startTime >= TimeSpan.FromHours(13) && startTime < TimeSpan.FromHours(23) && endTime <= TimeSpan.FromHours(23) && localEnd.Date == localStart.Date;
            if (!morning && !evening)
                return BookingTimeResult.Failure("Khoảng đặt phải nằm trọn trong một ca mở cửa (09:00–12:00 hoặc 13:00–23:00).");

            return BookingTimeResult.Success(startUtc.AddMinutes(durationMinutes));
        }
    }
}
