using FinanceTracker.API.Data;
using FinanceTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.API.Services
{
    public interface IImportedDocumentService
    {
        Task<IEnumerable<ImportedDocument>> GetAllAsync();
        Task<ImportedDocument?> GetByIdAsync(int id);
        Task<ImportedDocument> CreateAsync(string fileName, string filePath, string fileType, long fileSizeBytes);
        Task<ImportedDocument> UpdateProcessingStatusAsync(int id, bool isProcessed, int? transactionCount = null);
        Task<bool> DeleteAsync(int id);
    }

    public class ImportedDocumentService : IImportedDocumentService
    {
        private readonly ApplicationDbContext _context;

        public ImportedDocumentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ImportedDocument>> GetAllAsync()
        {
            return await _context.ImportedDocuments
                .Where(d => d.DeletedAt == null)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<ImportedDocument?> GetByIdAsync(int id)
        {
            return await _context.ImportedDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null);
        }

        public async Task<ImportedDocument> CreateAsync(string fileName, string filePath, string fileType, long fileSizeBytes)
        {
            var document = new ImportedDocument
            {
                FileName = fileName,
                FilePath = filePath,
                FileType = fileType,
                UploadedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ImportedDocuments.Add(document);
            await _context.SaveChangesAsync();

            return document;
        }

        public async Task<ImportedDocument> UpdateProcessingStatusAsync(int id, bool isProcessed, int? transactionCount = null)
        {
            var document = await _context.ImportedDocuments.FindAsync(id);
            if (document == null)
                throw new ArgumentException("Document not found");

            document.IsProcessed = isProcessed;
            document.ProcessedAt = isProcessed ? DateTime.UtcNow : null;
            document.TransactionCount = transactionCount;
            document.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var document = await _context.ImportedDocuments.FindAsync(id);
            if (document == null)
                return false;

            document.DeletedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
