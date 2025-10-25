using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.API.Models
{
    public class ImportedDocument
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string FileType { get; set; } = string.Empty; // CSV, PDF, etc.
        
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ProcessedAt { get; set; }
        
        public int? TransactionCount { get; set; } // Number of transactions imported
        
        public bool IsProcessed { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? DeletedAt { get; set; } = null;
    }
}
