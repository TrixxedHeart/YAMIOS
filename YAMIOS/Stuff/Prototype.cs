using System.Collections.Generic;
using System.Linq;
// TODO: i think im missing something to make this significantly easier for myself but we're just gonna do it like this for now
namespace YAMIOS.Stuff
{
    public class Prototype
    {
        public required string Name { get; set; }
        public required string ID { get; set; }
        public required string Type { get; set; }
        public string? Description { get; set; }
        public List<string> Parent { get; set; } = new();
        public bool? Abstract { get; set; }
        public List<Component> Components { get; set; } = new();
        public string? Suffix { get; set; }
        
        // Additional EntityPrototype fields
        public string? CustomLocalizationID { get; set; }
        public bool MapSavable { get; set; } = true;
        public HashSet<string>? Categories { get; set; }
        
        // Placement properties
        public string? PlacementMode { get; set; }
        public int? PlacementRange { get; set; }
        public string? PlacementOffset { get; set; }
        public List<int>? MountingPoints { get; set; }
        
        // Additional properties that might exist in YAML but not defined above
        public Dictionary<string, object> AdditionalProperties { get; set; } = new();
        
        // Track which optional fields are enabled for YAML serialization
        public HashSet<string> EnabledOptionalFields { get; set; } = new();
        
        // Helper method to check if an optional field is enabled
        public bool IsOptionalFieldEnabled(string fieldName)
        {
            return EnabledOptionalFields.Contains(fieldName.ToLower());
        }
        
        // Helper method to enable/disable an optional field
        public void SetOptionalFieldEnabled(string fieldName, bool enabled)
        {
            var lowerFieldName = fieldName.ToLower();
            if (enabled)
            {
                EnabledOptionalFields.Add(lowerFieldName);
            }
            else
            {
                EnabledOptionalFields.Remove(lowerFieldName);
                // Clear the field value when disabled
                SetPropertyValue(fieldName, GetDefaultValueForField(fieldName));
            }
        }
        
        // Helper method to get default value for a field
        private object? GetDefaultValueForField(string fieldName)
        {
            return fieldName.ToLower() switch
            {
                "name" => null,
                "description" => null,
                "suffix" => null,
                "parent" => new List<string>(),
                "abstract" => null,
                "localizationid" => null,
                "save" => true,
                "categories" => null,
                "placement.mode" => null,
                "placement.range" => null,
                "placement.offset" => null,
                _ => null
            };
        }
        
        // Helper method to get any property value
        public object? GetPropertyValue(string propertyName)
        {
            return propertyName.ToLower() switch
            {
                "name" => Name,
                "id" => ID,
                "type" => Type,
                "description" => Description,
                "parent" => Parent,
                "abstract" => Abstract,
                "suffix" => Suffix,
                "localizationid" => CustomLocalizationID,
                "save" => MapSavable,
                "categories" => Categories,
                "placement.mode" => PlacementMode,
                "placement.range" => PlacementRange,
                "placement.offset" => PlacementOffset,
                "placement.mountingpoints" => MountingPoints,
                _ => AdditionalProperties.TryGetValue(propertyName, out var value) ? value : null
            };
        }
        
        // Helper method to set any property value
        public void SetPropertyValue(string propertyName, object? value)
        {
            switch (propertyName.ToLower())
            {
                case "name":
                    Name = value?.ToString() ?? "";
                    break;
                case "id":
                    ID = value?.ToString() ?? "";
                    break;
                case "type":
                    Type = value?.ToString() ?? "";
                    break;
                case "description":
                    Description = value?.ToString();
                    break;
                case "parent":
                    if (value is List<string> parentList)
                        Parent = parentList;
                    else if (value is string parentString)
                        Parent = string.IsNullOrEmpty(parentString) ? new List<string>() : 
                                parentString.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                    break;
                case "abstract":
                    Abstract = value is bool b ? b : bool.TryParse(value?.ToString(), out var ab) ? ab : null;
                    break;
                case "suffix":
                    Suffix = value?.ToString();
                    break;
                case "localizationid":
                    CustomLocalizationID = value?.ToString();
                    break;
                case "save":
                    MapSavable = value is bool ms ? ms : bool.TryParse(value?.ToString(), out var msb) ? msb : true;
                    break;
                case "categories":
                    if (value is HashSet<string> catSet)
                        Categories = catSet;
                    else if (value is List<string> catList)
                        Categories = catList.ToHashSet();
                    else if (value is string catString)
                        Categories = string.IsNullOrEmpty(catString) ? null : 
                                   catString.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToHashSet();
                    break;
                case "placement.mode":
                    PlacementMode = value?.ToString();
                    break;
                case "placement.range":
                    PlacementRange = value is int pr ? pr : int.TryParse(value?.ToString(), out var pri) ? pri : null;
                    break;
                case "placement.offset":
                    PlacementOffset = value?.ToString();
                    break;
                case "placement.mountingpoints":
                    if (value is List<int> mpList)
                        MountingPoints = mpList;
                    break;
                default:
                    if (value != null)
                        AdditionalProperties[propertyName] = value;
                    else
                        AdditionalProperties.Remove(propertyName);
                    break;
            }
        }
    }
}