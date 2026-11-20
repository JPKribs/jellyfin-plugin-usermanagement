using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Jellyfin.Plugin.UserManagement.Models;
using Jellyfin.Plugin.UserManagement.Utilities;
using JPKribs.Jellyfin.Base;
using Xunit;

namespace Jellyfin.Plugin.UserManagement.Tests;

/// <summary>
/// The plugin's strings: every key a page uses has a string, and the strings the build embeds read in each language.
/// </summary>
public class TranslationsTests
{
    private static readonly Regex[] KeyPatterns =
    {
        new(@"data-str(?:-title|-label|-placeholder)?=""([^""]+)""", RegexOptions.Compiled),
        new(@"\btr\('([^']+)'", RegexOptions.Compiled),
        new(@"\bformat\('([^']+)'", RegexOptions.Compiled),
        new(@"\bhas\('([^']+)'", RegexOptions.Compiled)
    };

    /// <summary>
    /// Every key in a page's markup or script is a local string or a declared Jellyfin string.
    /// True: every word a page shows has a string in every language.
    /// False: a page would show a key, or its English fallback in every language.
    /// </summary>
    [Fact]
    public void WebKeys_AllHaveAString()
    {
        var project = ProjectFolder();
        var known = LocalKeys(project).Concat(JellyfinKeys(project)).ToHashSet(StringComparer.Ordinal);

        var missing = Directory.GetFiles(Path.Combine(project, "Web"))
            .SelectMany(file => KeyPatterns.SelectMany(p => p.Matches(File.ReadAllText(file)).Select(m => Path.GetFileName(file) + ": " + m.Groups[1].Value)))
            .Where(found => !known.Contains(found[(found.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// The pages find keys in their markup and scripts at all, so the key test above checks something.
    /// True: the patterns match how the pages use their strings.
    /// False: the key test would pass on pages it never read.
    /// </summary>
    [Fact]
    public void WebKeys_AreFound()
    {
        var project = ProjectFolder();

        var count = Directory.GetFiles(Path.Combine(project, "Web"))
            .Sum(file => KeyPatterns.Sum(p => p.Matches(File.ReadAllText(file)).Count));

        Assert.True(count > 100, "Only " + count + " keys found in Web.");
    }

    /// <summary>
    /// A declared Jellyfin string comes in another language, and a local string falls back to US English.
    /// True: the build embeds Jellyfin's translations, and a language with no local file still reads.
    /// False: the pages would show English in every language, or a key where a local string has no translation.
    /// </summary>
    [Fact]
    public void Strings_JellyfinStringIsTranslated_LocalStringFallsBack()
    {
        Assert.Equal("Speichern", BaseTranslations.Get(TranslationKeys.Save, "de"));
        Assert.Equal("Invite created.", BaseTranslations.Get(TranslationKeys.ToastInviteCreated, "de"));
    }

    /// <summary>
    /// A server message fills its placeholder in the requested language.
    /// True: the signup page shows the length rule with the group's number in it.
    /// False: the message would read with a raw placeholder.
    /// </summary>
    [Fact]
    public void PasswordValidator_MessageInLanguage_FillsPlaceholder()
    {
        var errors = PasswordValidator.Validate("abc", new PasswordPolicy { Enabled = true, MinLength = 8 }, "en-us");

        Assert.Equal("Password must be at least 8 characters long.", Assert.Single(errors));
    }

    /// <summary>
    /// The Translations route answers with the plugin's strings as JSON.
    /// True: a page's loadTranslations gets its strings.
    /// False: every page would fall back to its English markup.
    /// </summary>
    [Fact]
    public void TranslationsRoute_ServesTheStrings()
    {
        var controller = new Api.UserManagementController(null!, null!, null!, null!)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };

        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.FileContentResult>(controller.Translations("en-us"));
        using var body = JsonDocument.Parse(result.FileContents);
        var strings = body.RootElement.GetProperty("strings");

        Assert.Equal("Invites", strings.GetProperty("HeaderInvites").GetString());
        Assert.Equal("Save", strings.GetProperty("Save").GetString());
    }

    private static string ProjectFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Jellyfin.Plugin.UserManagement", "Web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Jellyfin.Plugin.UserManagement");
    }

    private static IEnumerable<string> LocalKeys(string project)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "Localization", "en-us.json")));
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
    }

    private static IEnumerable<string> JellyfinKeys(string project)
        => XDocument.Load(Path.Combine(project, "Jellyfin.Plugin.UserManagement.csproj"))
            .Descendants("JellyfinString")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .SelectMany(v => v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
}
