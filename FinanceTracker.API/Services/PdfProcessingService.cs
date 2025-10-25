using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace FinanceTracker.API.Services
{
    public interface IPdfProcessingService
    {
        Task<string> ExtractTextFromPdfAsync(string filePath);
        Task<List<ParsedTransaction>> ParseTransactionsFromTextAsync(string text);
    }

    public class PdfProcessingService : IPdfProcessingService
    {
        public async Task<string> ExtractTextFromPdfAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                using var pdfReader = new PdfReader(filePath);
                using var pdfDocument = new PdfDocument(pdfReader);
                
                var text = new System.Text.StringBuilder();
                
                for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
                {
                    var pageText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page));
                    text.AppendLine(pageText);
                }
                
                return text.ToString();
            });
        }

        public async Task<List<ParsedTransaction>> ParseTransactionsFromTextAsync(string text)
        {
            return await Task.Run(() =>
            {
                var transactions = new List<ParsedTransaction>();
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Patterns for different bank formats
                var scotiaDatePattern = @"(Mon|Tue|Wed|Thu|Fri|Sat|Sun),?\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\.?\s+\d{1,2},?\s+\d{4}";
                var standardDatePattern = @"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b";
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    // Look for Scotiabank date format first
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, scotiaDatePattern))
                    {
                        var transaction = TryParseScotiaTransactionLine(line, lines, i);
                        if (transaction != null)
                        {
                            transactions.Add(transaction);
                        }
                    }
                    // Then look for standard date patterns
                    else if (System.Text.RegularExpressions.Regex.IsMatch(line, standardDatePattern))
                    {
                        var transaction = TryParseTransactionLine(line, lines, i);
                        if (transaction != null)
                        {
                            transactions.Add(transaction);
                        }
                    }
                }
                
                return transactions;
            });
        }

        private ParsedTransaction? TryParseScotiaTransactionLine(string line, string[] allLines, int currentIndex)
        {
            try
            {
                // Extract date - format: "Tue, Oct. 14, 2025"
                var dateMatch = System.Text.RegularExpressions.Regex.Match(line, @"(Mon|Tue|Wed|Thu|Fri|Sat|Sun),?\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\.?\s+(\d{1,2}),?\s+(\d{4})");
                if (!dateMatch.Success)
                    return null;

                var dayName = dateMatch.Groups[1].Value;
                var monthName = dateMatch.Groups[2].Value;
                var day = dateMatch.Groups[3].Value;
                var year = dateMatch.Groups[4].Value;

                // Convert month name to number
                var monthNumber = monthName.ToLower() switch
                {
                    "jan" => "01", "feb" => "02", "mar" => "03", "apr" => "04",
                    "may" => "05", "jun" => "06", "jul" => "07", "aug" => "08",
                    "sep" => "09", "oct" => "10", "nov" => "11", "dec" => "12",
                    _ => "01"
                };

                var dateStr = $"{monthNumber}/{day.PadLeft(2, '0')}/{year}";
                if (!DateTime.TryParse(dateStr, out var date))
                    return null;

                // Extract amount - look for +$X.XX or -$X.XX patterns
                var amountMatch = System.Text.RegularExpressions.Regex.Match(line, @"([+-])\$(\d+\.\d{2})");
                if (!amountMatch.Success)
                    return null;

                var sign = amountMatch.Groups[1].Value;
                var amountStr = amountMatch.Groups[2].Value;
                if (!decimal.TryParse(amountStr, out var amount))
                    return null;

                // Only process withdrawals (negative amounts), skip deposits
                if (sign != "-")
                    return null;

                // Store withdrawal amount as positive (remove the minus sign)
                // amount is already positive from parsing, so no change needed

                // Extract description - look at next line(s) for transaction description
                var description = "";
                if (currentIndex + 1 < allLines.Length)
                {
                    var nextLine = allLines[currentIndex + 1].Trim();
                    // Skip lines that look like transaction types (Deposit, Pos Purchase, etc.)
                    if (!nextLine.Contains("Deposit") && !nextLine.Contains("Purchase") && 
                        !nextLine.Contains("Correction") && !nextLine.Contains("Transfer"))
                    {
                        description = nextLine;
                    }
                    else if (currentIndex + 2 < allLines.Length)
                    {
                        description = allLines[currentIndex + 2].Trim();
                    }
                }

                return new ParsedTransaction
                {
                    Date = date,
                    Amount = amount,
                    Description = description
                };
            }
            catch
            {
                return null;
            }
        }

        private ParsedTransaction? TryParseTransactionLine(string line, string[] allLines, int currentIndex)
        {
            try
            {
                // Try to extract date
                var dateMatch = System.Text.RegularExpressions.Regex.Match(line, @"\b(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})\b");
                if (!dateMatch.Success)
                    return null;

                var dateStr = dateMatch.Groups[1].Value;
                if (!DateTime.TryParse(dateStr, out var date))
                    return null;

                // Try to extract amount (look for $X.XX or X.XX patterns)
                var amountMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\$?\d+\.\d{2})");
                if (!amountMatch.Success)
                    return null;

                var amountStr = amountMatch.Groups[1].Value.Replace("$", "");
                if (!decimal.TryParse(amountStr, out var amount))
                    return null;

                // Extract description (everything except date and amount)
                var description = line
                    .Replace(dateStr, "")
                    .Replace(amountStr, "")
                    .Replace("$", "")
                    .Trim();

                // Clean up description
                description = System.Text.RegularExpressions.Regex.Replace(description, @"\s+", " ");

                return new ParsedTransaction
                {
                    Date = date,
                    Amount = amount,
                    Description = description
                };
            }
            catch
            {
                return null;
            }
        }
    }

    public class ParsedTransaction
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
