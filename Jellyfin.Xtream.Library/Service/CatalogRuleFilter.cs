// Copyright (C) 2024 Roland Breitschaft
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Xtream.Library.Service;

/// <summary>
/// Applies provider-specific catalog rules before metadata lookup and STRM generation.
/// </summary>
internal static class CatalogRuleFilter
{
    /// <summary>
    /// Determines whether a movie should continue through the sync pipeline.
    /// </summary>
    /// <param name="title">Original provider title.</param>
    /// <param name="categoryNames">Provider categories containing the movie.</param>
    /// <param name="provider">Provider-specific catalog rule configuration.</param>
    /// <returns>True when the movie should be included.</returns>
    internal static bool ShouldIncludeMovie(
        string? title,
        IEnumerable<string?> categoryNames,
        ProviderConfig provider)
    {
        if (!provider.EnableCatalogRules)
        {
            return true;
        }

        var value = title?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return false;
        }

        var categories = categoryNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToArray();

        var excludedPrefixes = ParseList(provider.CatalogExcludedMoviePrefixes);
        if (excludedPrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var searchableText = string.Join(" ", new[] { value }.Concat(categories));
        foreach (var pattern in ParseList(provider.CatalogExcludedMoviePatterns))
        {
            try
            {
                if (Regex.IsMatch(
                    searchableText,
                    pattern,
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1)))
                {
                    return false;
                }
            }
            catch (ArgumentException)
            {
                // Ignore invalid user-supplied regular expressions.
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological expression must not stop the complete sync.
            }
        }

        var includedTags = ParseList(provider.CatalogIncludedTags);
        if (includedTags.Length == 0)
        {
            return true;
        }

        bool includeMatch = false;

        if (provider.CatalogMatchProviderCategories)
        {
            includeMatch = categories.Any(category =>
                includedTags.Any(tag => ContainsWholeTag(category, tag)));
        }

        if (!includeMatch && provider.CatalogMatchTitlePrefixes)
        {
            includeMatch = includedTags.Any(tag =>
                StartsWithTag(value, tag));
        }

        if (includeMatch)
        {
            return true;
        }

        return provider.CatalogNoMatchBehavior.Equals(
            "Keep",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a series should continue through the sync pipeline.
    /// </summary>
    /// <param name="title">Original provider title.</param>
    /// <param name="categoryNames">Provider categories containing the series.</param>
    /// <param name="provider">Provider-specific series catalog configuration.</param>
    /// <returns>True when the series should be included.</returns>
    internal static bool ShouldIncludeSeries(
        string? title,
        IEnumerable<string?> categoryNames,
        ProviderConfig provider)
    {
        if (!provider.EnableSeriesCatalogRules)
        {
            return true;
        }

        var value = title?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return false;
        }

        var categories = categoryNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToArray();

        var excludedPrefixes = ParseList(
            provider.SeriesCatalogExcludedPrefixes);

        if (excludedPrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var searchableText = string.Join(
            " ",
            new[] { value }.Concat(categories));

        foreach (var pattern in ParseList(
            provider.SeriesCatalogExcludedPatterns))
        {
            try
            {
                if (Regex.IsMatch(
                    searchableText,
                    pattern,
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1)))
                {
                    return false;
                }
            }
            catch (ArgumentException)
            {
                // Ignore invalid user-supplied regular expressions.
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological expression must not stop the sync.
            }
        }

        var includedTags = ParseList(
            provider.SeriesCatalogIncludedTags);

        if (includedTags.Length == 0)
        {
            return true;
        }

        bool includeMatch = false;

        if (provider.SeriesCatalogMatchProviderCategories)
        {
            includeMatch = categories.Any(category =>
                includedTags.Any(tag =>
                    ContainsWholeTag(category, tag)));
        }

        if (!includeMatch &&
            provider.SeriesCatalogMatchTitlePrefixes)
        {
            includeMatch = includedTags.Any(tag =>
                StartsWithTag(value, tag));
        }

        if (includeMatch)
        {
            return true;
        }

        return provider.SeriesCatalogNoMatchBehavior.Equals(
            "Keep",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a failed series should be retried.
    /// </summary>
    /// <param name="title">Original provider title.</param>
    /// <param name="categoryNames">Stored provider category names.</param>
    /// <param name="provider">Provider-specific series catalog configuration.</param>
    /// <returns>True when the failed series should be retried.</returns>
    internal static bool ShouldRetrySeries(
        string? title,
        IEnumerable<string?> categoryNames,
        ProviderConfig provider)
    {
        var categories = categoryNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToArray();

        if (categories.Length > 0)
        {
            return ShouldIncludeSeries(title, categories, provider);
        }

        if (!provider.EnableSeriesCatalogRules)
        {
            return true;
        }

        var value = title?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return false;
        }

        if (ParseList(provider.SeriesCatalogExcludedPrefixes)
            .Any(prefix => value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        foreach (var pattern in ParseList(
            provider.SeriesCatalogExcludedPatterns))
        {
            try
            {
                if (Regex.IsMatch(
                    value,
                    pattern,
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1)))
                {
                    return false;
                }
            }
            catch (ArgumentException)
            {
                // Ignore invalid user-supplied regular expressions.
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological expression must not stop the retry.
            }
        }

        var includedTags = ParseList(
            provider.SeriesCatalogIncludedTags);

        if (includedTags.Length == 0)
        {
            return true;
        }

        if (provider.SeriesCatalogMatchTitlePrefixes &&
            includedTags.Any(tag => StartsWithTag(value, tag)))
        {
            return true;
        }

        if (!provider.SeriesCatalogMatchProviderCategories)
        {
            return provider.SeriesCatalogNoMatchBehavior.Equals(
                "Keep",
                StringComparison.OrdinalIgnoreCase);
        }

        // Older failed items have no stored category names. Keep them
        // eligible unless their title clearly matches an exclusion rule.
        return true;
    }

    private static string[] ParseList(string? value)
    {
        return (value ?? string.Empty)
            .Split(
                [';', ',', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(item => !item.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ContainsWholeTag(string text, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(tag)}(?![\p{{L}}\p{{N}}])";

        return Regex.IsMatch(
            text,
            pattern,
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
    }

    private static bool StartsWithTag(string title, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var pattern = $@"^\s*[\[\(]?\s*{Regex.Escape(tag)}(?![\p{{L}}\p{{N}}])";

        return Regex.IsMatch(
            title,
            pattern,
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
    }
}
