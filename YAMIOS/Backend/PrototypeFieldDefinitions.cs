namespace YAMIOS.Backend
{
    // is this tthe right way to do this? idk, crucify me
    public enum PrototypeFieldType
    {
        String,
        Boolean,
        Integer,
        StringArray,
        Vector2,
        Enum
    }

    public class PrototypeFieldDefinition
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public PrototypeFieldType Type { get; set; }
        public string? Description { get; set; }
        public bool IsRequired { get; set; }
        public object? DefaultValue { get; set; }
        public string[]? EnumValues { get; set; }
        public int? MaxLength { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsArray { get; set; }
        public bool IsOptional { get; set; } = true;
    }

    public static class EntityPrototypeFields
    {
        public static readonly List<PrototypeFieldDefinition> BasicFields = new()
        {
            new PrototypeFieldDefinition
            {
                Name = "type",
                DisplayName = "Type",
                Type = PrototypeFieldType.String,
                Description = "The prototype type (entity, construction, etc.)",
                IsRequired = true,
                IsOptional = false,
                DefaultValue = "entity"
            },
            new PrototypeFieldDefinition
            {
                Name = "id",
                DisplayName = "ID",
                Type = PrototypeFieldType.String,
                Description = "The unique identifier for this prototype",
                IsRequired = true,
                IsOptional = false,
                MaxLength = 100
            },
            new PrototypeFieldDefinition
            {
                Name = "name",
                DisplayName = "Name",
                Type = PrototypeFieldType.String,
                Description = "The display name of the entity",
                IsOptional = true,
                MaxLength = 200
            },
            new PrototypeFieldDefinition
            {
                Name = "description",
                DisplayName = "Description",
                Type = PrototypeFieldType.String,
                Description = "The description shown when examining the entity",
                IsOptional = true,
                MaxLength = 500
            },
            new PrototypeFieldDefinition
            {
                Name = "suffix",
                DisplayName = "Suffix",
                Type = PrototypeFieldType.String,
                Description = "Optional suffix for development menus",
                IsOptional = true,
                MaxLength = 100
            },
            new PrototypeFieldDefinition
            {
                Name = "parent",
                DisplayName = "Parent",
                Type = PrototypeFieldType.StringArray,
                Description = "The prototype(s) this inherits from",
                IsOptional = true,
                IsArray = true
            },
            new PrototypeFieldDefinition
            {
                Name = "abstract",
                DisplayName = "Abstract",
                Type = PrototypeFieldType.Boolean,
                Description = "Whether this is an abstract prototype (cannot be spawned directly)",
                IsOptional = true,
                DefaultValue = false
            },
            new PrototypeFieldDefinition
            {
                Name = "save",
                DisplayName = "Map Savable",
                Type = PrototypeFieldType.Boolean,
                Description = "Whether this entity will be saved by the map loader",
                IsOptional = true,
                DefaultValue = true
            },
            new PrototypeFieldDefinition
            {
                Name = "localizationId",
                DisplayName = "Localization ID",
                Type = PrototypeFieldType.String,
                Description = "Custom localization ID for name/description lookup",
                IsOptional = true,
                MaxLength = 100
            },
            new PrototypeFieldDefinition
            {
                Name = "categories",
                DisplayName = "Categories",
                Type = PrototypeFieldType.StringArray,
                Description = "Categories this prototype belongs to",
                IsOptional = true,
                IsArray = true
            }
        };

        public static readonly List<PrototypeFieldDefinition> PlacementFields = new()
        {
            new PrototypeFieldDefinition
            {
                Name = "placement.mode",
                DisplayName = "Placement Mode",
                Type = PrototypeFieldType.Enum,
                Description = "How this entity can be placed",
                IsOptional = true,
                EnumValues = new[] { "PlaceFree", "PlaceNearby", "SnapgridCenter", "SnapgridBorder", "AlignWall", "AlignWallProper" },
                DefaultValue = "PlaceFree"
            },
            new PrototypeFieldDefinition
            {
                Name = "placement.range",
                DisplayName = "Placement Range",
                Type = PrototypeFieldType.Integer,
                Description = "Maximum range for placement",
                IsOptional = true,
                DefaultValue = 200
            },
            new PrototypeFieldDefinition
            {
                Name = "placement.offset",
                DisplayName = "Placement Offset",
                Type = PrototypeFieldType.Vector2,
                Description = "Offset applied when placing (x, y)",
                IsOptional = true
            }
        };

        public static List<PrototypeFieldDefinition> GetAllFields()
        {
            var allFields = new List<PrototypeFieldDefinition>();
            allFields.AddRange(BasicFields);
            allFields.AddRange(PlacementFields);
            return allFields;
        }
    }
}
