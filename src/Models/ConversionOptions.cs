namespace SPConverter.Models;

public class ConversionOptions
{
    public string TargetFormat { get; set; } = "JPEG";
    public int Quality { get; set; } = 100;
    
    // Флаги из нашего обсуждения
    public bool IncludeSubfolders { get; set; } = true;
    public bool DeleteOriginalFiles { get; set; } = false;
    public bool ExtractAllPages { get; set; }
}
