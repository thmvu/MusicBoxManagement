using System;
using System.Globalization;

namespace MusicBoxManagement.Services
{
    public sealed class GuestBookingDateResult
    {
        public bool IsValid { get; private set; }
        public DateTimeOffset StartTimeUtc { get; private set; }

        public static GuestBookingDateResult Success(DateTimeOffset startTimeUtc)
        {
            return new GuestBookingDateResult { IsValid = true, StartTimeUtc = startTimeUtc };
        }

        public static GuestBookingDateResult Failure()
        {
            return new GuestBookingDateResult();
        }
    }

    public static class GuestBookingDateParser
    {
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        public static GuestBookingDateResult Parse(string bookingDate, string startSlot)
        {
            DateTime localStart;
            var input = (bookingDate ?? "") + " " + (startSlot ?? "");
            if (!DateTime.TryParseExact(input, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out localStart))
                return GuestBookingDateResult.Failure();

            return GuestBookingDateResult.Success(new DateTimeOffset(localStart, VietnamOffset).ToUniversalTime());
        }
    }
}
