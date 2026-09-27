using System.Collections.Generic;
using System.Linq;

namespace MusicBoxManagement.Services
{
    public sealed class OrderLineInput
    {
        public int ServiceId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class OrderInputResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }
        public IList<OrderLineInput> Lines { get; private set; }

        public static OrderInputResult Success(IList<OrderLineInput> lines)
        {
            return new OrderInputResult { IsValid = true, Lines = lines };
        }

        public static OrderInputResult Failure(string error)
        {
            return new OrderInputResult { Error = error };
        }
    }

    public static class OrderInputRules
    {
        public static OrderInputResult Validate(IEnumerable<OrderLineInput> input)
        {
            var source = input == null ? new List<OrderLineInput>() : input.ToList();
            if (source.Count == 0)
                return OrderInputResult.Failure("Vui lòng chọn ít nhất một món.");
            if (source.Any(item => item == null || item.ServiceId <= 0 || item.Quantity <= 0))
                return OrderInputResult.Failure("Món hoặc số lượng không hợp lệ.");

            var lines = new List<OrderLineInput>();
            foreach (var group in source.GroupBy(item => item.ServiceId))
            {
                var total = group.Sum(item => (long)item.Quantity);
                if (total > 10)
                    return OrderInputResult.Failure("Mỗi món chỉ được chọn tối đa 10 phần trong một lần gọi.");
                lines.Add(new OrderLineInput { ServiceId = group.Key, Quantity = (int)total });
            }
            return OrderInputResult.Success(lines);
        }
    }
}
