using System;
using System.Collections.Generic;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class CheckInCheckResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }
        public DateTimeOffset CandidateEndUtc { get; private set; }

        public static CheckInCheckResult Success(DateTimeOffset candidateEndUtc)
        {
            return new CheckInCheckResult { IsValid = true, CandidateEndUtc = candidateEndUtc };
        }

        public static CheckInCheckResult Failure(string error)
        {
            return new CheckInCheckResult { Error = error };
        }
    }

    public static class CheckInRules
    {
        public static CheckInCheckResult Validate(Reservation reservation, DateTimeOffset nowUtc,
            bool roomActive, IEnumerable<Reservation> otherReservations, IEnumerable<RoomSession> sessions)
        {
            if (reservation.Status != ReservationStatuses.Confirmed ||
                nowUtc >= reservation.StartTime.AddMinutes(15))
                return CheckInCheckResult.Failure("Đặt phòng không còn hiệu lực để nhận phòng.");
            if (!roomActive)
                return CheckInCheckResult.Failure("Phòng đang ngừng hoạt động.");

            var candidateEnd = nowUtc.Add(reservation.EndTime - reservation.StartTime);
            var offset = TimeSpan.FromHours(7);
            var localStart = nowUtc.ToOffset(offset);
            var localEnd = candidateEnd.ToOffset(offset);
            var starts = localStart.TimeOfDay;
            var ends = localEnd.TimeOfDay;
            var sameDay = localStart.Date == localEnd.Date;
            var morning = sameDay && starts >= TimeSpan.FromHours(9) &&
                starts < TimeSpan.FromHours(12) && ends <= TimeSpan.FromHours(12);
            var evening = sameDay && starts >= TimeSpan.FromHours(13) &&
                starts < TimeSpan.FromHours(23) && ends <= TimeSpan.FromHours(23);
            if (!morning && !evening)
                return CheckInCheckResult.Failure("Thời gian sử dụng phải nằm trọn trong một ca mở cửa.");

            if (sessions.Any(item => item.Status == RoomSessionStatuses.Active &&
                (item.RoomId == reservation.RoomId || item.CustomerId == reservation.CustomerId)))
                return CheckInCheckResult.Failure("Phòng hoặc khách đang có phiên sử dụng.");

            if (otherReservations.Any(item => item.ReservationId != reservation.ReservationId &&
                item.Status == ReservationStatuses.Confirmed &&
                nowUtc < item.StartTime.AddMinutes(15) &&
                (item.RoomId == reservation.RoomId || item.CustomerId == reservation.CustomerId) &&
                AvailabilityRules.Overlaps(nowUtc, candidateEnd, item.StartTime, item.EndTime)))
                return CheckInCheckResult.Failure("Khoảng sử dụng sẽ trùng đặt phòng khác của phòng hoặc khách.");

            return CheckInCheckResult.Success(candidateEnd);
        }
    }
}
