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
/// Applies catalog rules before metadata lookup and STRM generation.
/// </summary>
internal static class CatalogRuleFilter
{
    private static readonly string[] ExcludedMoviePrefixes =
    [
        "EN -",
        "ES -",
        "ES-DO -",
        "ES- -",
        "FR -",
        "IT -",
        "AR -",
        "TR -",
        "RU -",
        "SOC -",
    ];

    private static readonly Regex[] ExcludedMoviePatterns =
    [
        new(@"\bvs\.?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bOlympics?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bBasketball\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bSoccer\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bUFC\s+on\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bFight\s+Night\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bBundesliga\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bPremier\s+League\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    /// <summary>
    /// Determines whether a movie should continue through the sync pipeline.
    /// </summary>
    /// <param name="title">Original provider title.</param>
    /// <param name="categoryNames">Provider categories containing the movie.</param>
    /// <returns>True when the movie should be included.</returns>
    internal static bool ShouldIncludeMovie(
        string? title,
        IEnumerable<string?> categoryNames)
    {
        var value = title?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            return false;
        }

        if (ExcludedMoviePrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var searchableText = string.Join(
            " ",
            new[] { value }.Concat(
                categoryNames.Where(name => !string.IsNullOrWhiteSpace(name))));

        return !ExcludedMoviePatterns.Any(
            pattern => pattern.IsMatch(searchableText));
    }
}