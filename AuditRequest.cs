using System.ComponentModel.DataAnnotations;

public class AuditRequest
{
    [Required(ErrorMessage = "Tytuł jest wymagany")]
    [StringLength(200, MinimumLength = 1)]
    [RegularExpression(
        @"^[\w\s.,!?-]+$",
        ErrorMessage = "Niedozwolone znaki w tytule")]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1, 5, ErrorMessage = "Priorytet musi być od 1 do 5")]
    public int Priority { get; set; }
}