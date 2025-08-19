using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
// this doesnt work i think
namespace YAMIOS.Backend
{
    public class ComponentInfo
    {
        public string ComponentName { get; set; } = "";
        public string Summary { get; set; } = "";
        public string FilePath { get; set; } = "";
        public Dictionary<string, ComponentField> Fields { get; set; } = new();
        public List<string> CommonFields { get; set; } = new();
    }

    public class ComponentField
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string? DefaultValue { get; set; }
        public bool IsRequired { get; set; }
    }

    public static class ComponentScanner
    {
        private static readonly Dictionary<string, ComponentInfo> _componentInfos = new();
        private static bool _isScanned = false;

        public static ComponentInfo? GetComponentInfo(string componentName)
        {
            // Remove "Component" suffix if present for matching
            var cleanName = componentName.EndsWith("Component") ? 
                componentName.Substring(0, componentName.Length - 9) : componentName;
            
            return _componentInfos.TryGetValue(cleanName, out var info) ? info : 
                   _componentInfos.TryGetValue(componentName, out info) ? info : null;
        }

        public static IEnumerable<string> GetAllComponentNames()
        {
            return _componentInfos.Keys.Where(k => !k.EndsWith("Component"));
        }

        public static IEnumerable<ComponentInfo> GetAllComponents()
        {
            return _componentInfos.Values.Distinct();
        }

        public static void ScanComponents(string ss14RepoRoot)
        {
            if (_isScanned) return;
            
            _componentInfos.Clear();
            
            try
            {
                // Common SS14 component locations
                var searchPaths = new[]
                {
                    Path.Combine(ss14RepoRoot, "Content.Shared", "Components"),
                    Path.Combine(ss14RepoRoot, "Content.Server", "Components"),
                    Path.Combine(ss14RepoRoot, "Content.Client", "Components"),
                    Path.Combine(ss14RepoRoot, "Content.Shared"),
                    Path.Combine(ss14RepoRoot, "Content.Server"),
                    Path.Combine(ss14RepoRoot, "Content.Client")
                };

                foreach (var searchPath in searchPaths)
                {
                    if (Directory.Exists(searchPath))
                    {
                        ScanDirectory(searchPath);
                    }
                }
                
                ScanPrototypeFields(ss14RepoRoot);
                
                _isScanned = true;
                System.Diagnostics.Debug.WriteLine($"Scanned {_componentInfos.Count} component entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning components: {ex.Message}");
            }
        }

        private static void ScanDirectory(string directory)
        {
            try
            {
                var csFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                    .Where(f => f.Contains("Component") || f.Contains("System"))
                    .ToArray();

                foreach (var file in csFiles)
                {
                    ParseComponentFile(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning directory {directory}: {ex.Message}");
            }
        }

        private static void ParseComponentFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                
                // Look for component classes with documentation
                var componentMatches = Regex.Matches(content, 
                    @"/// <summary>\s*\n((?:///.*\n)*?)/// </summary>\s*\n(?:.*\n)*?(?:public (?:sealed )?(?:partial )?class|public (?:sealed )?(?:partial )?struct)\s+(\w*Component|\w*System)",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase);

                foreach (Match match in componentMatches)
                {
                    var summarySection = match.Groups[1].Value;
                    var className = match.Groups[2].Value;
                    
                    // Clean up the summary
                    var summary = Regex.Replace(summarySection, @"///\s*", "").Trim();
                    summary = Regex.Replace(summary, @"\s+", " "); // Normalize whitespace
                    
                    if (!string.IsNullOrEmpty(summary) && !string.IsNullOrEmpty(className))
                    {
                        var cleanName = className.EndsWith("Component") ? 
                            className.Substring(0, className.Length - 9) : className;
                        
                        var componentInfo = new ComponentInfo
                        {
                            ComponentName = className,
                            Summary = summary,
                            FilePath = filePath
                        };
                        
                        ParseComponentFields(content, componentInfo);
                        
                        _componentInfos[className] = componentInfo;
                        
                        System.Diagnostics.Debug.WriteLine($"Found component {className} with {componentInfo.Fields.Count} fields");
                    }
                }
                
                var simpleMatches = Regex.Matches(content,
                    @"/// (.+)\s*\n(?:.*\n)*?(?:public (?:sealed )?(?:partial )?class|public (?:sealed )?(?:partial )?struct)\s+(\w*Component|\w*System)",
                    RegexOptions.Multiline);
                
                foreach (Match match in simpleMatches)
                {
                    var summary = match.Groups[1].Value.Trim();
                    var className = match.Groups[2].Value;
                    
                    var cleanName = className.EndsWith("Component") ? 
                        className.Substring(0, className.Length - 9) : className;
                    
                    // Only add if we don't already have info for this component
                    if (!_componentInfos.ContainsKey(cleanName) && !string.IsNullOrEmpty(summary))
                    {
                        var componentInfo = new ComponentInfo
                        {
                            ComponentName = className,
                            Summary = summary,
                            FilePath = filePath
                        };
                        
                        ParseComponentFields(content, componentInfo);
                        
                        _componentInfos[cleanName] = componentInfo;
                        _componentInfos[className] = componentInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing file {filePath}: {ex.Message}");
            }
        }

        private static void ParseComponentFields(string content, ComponentInfo componentInfo)
        {
            // Look for public properties and fields with [DataField] attributes
            var fieldMatches = Regex.Matches(content,
                @"\[DataField.*?\]\s*(?:///.*?\n\s*)?public\s+(\w+(?:<.*?>)?(?:\??)?)\s+(\w+)\s*(?:{[^}]*}|;)",
                RegexOptions.Multiline | RegexOptions.Singleline);

            foreach (Match match in fieldMatches)
            {
                var fieldType = match.Groups[1].Value;
                var fieldName = match.Groups[2].Value;
                
                var yamlFieldName = char.ToLower(fieldName[0]) + fieldName.Substring(1);

                var field = new ComponentField
                {
                    Name = yamlFieldName,
                    Type = CleanUpTypeName(fieldType),
                    IsRequired = !fieldType.Contains("?") && !fieldType.Contains("= ")
                };

                componentInfo.Fields[yamlFieldName] = field;
            }
            
            var propertyMatches = Regex.Matches(content,
                @"public\s+(\w+(?:<.*?>)?(?:\??)?)\s+(\w+)\s*{\s*get;\s*set;\s*}",
                RegexOptions.Multiline);

            foreach (Match match in propertyMatches)
            {
                var fieldType = match.Groups[1].Value;
                var fieldName = match.Groups[2].Value;
                var yamlFieldName = char.ToLower(fieldName[0]) + fieldName.Substring(1);

                // Only add if not already present from DataField
                if (!componentInfo.Fields.ContainsKey(yamlFieldName))
                {
                    var field = new ComponentField
                    {
                        Name = yamlFieldName,
                        Type = CleanUpTypeName(fieldType),
                        IsRequired = false
                    };

                    componentInfo.Fields[yamlFieldName] = field;
                }
            }
        }

        private static void ScanPrototypeFields(string ss14RepoRoot)
        {
            try
            {
                var prototypesPath = Path.Combine(ss14RepoRoot, "Resources", "Prototypes");
                if (!Directory.Exists(prototypesPath)) return;

                var yamlFiles = Directory.GetFiles(prototypesPath, "*.yml", SearchOption.AllDirectories)
                    .Take(50);

                var componentFieldUsage = new Dictionary<string, HashSet<string>>();

                foreach (var yamlFile in yamlFiles)
                {
                    try
                    {
                        var prototypes = YamlPrototypeLoader.Load(yamlFile);
                        foreach (var prototype in prototypes)
                        {
                            foreach (var component in prototype.Components)
                            {
                                var componentName = component.Name;
                                if (!componentFieldUsage.ContainsKey(componentName))
                                    componentFieldUsage[componentName] = new HashSet<string>();

                                foreach (var field in component.Fields.Keys)
                                {
                                    componentFieldUsage[componentName].Add(field);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be parsed, don't explode.
                        continue;
                    }
                }
                
                foreach (var kvp in componentFieldUsage)
                {
                    var componentName = kvp.Key;
                    var fields = kvp.Value;

                    if (_componentInfos.TryGetValue(componentName, out var componentInfo))
                    {
                        componentInfo.CommonFields = fields.ToList();
                        
                        foreach (var fieldName in fields)
                        {
                            if (!componentInfo.Fields.ContainsKey(fieldName))
                            {
                                componentInfo.Fields[fieldName] = new ComponentField
                                {
                                    Name = fieldName,
                                    Type = "unknown",
                                    Description = "Field found in existing prototypes",
                                    IsRequired = false
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning prototype fields: {ex.Message}");
            }
        }

        private static string CleanUpTypeName(string typeName)
        {
            var cleaned = typeName.Replace("?", "").Trim();
            
            if (cleaned.StartsWith("List<") || cleaned.StartsWith("IList<"))
                return "list";
            if (cleaned.StartsWith("Dictionary<") || cleaned.StartsWith("IDictionary<"))
                return "dictionary";
            if (cleaned == "string" || cleaned == "String")
                return "string";
            if (cleaned == "int" || cleaned == "Int32")
                return "number";
            if (cleaned == "float" || cleaned == "Single" || cleaned == "double" || cleaned == "Double")
                return "number";
            if (cleaned == "bool" || cleaned == "Boolean")
                return "boolean";
            
            return cleaned.ToLower();
        }

        public static void ClearCache()
        {
            _componentInfos.Clear();
            _isScanned = false;
        }
    }
}
