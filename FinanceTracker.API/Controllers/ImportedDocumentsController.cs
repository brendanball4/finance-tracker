using FinanceTracker.API.Models;
using FinanceTracker.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportedDocumentsController : ControllerBase
    {
        private readonly IImportedDocumentService _documentService;
        private readonly IPdfProcessingService _pdfProcessingService;
        private readonly ITransactionService _transactionService;
        private readonly IWebHostEnvironment _environment;

        public ImportedDocumentsController(
            IImportedDocumentService documentService, 
            IPdfProcessingService pdfProcessingService,
            ITransactionService transactionService,
            IWebHostEnvironment environment)
        {
            _documentService = documentService;
            _pdfProcessingService = pdfProcessingService;
            _transactionService = transactionService;
            _environment = environment;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ImportedDocument>>> GetAll()
        {
            var documents = await _documentService.GetAllAsync();
            return Ok(documents);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ImportedDocument>> GetById(int id)
        {
            var document = await _documentService.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            return Ok(document);
        }

        [HttpPost("upload")]
        public async Task<ActionResult<ImportedDocument>> UploadPdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only PDF files are allowed");

            try
            {
                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads");
                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create document record
                var document = await _documentService.CreateAsync(
                    file.FileName,
                    filePath,
                    file.ContentType,
                    file.Length
                );

                // Process PDF in background
                _ = Task.Run(async () => await ProcessPdfAsync(document.Id, filePath));

                return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error uploading file: {ex.Message}");
            }
        }

        [HttpPut("{id}/processing-status")]
        public async Task<ActionResult<ImportedDocument>> UpdateProcessingStatus(
            int id, 
            [FromBody] UpdateProcessingStatusRequest request)
        {
            try
            {
                var document = await _documentService.UpdateProcessingStatusAsync(
                    id, 
                    request.IsProcessed, 
                    request.TransactionCount
                );

                return Ok(document);
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/process")]
        public async Task<ActionResult> ProcessDocument(int id)
        {
            var document = await _documentService.GetByIdAsync(id);
            if (document == null)
                return NotFound();

            try
            {
                await ProcessPdfAsync(id, document.FilePath);
                return Ok(new { message = "Document processing started" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing document: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var success = await _documentService.DeleteAsync(id);
            if (!success)
                return NotFound();

            return NoContent();
        }

        private async Task ProcessPdfAsync(int documentId, string filePath)
        {
            try
            {
                // Extract text from PDF
                var text = await _pdfProcessingService.ExtractTextFromPdfAsync(filePath);
                
                // Parse transactions from text
                var parsedTransactions = await _pdfProcessingService.ParseTransactionsFromTextAsync(text);
                
                // Create transactions in database
                var transactions = await _transactionService.CreateBulkAsync(parsedTransactions);
                
                // Update document processing status
                await _documentService.UpdateProcessingStatusAsync(
                    documentId, 
                    true, 
                    transactions.Count()
                );
            }
            catch (Exception ex)
            {
                // Update document with error status
                await _documentService.UpdateProcessingStatusAsync(
                    documentId, 
                    false, 
                    null
                );
            }
        }
    }

    public class UpdateProcessingStatusRequest
    {
        public bool IsProcessed { get; set; }
        public int? TransactionCount { get; set; }
    }
}
