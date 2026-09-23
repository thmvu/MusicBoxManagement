using System;
using System.Collections.Generic;
using System.Linq;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class CustomerService
    {
        private readonly ApplicationDbContext db;

        public CustomerService(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IList<Customer> List(string search)
        {
            var query = db.Customers.AsQueryable();
            var term = (search ?? "").Trim();
            if (term.Length > 0)
            {
                string phone;
                if (PhoneNumberNormalizer.TryNormalize(term, out phone))
                    query = query.Where(customer => customer.PhoneNumber == phone || customer.FullName.Contains(term));
                else
                    query = query.Where(customer => customer.FullName.Contains(term) || customer.PhoneNumber.Contains(term));
            }
            return query.OrderBy(customer => customer.FullName).ThenBy(customer => customer.CustomerId).ToList();
        }

        public CustomerFormViewModel GetForm(int id)
        {
            var customer = db.Customers.SingleOrDefault(item => item.CustomerId == id);
            if (customer == null) return null;
            return new CustomerFormViewModel
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                PhoneNumber = customer.PhoneNumber
            };
        }

        public int Create(CustomerFormViewModel model, string actorUserId)
        {
            var phone = Validate(model);
            if (db.Customers.Any(customer => customer.PhoneNumber == phone))
                throw new ArgumentException("Số điện thoại đã có khách hàng.");

            using (var transaction = db.Database.BeginTransaction())
            {
                var customer = new Customer { FullName = model.FullName.Trim(), PhoneNumber = phone };
                db.Customers.Add(customer);
                db.SaveChanges();
                Log(actorUserId, "Create", customer.CustomerId.ToString(), "Tạo khách hàng " + customer.FullName);
                db.SaveChanges();
                transaction.Commit();
                return customer.CustomerId;
            }
        }

        public bool Update(CustomerFormViewModel model, string actorUserId)
        {
            var phone = Validate(model);
            var customer = db.Customers.SingleOrDefault(item => item.CustomerId == model.CustomerId);
            if (customer == null) return false;
            if (db.Customers.Any(item => item.PhoneNumber == phone && item.CustomerId != customer.CustomerId))
                throw new ArgumentException("Số điện thoại đã có khách hàng.");

            customer.FullName = model.FullName.Trim();
            customer.PhoneNumber = phone;
            Log(actorUserId, "Update", customer.CustomerId.ToString(), "Cập nhật khách hàng " + customer.FullName);
            db.SaveChanges();
            return true;
        }

        // Reservation and walk-in use the same normalization and reuse an existing customer.
        // Guest input must never overwrite the stored name of that customer.
        public Customer FindByPhone(string input)
        {
            string phone;
            return PhoneNumberNormalizer.TryNormalize(input, out phone)
                ? db.Customers.SingleOrDefault(customer => customer.PhoneNumber == phone)
                : null;
        }

        private static string Validate(CustomerFormViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.FullName))
                throw new ArgumentException("Họ tên là bắt buộc.");
            string phone;
            if (!PhoneNumberNormalizer.TryNormalize(model.PhoneNumber, out phone))
                throw new ArgumentException("Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.");
            return phone;
        }

        private void Log(string actorUserId, string action, string entityId, string description)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorType = "Staff",
                UserId = actorUserId,
                Action = action,
                EntityName = "Customer",
                EntityId = entityId,
                Description = description,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
