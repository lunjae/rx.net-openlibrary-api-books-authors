using System.Collections.Generic;
using OpenLibraryAuthors.Models;

namespace OpenLibraryAuthors.Messages
{
    public record SearchAuthorRequest(string AuthorName);

    public record BookMessage(
        string Author,
        string Title,
        int? Year,
        string[] Languages,
        double? Rating);

    public record GetAuthorSummary(string AuthorName);

    public record AuthorSummary(
        string Author,
        List<BookInfo> Books,
        string Status
    );
}