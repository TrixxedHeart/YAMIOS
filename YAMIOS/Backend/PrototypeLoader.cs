using System.Collections.Generic;
using System.IO;
using System.Linq;
using YAMIOS.Stuff;
using YamlDotNet.RepresentationModel;

namespace YAMIOS.Backend
{
    public static class YamlPrototypeLoader
    {
        public static List<Prototype> Load(string filePath)
        {
            var prototypes = new List<Prototype>();
            using var reader = new StreamReader(filePath);
            var yaml = new YamlStream();
            yaml.Load(reader);

            foreach (var doc in yaml.Documents)
            {
                var root = doc.RootNode;
                if (root is YamlSequenceNode sequence)
                {
                    foreach (var entityNode in sequence)
                    {
                        if (entityNode is YamlMappingNode mapping)
                        {
                            var prototype = ParsePrototype(mapping);
                            if (prototype != null)
                                prototypes.Add(prototype);
                        }
                    }
                }
                else if (root is YamlMappingNode mapping)
                {
                    var prototype = ParsePrototype(mapping);
                    if (prototype != null)
                        prototypes.Add(prototype);
                }
            }

            // Inherit missing fields from parents
            var protoDict = prototypes.ToDictionary(p => p.ID);
            foreach (var proto in prototypes)
            {
                InheritFields(proto, protoDict);
            }

            return prototypes;
        }

        private static void InheritFields(Prototype proto, Dictionary<string, Prototype> protoDict)
        {
            foreach (var parentId in proto.Parent)
            {
                if (protoDict.TryGetValue(parentId, out var parent))
                {
                    // Inherit name if missing or empty
                    if (string.IsNullOrEmpty(proto.Name) && !string.IsNullOrEmpty(parent.Name))
                        proto.Name = parent.Name;
                    // Inherit description if missing or empty
                    if (string.IsNullOrEmpty(proto.Description) && !string.IsNullOrEmpty(parent.Description))
                        proto.Description = parent.Description;
                    // Inherit suffix if missing or empty
                    if (string.IsNullOrEmpty(proto.Suffix) && !string.IsNullOrEmpty(parent.Suffix))
                        proto.Suffix = parent.Suffix;
                    // Inherit from parent's parents
                    InheritFields(parent, protoDict);
                }
            }
        }

        private static readonly string[] BasicFields = new[]
        {
            "id", "name", "type", "description", "abstract", "suffix", "setname", "setdesc", "setsuffix",
            "categories", "localizationId", "hidespawnmenu", "save", "mountingpoints", "placementmode",
            "placementrange", "placementoffset", "customlocalizationid"
        };

        private static Prototype? ParsePrototype(YamlMappingNode mapping)
        {
            if (!mapping.Children.TryGetValue("id", out var idNode))
                return null;
            var id = idNode.ToString();
            var name = mapping.Children.TryGetValue("name", out var nameNode) ? nameNode.ToString() : "";
            var type = mapping.Children.TryGetValue("type", out var typeNode) ? typeNode.ToString() : "entity";

            var prototype = new Prototype
            {
                ID = id,
                Name = name,
                Type = type
            };
            var protoType = typeof(Prototype);

            foreach (var field in BasicFields)
            {
                if (field == "id" || field == "name" || field == "type") continue;
                if (!mapping.Children.TryGetValue(field, out var valueNode))
                    continue;
                var prop = protoType.GetProperty(
                    field.Equals("description", StringComparison.OrdinalIgnoreCase) ? "Description" :
                    field.Equals("abstract", StringComparison.OrdinalIgnoreCase) ? "Abstract" :
                    field.Equals("suffix", StringComparison.OrdinalIgnoreCase) ? "Suffix" :
                    field.Equals("setname", StringComparison.OrdinalIgnoreCase) ? "SetName" :
                    field.Equals("setdesc", StringComparison.OrdinalIgnoreCase) ? "SetDesc" :
                    field.Equals("setsuffix", StringComparison.OrdinalIgnoreCase) ? "SetSuffix" :
                    field.Equals("categories", StringComparison.OrdinalIgnoreCase) ? "Categories" :
                    field.Equals("localizationId", StringComparison.OrdinalIgnoreCase) ? "CustomLocalizationID" :
                    field.Equals("hidespawnmenu", StringComparison.OrdinalIgnoreCase) ? "HideSpawnMenu" :
                    field.Equals("save", StringComparison.OrdinalIgnoreCase) ? "MapSavable" :
                    field.Equals("mountingpoints", StringComparison.OrdinalIgnoreCase) ? "MountingPoints" :
                    field.Equals("placementmode", StringComparison.OrdinalIgnoreCase) ? "PlacementMode" :
                    field.Equals("placementrange", StringComparison.OrdinalIgnoreCase) ? "PlacementRange" :
                    field.Equals("placementoffset", StringComparison.OrdinalIgnoreCase) ? "PlacementOffset" :
                    field.Equals("customlocalizationid", StringComparison.OrdinalIgnoreCase) ? "CustomLocalizationID" :
                    field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop == null) continue;
                object? value = null;
                if (prop.PropertyType == typeof(string))
                    value = valueNode.ToString();
                else if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?))
                    value = bool.TryParse(valueNode.ToString(), out var b) ? b : null;
                else if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?))
                    value = int.TryParse(valueNode.ToString(), out var i) ? i : null;
                else if (prop.PropertyType == typeof(HashSet<string>))
                    value = valueNode is YamlSequenceNode seq ? new HashSet<string>(seq.Children.Select(c => c.ToString())) : null;
                else if (prop.PropertyType == typeof(List<int>))
                    value = valueNode is YamlSequenceNode seq ? seq.Children.Select(c => int.Parse(c.ToString())).ToList() : null;
                else
                    value = valueNode.ToString();

                prop.SetValue(prototype, value);
            }
            
            if (mapping.Children.TryGetValue("parent", out var parentNode))
            {
                if (parentNode is YamlSequenceNode parentSequence)
                {
                    prototype.Parent = parentSequence.Children
                        .Select(p => p.ToString())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();
                }
                else
                {
                    var parentStr = parentNode.ToString();
                    if (!string.IsNullOrEmpty(parentStr))
                        prototype.Parent.Add(parentStr);
                }
            }

            // Parse components
            if (mapping.Children.TryGetValue("components", out var componentsNode) &&
                componentsNode is YamlSequenceNode compSeq)
            {
                foreach (var comp in compSeq)
                {
                    if (comp is YamlMappingNode compMap)
                    {
                        var component = new Component
                        {
                            Name = compMap.Children.TryGetValue("type", out var compType) ?
                                compType.ToString() : "UnknownComponent"
                        };
                        ParseComponentFields(compMap, component.Fields);
                        prototype.Components.Add(component);
                    }
                }
            }

            return prototype;
        }

        private static void ParseComponentFields(YamlMappingNode mapping, Dictionary<string, object> fields)
        {
            foreach (var field in mapping.Children)
            {
                var key = field.Key.ToString();
                if (key == "type") continue; // Skip type, we don't use this, probably should though for editing non-entitys

                var value = ParseYamlValue(field.Value);
                fields[key] = value;
            }
        }

        private static object ParseYamlValue(YamlNode node)
        {
            switch (node)
            {
                case YamlScalarNode scalar:
                    return scalar.Value ?? "";
                
                case YamlSequenceNode sequence:
                    return sequence.Children.Select(ParseYamlValue).ToList();
                
                case YamlMappingNode mapping:
                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in mapping.Children)
                    {
                        dict[kvp.Key.ToString()] = ParseYamlValue(kvp.Value);
                    }
                    return dict;
                
                default:
                    return node.ToString();
            }
        }

        private static bool? ParseBool(string value)
        {
            if (bool.TryParse(value, out var result))
                return result;
            return null;
        }
    }
}