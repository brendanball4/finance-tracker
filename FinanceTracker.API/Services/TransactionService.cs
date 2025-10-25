using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.API.Services
{
    public interface ITransactionService
    {
        Task<IEnumerable<Transaction>> GetAllAsync();
        Task<Transaction?> GetByIdAsync(int id);
        Task<Transaction> CreateAsync(decimal amount, DateTime date, string description, int? categoryId = null);
        Task<IEnumerable<Transaction>> CreateBulkAsync(IEnumerable<ParsedTransaction> parsedTransactions);
        Task<Transaction> UpdateAsync(int id, decimal amount, DateTime date, string description, int? categoryId = null);
        Task<bool> DeleteAsync(int id);
    }

    public class TransactionService : ITransactionService
    {
        private readonly ApplicationDbContext _context;

        public TransactionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Transaction>> GetAllAsync()
        {
            return await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.DeletedAt == null)
                .OrderByDescending(t => t.Date)
                .ToListAsync();
        }

        public async Task<Transaction?> GetByIdAsync(int id)
        {
            return await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null);
        }

        public async Task<Transaction> CreateAsync(decimal amount, DateTime date, string description, int? categoryId = null)
        {
            var transaction = new Transaction
            {
                Amount = amount,
                Date = date,
                Description = description,
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return transaction;
        }

        public async Task<IEnumerable<Transaction>> CreateBulkAsync(IEnumerable<ParsedTransaction> parsedTransactions)
        {
            var transactions = parsedTransactions.Select(pt => new Transaction
            {
                Amount = pt.Amount,
                Date = pt.Date,
                Description = pt.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            _context.Transactions.AddRange(transactions);
            await _context.SaveChangesAsync();

            return transactions;
        }

        public async Task<Transaction> UpdateAsync(int id, decimal amount, DateTime date, string description, int? categoryId = null)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
                throw new ArgumentException("Transaction not found");

            transaction.Amount = amount;
            transaction.Date = date;
            transaction.Description = description;
            transaction.CategoryId = categoryId;
            transaction.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
                return false;

            transaction.DeletedAt = DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
