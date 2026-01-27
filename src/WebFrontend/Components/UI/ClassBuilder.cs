namespace WebFrontend.Components.UI;

/// <summary>
/// Utility class for building CSS class strings with variant support, similar to class-variance-authority.
/// </summary>
public static class ClassBuilder
{
    /// <summary>
    /// Merges multiple class strings, removing duplicates and empty entries.
    /// </summary>
    public static string Merge(params string?[] classes)
    {
        var classList = new List<string>();
        
        foreach (var cls in classes)
        {
            if (string.IsNullOrWhiteSpace(cls))
                continue;
                
            var parts = cls.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (!classList.Contains(part))
                {
                    classList.Add(part);
                }
            }
        }
        
        return string.Join(" ", classList);
    }

    /// <summary>
    /// Builds a class string from base classes and variant-specific classes.
    /// </summary>
    public static string Build(string baseClasses, Dictionary<string, string>? variants = null, string? additionalClasses = null)
    {
        var allClasses = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(baseClasses))
        {
            allClasses.Add(baseClasses);
        }
        
        if (variants != null)
        {
            foreach (var variant in variants.Values)
            {
                if (!string.IsNullOrWhiteSpace(variant))
                {
                    allClasses.Add(variant);
                }
            }
        }
        
        if (!string.IsNullOrWhiteSpace(additionalClasses))
        {
            allClasses.Add(additionalClasses);
        }
        
        return Merge(allClasses.ToArray());
    }
}
