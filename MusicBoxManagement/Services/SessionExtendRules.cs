using System;
using System.Collections.Generic;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class SessionExtendCheckResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }
        public DateTimeOffset NewEndUtc { get; private set; }

        public static SessionExtendCheckResult Success(DateTimeOffset newEndUtc)
        {
            return new SessionExtendCheckResult { IsValid = true, NewEndUtc = newEndUtc };
        }

        public static SessionExtendCheckResult Failure(string error)
        {
            return new SessionExtendCheckResult { Error = error };
        }
    }

    public static class SessionExtendRules
    {
        public static SessionExtendCheckResult Validate(RoomSession session, DateTimeOffset nowUtc,
            int minutes, IEnumerable<Reservation> reservations, IEnumerable<RoomSession> otherSessions = null)
        {
            if (session.Status != RoomSessionStatuses.Active || !session.ReservationId.HasValue ||
                !session.ExpectedEndTime.HasValue)
                return SessionExtendCheckResult.Failure("Chỉ phiên đang hoạt động từ đặt trước mới được gia hạn.");
            if (minutes != 30 && minutes != 60)
                return SessionExtendCheckResult.Failure("Chỉ được gia hạn thêm 30 hoặc 60 phút.");

            var oldEnd = session.ExpectedEndTime.Value;
            if (nowUtc > oldEnd)
                return SessionExtendCheckResult.Failure("Đã quá giờ dự kiến kết thúc. Vui lòng nhờ nhân viên xử lý trực tiếp.");
            var newEnd = oldEnd.AddMinutes(minutes);
            var offset = TimeSpan.FromHours(7);
            var localOldEnd = oldEnd.ToOffset(offset);
            var localNewEnd = newEnd.ToOffset(offset);
            var sameDay = localOldEnd.Date == localNewEnd.Date;
            var morning = sameDay && localOldEnd.TimeOfDay >= TimeSpan.FromHours(9) &&
                localOldEnd.TimeOfDay < TimeSpan.FromHours(12) && localNewEnd.TimeOfDay <= TimeSpan.FromHours(12);
            var evening = sameDay && localOldEnd.TimeOfDay >= TimeSpan.FromHours(13) &&
                localOldEnd.TimeOfDay < TimeSpan.FromHours(23) && localNewEnd.TimeOfDay <= TimeSpan.FromHours(23);
            if (!morning && !evening)
                return SessionExtendCheckResult.Failure("Không thể gia hạn qua giờ nghỉ hoặc giờ đóng cửa.");

            var conflict = reservations.Where(item => item.Status == ReservationStatuses.Confirmed &&
                    nowUtc < item.StartTime.AddMinutes(15) &&
                    (item.RoomId == session.RoomId || item.CustomerId == session.CustomerId) &&
                    AvailabilityRules.Overlaps(oldEnd, newEnd, item.StartTime, item.EndTime))
                .OrderBy(item => item.StartTime).FirstOrDefault();
            if (conflict != null)
            {
                var limit = conflict.StartTime < oldEnd ? oldEnd : conflict.StartTime;
                return SessionExtendCheckResult.Failure("Chỉ có thể sử dụng đến " +
                    limit.ToOffset(offset).ToString("dd/MM/yyyy HH:mm") + " vì đã có lịch tiếp theo.");
            }

            if (otherSessions != null && otherSessions.Any(item => item.RoomSessionId != session.RoomSessionId &&
                item.Status == RoomSessionStatuses.Active && item.ExpectedEndTime.HasValue &&
                (item.RoomId == session.RoomId || item.CustomerId == session.CustomerId) &&
                AvailabilityRules.Overlaps(oldEnd, newEnd, item.ActualStartTime, item.ExpectedEndTime.Value)))
                return SessionExtendCheckResult.Failure("Khoảng gia hạn trùng phiên sử dụng khác.");

            return SessionExtendCheckResult.Success(newEnd);
        }
    }
}
