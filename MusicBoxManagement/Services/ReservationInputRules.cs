namespace MusicBoxManagement.Services
{
    public sealed class ReservationInputResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }
        public string FullName { get; private set; }
        public string PhoneNumber { get; private set; }

        public static ReservationInputResult Success(string fullName, string phoneNumber)
        {
            return new ReservationInputResult { IsValid = true, FullName = fullName, PhoneNumber = phoneNumber };
        }

        public static ReservationInputResult Failure(string error)
        {
            return new ReservationInputResult { Error = error };
        }
    }

    public static class ReservationInputRules
    {
        public static ReservationInputResult Validate(string fullName, string phoneNumber)
        {
            var name = (fullName ?? "").Trim();
            if (name.Length == 0 || name.Length > 100)
                return ReservationInputResult.Failure("Họ tên phải có từ 1 đến 100 ký tự.");

            string normalizedPhone;
            if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out normalizedPhone))
                return ReservationInputResult.Failure("Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.");

            return ReservationInputResult.Success(name, normalizedPhone);
        }
    }
}
