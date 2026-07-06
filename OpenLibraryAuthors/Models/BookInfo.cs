namespace OpenLibraryAuthors.Models;

public record BookInfo(
    string Title,
    int? Year, 
    string[] Languages,
    double? Rating
);