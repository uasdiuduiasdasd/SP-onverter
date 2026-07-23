namespace SPConverter.Models;

public class ConversionOptions
{
    public string TargetFormat { get; set; } = "JPEG";
    public int Quality { get; set; } = 100;
    public bool DeleteOriginalFiles { get; set; } = false;
    public bool ExtractAllPages { get; set; }
}
