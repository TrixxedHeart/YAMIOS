using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using YAMIOS.Stuff;
using YAMIOS.Backend;
using YAMIOS.Serialization;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using System.Text.Json;
using Avalonia.LogicalTree;
using System;
using System.Threading.Tasks;
using Avalonia.Layout;
using static Avalonia.Controls.ToolTip;

namespace YAMIOS
{
    public partial class MainWindow : Window
    {
        private List<Prototype> _prototypes = new();
        private Prototype? _selectedPrototype;
        private EditorConfig _config;

        public MainWindow()
        {
            InitializeComponent();
            _config = ConfigManager.Load();
            if (string.IsNullOrWhiteSpace(_config.SS14RepoRoot) || !Directory.Exists(_config.SS14RepoRoot))
            {
                _ = PromptForRepoRoot();
            }
            else
            {
                RepoStatus.Text = $"SS14 Repo: Connected";
                RepoStatus.Foreground = new SolidColorBrush(Color.Parse("#90EE90"));
                LogText.Text = "SS14 repo loaded from config.";
            }
        }

        private async Task PromptForRepoRoot()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select SS14 Repository Root Folder (containing .sln file)",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var selectedPath = folders[0].Path.LocalPath;
                
                // Validate this is actually an SS14 repo
                if (IsValidSS14Repository(selectedPath))
                {
                    InitializeRepoConnection(selectedPath);
                }
                else
                {
                    await ShowMessageBox("Invalid Repository", "Selected folder doesn't appear to be a valid SS14 repository. Please select the folder containing the .sln file.");
                    RepoStatus.Text = "No SS14 Repo";
                    RepoStatus.Foreground = new SolidColorBrush(Color.Parse("#FFD700"));
                    LogText.Text = "Invalid SS14 repo selected. Please try again.";
                }
            }
            else
            {
                RepoStatus.Text = "No SS14 Repo";
                LogText.Text = "Please select the SS14 repo root folder to enable features.";
            }
        }

        private async Task ShowMessageBox(string title, string message)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(20),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            messageBox.Content = textBlock;
            await messageBox.ShowDialog(this);
        }

        private bool IsValidSS14Repository(string path)
        {
            try
            {
                // Check for SS14 solution file
                var slnFiles = Directory.GetFiles(path, "*.sln");
                if (!slnFiles.Any(f => Path.GetFileName(f).ToLower().Contains("station") || 
                                      Path.GetFileName(f).ToLower().Contains("ss14")))
                    return false;

                // Check for Resources folder structure
                var resourcesPath = Path.Combine(path, "Resources");
                if (!Directory.Exists(resourcesPath))
                    return false;

                var texturesPath = Path.Combine(resourcesPath, "Textures");
                var prototypesPath = Path.Combine(resourcesPath, "Prototypes");
                
                return Directory.Exists(texturesPath) && Directory.Exists(prototypesPath);
            }
            catch
            {
                return false;
            }
        }

        private void InitializeRepoConnection(string repoPath)
        {
            _config.SS14RepoRoot = repoPath;
            ConfigManager.Save(_config);
            RepoStatus.Text = "SS14 Repo: Connected";
            RepoStatus.Foreground = new SolidColorBrush(Color.Parse("#90EE90"));
            LogText.Text = $"SS14 repo connected: {repoPath}";
            
            // Scan components in background
            Task.Run(() =>
            {
                try
                {
                    ComponentScanner.ScanComponents(repoPath);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        LogText.Text = "SS14 repo connected and components scanned.";
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to scan components: {ex.Message}");
                }
            });
        }

        private async void OpenFile_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Prototype File",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("YAML Files") { Patterns = new[] { "*.yml", "*.yaml" } } }
            });

            if (files.Count > 0)
            {
                _prototypes = YamlPrototypeLoader.Load(files[0].Path.LocalPath);
                RefreshPrototypeList();
            }
        }

        private async void ScanRepo_Click(object? sender, RoutedEventArgs e)
        {
            await PromptForRepoRoot();
        }

        private void NewPrototype_Click(object? sender, RoutedEventArgs e)
        {
            var prototype = new Prototype { Name = "New Prototype", ID = "new_id", Type = "item" };
            _prototypes.Add(prototype);
            RefreshPrototypeList();
            PrototypeList.SelectedItem = prototype;
        }

        private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

        private void RefreshPrototypeList()
        {
            PrototypeList.ItemsSource = null;
            // Create items with display text for Avalonia ListBox, including suffix
            var items = _prototypes.Select(p => new
            {
                Display = string.IsNullOrWhiteSpace(p.Name) ? p.ID : 
                         !string.IsNullOrEmpty(p.Suffix) ? $"{p.ID} - {p.Name} ({p.Suffix})" : $"{p.ID} - {p.Name}",
                Prototype = p
            }).ToList();
            PrototypeList.ItemsSource = items;
            
            // Set up item template programmatically since Avalonia doesn't have DisplayMemberPath
            if (PrototypeList.ItemTemplate == null)
            {
                PrototypeList.ItemTemplate = new FuncDataTemplate<object>((item, namescope) =>
                {
                    var textBlock = new TextBlock();
                    textBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Display"));
                    return textBlock;
                });
            }
        }

        private void PrototypeList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (PrototypeList.SelectedItem != null)
            {
                var selected = PrototypeList.SelectedItem.GetType().GetProperty("Prototype")?.GetValue(PrototypeList.SelectedItem) as Prototype;
                if (selected != null)
                {
                    _selectedPrototype = selected;
                    ShowPrototype(selected);
                }
            }
        }

        private void AddComponent_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedPrototype != null)
            {
                var newComponent = new Component { Name = "NewComponent" };
                _selectedPrototype.Components.Add(newComponent);
                ShowPrototype(_selectedPrototype);
                RefreshTextureList(_selectedPrototype);
            }
        }

        private void RemoveComponent_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedPrototype != null && _selectedPrototype.Components.Count > 0)
            {
                _selectedPrototype.Components.RemoveAt(_selectedPrototype.Components.Count - 1);
                ShowPrototype(_selectedPrototype);
                RefreshTextureList(_selectedPrototype);
            }
        }

        private void RefreshTextureList(Prototype prototype)
        {
            var textures = new List<string>();
            foreach (var comp in prototype.Components)
            {
                foreach (var field in comp.Fields)
                {
                    var fieldKey = field.Key.ToLower();
                    var fieldValue = field.Value?.ToString() ?? "";
                    
                    if (IsTextureField(fieldKey, fieldValue))
                    {
                        if (!string.IsNullOrEmpty(fieldValue))
                        {
                            textures.Add($"{field.Key}: {fieldValue}");
                        }
                    }
                }
            }
            TextureList.ItemsSource = textures.Count > 0 ? textures : new List<string> { "No textures found" };
        }

        private void RemoveComponentButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Component componentToRemove && _selectedPrototype != null)
            {
                _selectedPrototype.Components.Remove(componentToRemove);
                ShowPrototype(_selectedPrototype);
                RefreshTextureList(_selectedPrototype);
            }
        }

        private void LoadPrototypeSprite(Prototype prototype)
        {
            // Clear the current sprite and inheritance info
            PrototypeSprite.Source = null;
            PrototypeSprite.IsVisible = true;
            ClearInheritanceInfo();
            
            // Check for general inheritance (not just sprite)
            var inheritanceInfo = GetInheritanceInfo(prototype);
            
            // Try to find sprite in current prototype first
            if (TryLoadSpriteFromPrototype(prototype))
            {
                // Show inheritance info if anything is inherited
                if (inheritanceInfo.hasInheritance)
                {
                    UpdateInheritanceInfo(inheritanceInfo.parentPrototype!, inheritanceInfo.parentId!, inheritanceInfo.inheritedProperties);
                }
                return;
            }
            
            // If no sprite found in current prototype, check parent prototypes
            if (TryLoadSpriteFromParents(prototype))
            {
                return;
            }
            
            // If no sprite found anywhere, hide the image but still show inheritance if applicable
            PrototypeSprite.IsVisible = false;
            if (inheritanceInfo.hasInheritance)
            {
                UpdateInheritanceInfo(inheritanceInfo.parentPrototype!, inheritanceInfo.parentId!, inheritanceInfo.inheritedProperties);
            }
        }

        private (bool hasInheritance, Prototype? parentPrototype, string? parentId, List<string> inheritedProperties) GetInheritanceInfo(Prototype prototype)
        {
            var inheritedProps = new List<string>();
            
            if (prototype.Parent.Count == 0)
                return (false, null, null, inheritedProps);
            
            // Find the first parent prototype
            var parentId = prototype.Parent.First();
            var parentPrototype = _prototypes.FirstOrDefault(p => 
                p.ID.Equals(parentId, StringComparison.OrdinalIgnoreCase));
            
            if (parentPrototype == null)
                return (false, null, null, inheritedProps);
            
            // Check what properties are inherited (missing in child but present in parent)
            if (string.IsNullOrEmpty(prototype.Name) && !string.IsNullOrEmpty(parentPrototype.Name))
                inheritedProps.Add("name");
            
            if (string.IsNullOrEmpty(prototype.Description) && !string.IsNullOrEmpty(parentPrototype.Description))
                inheritedProps.Add("description");
            
            if (string.IsNullOrEmpty(prototype.Suffix) && !string.IsNullOrEmpty(parentPrototype.Suffix))
                inheritedProps.Add("suffix");
            
            // Check if sprite is inherited
            var childSpriteComp = prototype.Components.FirstOrDefault(c => 
                c.Name.Equals("Sprite", StringComparison.OrdinalIgnoreCase));
            var parentSpriteComp = parentPrototype.Components.FirstOrDefault(c => 
                c.Name.Equals("Sprite", StringComparison.OrdinalIgnoreCase));
            
            if (childSpriteComp != null && parentSpriteComp != null)
            {
                // Check if child has its own sprite path
                var childHasSpritePath = childSpriteComp.Fields.Any(f => 
                    (f.Key.Equals("sprite", StringComparison.OrdinalIgnoreCase) ||
                     f.Key.Equals("texture", StringComparison.OrdinalIgnoreCase) ||
                     f.Key.Equals("rsi", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrEmpty(f.Value?.ToString()));
                
                var parentHasSpritePath = parentSpriteComp.Fields.Any(f => 
                    (f.Key.Equals("sprite", StringComparison.OrdinalIgnoreCase) ||
                     f.Key.Equals("texture", StringComparison.OrdinalIgnoreCase) ||
                     f.Key.Equals("rsi", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrEmpty(f.Value?.ToString()));
                
                // Get sprite paths for comparison
                var childSpriteField = childSpriteComp.Fields.FirstOrDefault(f => 
                    f.Key.Equals("sprite", StringComparison.OrdinalIgnoreCase));
                var childSpritePath = childSpriteField.Key != null ? childSpriteField.Value?.ToString() : null;
                
                var parentSpriteField = parentSpriteComp.Fields.FirstOrDefault(f => 
                    f.Key.Equals("sprite", StringComparison.OrdinalIgnoreCase));
                var parentSpritePath = parentSpriteField.Key != null ? parentSpriteField.Value?.ToString() : null;
                
                // Get states for comparison
                var childStateField = childSpriteComp.Fields.FirstOrDefault(f => 
                    f.Key.Equals("state", StringComparison.OrdinalIgnoreCase));
                var childState = childStateField.Key != null ? childStateField.Value?.ToString() : null;
                
                var parentStateField = parentSpriteComp.Fields.FirstOrDefault(f => 
                    f.Key.Equals("state", StringComparison.OrdinalIgnoreCase));
                var parentState = parentStateField.Key != null ? parentStateField.Value?.ToString() : null;
                
                // Only consider sprite inherited if:
                // 1. Child has no sprite path AND parent has sprite path AND child uses same state as parent
                // 2. Child explicitly uses same sprite path as parent
                if (!childHasSpritePath && parentHasSpritePath)
                {
                    // If child has a different state than parent, it's likely using a different sprite
                    // Only mark as inherited if states are the same or child has no state (inherits parent's state)
                    if (string.IsNullOrEmpty(childState) || 
                        (!string.IsNullOrEmpty(parentState) && childState.Equals(parentState, StringComparison.OrdinalIgnoreCase)))
                    {
                        var childHasOnlyModifications = childSpriteComp.Fields.Any(f => 
                            f.Key.Equals("scale", StringComparison.OrdinalIgnoreCase) ||
                            f.Key.Equals("offset", StringComparison.OrdinalIgnoreCase) ||
                            f.Key.Equals("rotation", StringComparison.OrdinalIgnoreCase));
                        
                        if (childHasOnlyModifications)
                            inheritedProps.Add("sprite (path inherited, modified)");
                        else
                            inheritedProps.Add("sprite");
                    }
                    // If child has different state, it's not inherited - it's a new sprite using different state
                }
                else if (childHasSpritePath && parentHasSpritePath && 
                         !string.IsNullOrEmpty(childSpritePath) && !string.IsNullOrEmpty(parentSpritePath) &&
                         childSpritePath.Equals(parentSpritePath, StringComparison.OrdinalIgnoreCase))
                {
                    // Child explicitly uses same sprite path as parent
                    inheritedProps.Add("sprite (same path as parent)");
                }
            }
            else if (childSpriteComp == null && parentSpriteComp != null)
            {
                // Child has no Sprite component at all, so it would inherit everything
                inheritedProps.Add("sprite (entire component)");
            }
            
            return (inheritedProps.Count > 0, parentPrototype, parentId, inheritedProps);
        }

        private void UpdateInheritanceInfo(Prototype parentPrototype, string parentId, List<string> inheritedProperties)
        {
            // Update sprite tooltip with inheritance info
            var currentTooltip = ToolTip.GetTip(PrototypeSprite)?.ToString() ?? "";
            if (!string.IsNullOrEmpty(currentTooltip))
            {
                var inheritanceInfo = $"🔗 Inherited from: {parentId}";
                if (!string.IsNullOrEmpty(parentPrototype.Name))
                {
                    inheritanceInfo += $" ({parentPrototype.Name})";
                }
                if (inheritedProperties.Count > 0)
                {
                    inheritanceInfo += $"\nInherited: {string.Join(", ", inheritedProperties)}";
                }
                ToolTip.SetTip(PrototypeSprite, $"{currentTooltip}\n{inheritanceInfo}");
            }

            // Add inheritance indicator next to the sprite
            ShowInheritanceIndicator(parentPrototype, parentId, inheritedProperties);
        }

        private void ShowInheritanceIndicator(Prototype parentPrototype, string parentId, List<string> inheritedProperties)
        {
            // Find the horizontal StackPanel that contains the sprite and text info
            var headerContainer = this.GetLogicalDescendants()
                .OfType<StackPanel>()
                .FirstOrDefault(sp => sp.Orientation == Avalonia.Layout.Orientation.Horizontal &&
                                     sp.Children.OfType<Image>().Any(img => img.Name == "PrototypeSprite"));

            if (headerContainer != null)
            {
                // Check if inheritance indicator already exists
                var existingIndicator = headerContainer.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Name == "InheritanceIndicator");

                if (existingIndicator == null)
                {
                    // Create inheritance indicator positioned after the sprite
                    var indicator = new TextBlock
                    {
                        Name = "InheritanceIndicator",
                        Text = "🔗",
                        FontSize = 18,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        Margin = new Avalonia.Thickness(-8, 0, 8, 0) // Overlap slightly with sprite area
                    };

                    // Build tooltip text
                    var tooltipText = $"Inherits from: {parentId}";
                    if (!string.IsNullOrEmpty(parentPrototype.Name))
                    {
                        tooltipText += $"\nParent: {parentPrototype.Name}";
                    }
                    if (inheritedProperties.Count > 0)
                    {
                        tooltipText += $"\nInherited properties: {string.Join(", ", inheritedProperties)}";
                    }
                    if (!string.IsNullOrEmpty(parentPrototype.Description))
                    {
                        tooltipText += $"\nParent description: {parentPrototype.Description}";
                    }
                    
                    ToolTip.SetTip(indicator, tooltipText);

                    // Insert after the sprite (index 1, since sprite is at index 0)
                    headerContainer.Children.Insert(1, indicator);
                }
                else
                {
                    // Update existing indicator tooltip
                    var tooltipText = $"Inherits from: {parentId}";
                    if (!string.IsNullOrEmpty(parentPrototype.Name))
                    {
                        tooltipText += $"\nParent: {parentPrototype.Name}";
                    }
                    if (inheritedProperties.Count > 0)
                    {
                        tooltipText += $"\nInherited properties: {string.Join(", ", inheritedProperties)}";
                    }
                    if (!string.IsNullOrEmpty(parentPrototype.Description))
                    {
                        tooltipText += $"\nParent description: {parentPrototype.Description}";
                    }
                    
                    ToolTip.SetTip(existingIndicator, tooltipText);
                    existingIndicator.IsVisible = true;
                }
            }
        }

        private void ClearInheritanceInfo()
        {
            // Remove inheritance indicator from header container
            var headerContainer = this.GetLogicalDescendants()
                .OfType<StackPanel>()
                .FirstOrDefault(sp => sp.Orientation == Avalonia.Layout.Orientation.Horizontal &&
                                     sp.Children.OfType<Image>().Any(img => img.Name == "PrototypeSprite"));

            if (headerContainer != null)
            {
                var indicator = headerContainer.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Name == "InheritanceIndicator");
                
                if (indicator != null)
                {
                    headerContainer.Children.Remove(indicator);
                }
            }
        }

        private void LoadPrototypeDescription(Prototype prototype)
        {
            // Clear the current description
            PrototypeDescription.Text = "";
            PrototypeDescription.IsVisible = false;
            
            // First priority: Use the prototype's Description property (top-level field)
            if (!string.IsNullOrEmpty(prototype.Description))
            {
                PrototypeDescription.Text = prototype.Description;
                PrototypeDescription.IsVisible = true;
                return;
            }
            
            // Second priority: Look for description in MetaData component
            var metaDataComponent = prototype.Components.FirstOrDefault(c => 
                c.Name.Equals("MetaData", StringComparison.OrdinalIgnoreCase));
            
            if (metaDataComponent != null)
            {
                var descField = metaDataComponent.Fields.FirstOrDefault(f => 
                    f.Key.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                    f.Key.Equals("desc", StringComparison.OrdinalIgnoreCase));
                
                if (descField.Key != null && !string.IsNullOrEmpty(descField.Value?.ToString()))
                {
                    PrototypeDescription.Text = descField.Value.ToString()!;
                    PrototypeDescription.IsVisible = true;
                    return;
                }
            }
            
            // Third priority: Look for description in any component
            foreach (var comp in prototype.Components)
            {
                foreach (var field in comp.Fields)
                {
                    var fieldKey = field.Key.ToLower();
                    var fieldValue = field.Value?.ToString() ?? "";
                    
                    // Check if this field contains description information
                    if ((fieldKey.Contains("description") || fieldKey.Contains("desc")) && !string.IsNullOrEmpty(fieldValue))
                    {
                        PrototypeDescription.Text = fieldValue;
                        PrototypeDescription.IsVisible = true;
                        return;
                    }
                }
            }
        }

        private void ShowBasicProperties(Prototype prototype)
        {
            BasicPropertiesPanel.Children.Clear();
            
            // Get all basic fields from our field definitions
            var basicFields = EntityPrototypeFields.BasicFields;
            
            foreach (var fieldDef in basicFields)
            {
                CreatePropertyField(prototype, fieldDef);
            }
            
            // Add a separator for placement properties
            var placementSeparator = new TextBlock
            {
                Text = "Placement Properties",
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9)),
                Margin = new Thickness(0, 15, 0, 8)
            };
            BasicPropertiesPanel.Children.Add(placementSeparator);
            
            // Add placement fields
            var placementFields = EntityPrototypeFields.PlacementFields;
            foreach (var fieldDef in placementFields)
            {
                CreatePropertyField(prototype, fieldDef);
            }
        }

        private void CreatePropertyField(Prototype prototype, PrototypeFieldDefinition fieldDef)
        {
            var fieldStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // For optional fields, add a toggle checkbox first
            if (fieldDef.IsOptional)
            {
                var toggleCheckBox = new CheckBox
                {
                    IsChecked = prototype.IsOptionalFieldEnabled(fieldDef.Name) || 
                               !IsFieldEmptyOrDefault(prototype, fieldDef),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                SetTip(toggleCheckBox, $"Include {fieldDef.DisplayName} in YAML output");

                // Initialize the field as enabled if it has a non-default value
                if (toggleCheckBox.IsChecked == true)
                {
                    prototype.SetOptionalFieldEnabled(fieldDef.Name, true);
                }

                fieldStack.Children.Add(toggleCheckBox);
            }

            var fieldLabel = new TextBlock
            {
                Text = fieldDef.DisplayName,
                Width = fieldDef.IsOptional ? 100 : 120, // Slightly smaller if we have a toggle
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add tooltip with description if available
            if (!string.IsNullOrEmpty(fieldDef.Description))
            {
                ToolTip.SetTip(fieldLabel, fieldDef.Description);
            }

            Control inputControl;

            switch (fieldDef.Type)
            {
                case PrototypeFieldType.Boolean:
                    inputControl = CreateBooleanField(prototype, fieldDef);
                    break;
                case PrototypeFieldType.Integer:
                    inputControl = CreateIntegerField(prototype, fieldDef);
                    break;
                case PrototypeFieldType.StringArray:
                    inputControl = CreateStringArrayField(prototype, fieldDef);
                    break;
                case PrototypeFieldType.Enum:
                    inputControl = CreateEnumField(prototype, fieldDef);
                    break;
                default: // String and others
                    inputControl = CreateStringField(prototype, fieldDef);
                    break;
            }

            // Disable the input control if the field is optional and not enabled
            if (fieldDef.IsOptional)
            {
                var isEnabled = prototype.IsOptionalFieldEnabled(fieldDef.Name) || 
                               !IsFieldEmptyOrDefault(prototype, fieldDef);
                inputControl.IsEnabled = isEnabled;

                // Set up the toggle functionality
                var toggleCheckBox = (CheckBox)fieldStack.Children[0];
                toggleCheckBox.IsCheckedChanged += (s, e) =>
                {
                    var enabled = toggleCheckBox.IsChecked ?? false;
                    prototype.SetOptionalFieldEnabled(fieldDef.Name, enabled);
                    inputControl.IsEnabled = enabled;
                    
                    // If disabling, clear the visual field as well
                    if (!enabled)
                    {
                        ClearInputControl(inputControl, fieldDef);
                    }
                };
            }

            fieldStack.Children.Add(fieldLabel);
            fieldStack.Children.Add(inputControl);
            BasicPropertiesPanel.Children.Add(fieldStack);
        }

        private bool IsFieldEmptyOrDefault(Prototype prototype, PrototypeFieldDefinition fieldDef)
        {
            var value = prototype.GetPropertyValue(fieldDef.Name);
            
            return fieldDef.Type switch
            {
                PrototypeFieldType.String => string.IsNullOrEmpty(value?.ToString()),
                PrototypeFieldType.Boolean => value == null || (fieldDef.DefaultValue != null && 
                                                               value.Equals(fieldDef.DefaultValue)),
                PrototypeFieldType.Integer => value == null || (fieldDef.DefaultValue != null && 
                                                               value.Equals(fieldDef.DefaultValue)),
                PrototypeFieldType.StringArray => value == null || 
                                                  (value is List<string> list && list.Count == 0) ||
                                                  (value is HashSet<string> set && set.Count == 0),
                PrototypeFieldType.Enum => string.IsNullOrEmpty(value?.ToString()),
                _ => value == null
            };
        }

        private void ClearInputControl(Control inputControl, PrototypeFieldDefinition fieldDef)
        {
            switch (inputControl)
            {
                case TextBox textBox:
                    textBox.Text = "";
                    break;
                case CheckBox checkBox:
                    checkBox.IsChecked = fieldDef.DefaultValue is bool def ? def : false;
                    break;
                case NumericUpDown numericUpDown:
                    numericUpDown.Value = fieldDef.DefaultValue is int defInt ? defInt : 0;
                    break;
                case ComboBox comboBox:
                    comboBox.SelectedItem = fieldDef.DefaultValue?.ToString() ?? "";
                    break;
            }
        }

        private async void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedPrototype == null)
            {
                await ShowMessageBox("No Selection", "No prototype selected!");
                return;
            }

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .Build();

            var prototypeDict = new Dictionary<string, object>();

            // Always include required fields
            prototypeDict["type"] = _selectedPrototype.Type;
            prototypeDict["id"] = _selectedPrototype.ID;

            // Only include optional fields if they are enabled and have non-default values
            var allFields = EntityPrototypeFields.GetAllFields();
            
            foreach (var fieldDef in allFields)
            {
                // Skip required fields as they're already added
                if (!fieldDef.IsOptional || fieldDef.Name == "type" || fieldDef.Name == "id")
                    continue;

                // Only include if the field is enabled and has a meaningful value
                if (_selectedPrototype.IsOptionalFieldEnabled(fieldDef.Name))
                {
                    var value = _selectedPrototype.GetPropertyValue(fieldDef.Name);
                    
                    // Check if the value is meaningful (not null/empty/default)
                    if (IsValueMeaningful(value, fieldDef))
                    {
                        var yamlFieldName = GetYamlFieldName(fieldDef.Name);
                        
                        // Handle special formatting for certain field types
                        if (fieldDef.Name == "parent" && value is List<string> parentList && parentList.Count > 0)
                        {
                            prototypeDict[yamlFieldName] = parentList.Count == 1 ? parentList[0] : parentList;
                        }
                        else if (fieldDef.Name == "categories" && value is HashSet<string> catSet && catSet.Count > 0)
                        {
                            prototypeDict[yamlFieldName] = catSet.ToList();
                        }
                        else if (fieldDef.Name.StartsWith("placement."))
                        {
                            // Handle placement properties as nested object
                            if (!prototypeDict.ContainsKey("placement"))
                            {
                                prototypeDict["placement"] = new Dictionary<string, object>();
                            }
                            var placementDict = (Dictionary<string, object>)prototypeDict["placement"];
                            var placementKey = fieldDef.Name.Substring("placement.".Length);
                            placementDict[placementKey] = value!;
                        }
                        else if (value != null)
                        {
                            prototypeDict[yamlFieldName] = value;
                        }
                    }
                }
            }

            // Add components
            if (_selectedPrototype.Components.Count > 0)
            {
                prototypeDict["components"] = _selectedPrototype.Components.Select(c =>
                {
                    var compDict = new Dictionary<string, object> { { "type", c.Name } };
                    foreach (var field in c.Fields)
                    {
                        if (!string.IsNullOrEmpty(field.Value?.ToString()))
                        {
                            compDict[field.Key] = field.Value;
                        }
                    }
                    return compDict;
                }).ToList();
            }

            var yaml = serializer.Serialize(prototypeDict);

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Prototype File",
                FileTypeChoices = new[] { new FilePickerFileType("YAML Files") { Patterns = new[] { "*.yml", "*.yaml" } } }
            });

            if (file != null)
            {
                await File.WriteAllTextAsync(file.Path.LocalPath, yaml, Encoding.UTF8);
                await ShowMessageBox("Success", "Prototype saved!");
            }
        }

        private bool IsValueMeaningful(object? value, PrototypeFieldDefinition fieldDef)
        {
            return fieldDef.Type switch
            {
                PrototypeFieldType.String => !string.IsNullOrEmpty(value?.ToString()),
                PrototypeFieldType.Boolean => value != null && (fieldDef.DefaultValue == null || !value.Equals(fieldDef.DefaultValue)),
                PrototypeFieldType.Integer => value != null && (fieldDef.DefaultValue == null || !value.Equals(fieldDef.DefaultValue)),
                PrototypeFieldType.StringArray => value != null && 
                                                  ((value is List<string> list && list.Count > 0) ||
                                                   (value is HashSet<string> set && set.Count > 0)),
                PrototypeFieldType.Enum => !string.IsNullOrEmpty(value?.ToString()) && 
                                          (fieldDef.DefaultValue == null || !value.ToString()!.Equals(fieldDef.DefaultValue?.ToString())),
                _ => value != null
            };
        }

        private string GetYamlFieldName(string fieldName)
        {
            return fieldName.ToLower() switch
            {
                "localizationid" => "localizationId",
                "save" => "save",
                _ => fieldName
            };
        }

        private async void ValidateButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedPrototype == null)
            {
                await ShowMessageBox("No Selection", "No prototype selected!");
                return;
            }

            // TODO: Implement prototype validation logic
            await ShowMessageBox("Validation", "Prototype validation feature not yet implemented.");
        }

        // Stub for ShowPrototype
        private void ShowPrototype(Prototype prototype)
        {
            // Update prototype header information
            PrototypeTitle.Text = string.IsNullOrEmpty(prototype.Name) ? prototype.ID : prototype.Name;
            PrototypeId.Text = $"ID: {prototype.ID} | Type: {prototype.Type}";
            
            // Load description
            LoadPrototypeDescription(prototype);
            
            // Load sprite if available
            LoadPrototypeSprite(prototype);
            
            // Show basic properties
            ShowBasicProperties(prototype);
            
            // Show components
            ShowComponents(prototype);
            
            // Refresh texture list
            RefreshTextureList(prototype);
        }

        private void ShowComponents(Prototype prototype)
        {
            // Clear existing components
            ComponentPanel.ItemsSource = null;
            
            if (prototype.Components.Count == 0)
            {
                return;
            }
            
            // Simply set the components as the data source
            // The AXAML template should handle the display
            ComponentPanel.ItemsSource = prototype.Components;
        }

        // TODO: i forgot what this is for lol
        private bool IsTextureField(string fieldKey, string fieldValue)
        {
            return false;
        }
        private bool TryLoadSpriteFromPrototype(Prototype prototype)
        {
            // Look for Sprite component
            var spriteComponent = prototype.Components.FirstOrDefault(c => 
                c.Name.Equals("Sprite", StringComparison.OrdinalIgnoreCase));
            
            if (spriteComponent == null)
                return false;
            
            // Look for sprite path fields, is this neccessary? idk
            var spriteField = spriteComponent.Fields.FirstOrDefault(f => 
                f.Key.Equals("sprite", StringComparison.OrdinalIgnoreCase) ||
                f.Key.Equals("texture", StringComparison.OrdinalIgnoreCase) ||
                f.Key.Equals("rsi", StringComparison.OrdinalIgnoreCase));
            
            if (spriteField.Key == null || string.IsNullOrEmpty(spriteField.Value?.ToString()))
                return false;
            
            var spritePath = spriteField.Value.ToString();
            
            // Try to load the sprite from the SS14 repo
            if (!string.IsNullOrEmpty(_config.SS14RepoRoot))
            {
                try
                {
                    var fullPath = Path.Combine(_config.SS14RepoRoot, "Resources", spritePath);
                    
                    // Check common image extensions, I think ss14 only uses png but I'm just gonna do a handful
                    var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" };
                    foreach (var ext in extensions)
                    {
                        var imageFile = fullPath + ext;
                        if (File.Exists(imageFile))
                        {
                            var bitmap = new Bitmap(imageFile);
                            PrototypeSprite.Source = bitmap;
                            SetTip(PrototypeSprite, $"Sprite: {spritePath}{ext}");
                            return true;
                        }
                    }
                    
                    // just like check for the full path
                    if (File.Exists(fullPath))
                    {
                        var bitmap = new Bitmap(fullPath);
                        PrototypeSprite.Source = bitmap;
                        SetTip(PrototypeSprite, $"Sprite: {spritePath}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load sprite {spritePath}: {ex.Message}");
                }
            }
            
            // If we can't load the actual sprite, show a placeholder with the path info
            SetTip(PrototypeSprite, $"Sprite path: {spritePath}\n(Image not found or repo not connected)");
            return false;
        }

        // gets info from the parent entity and uses it if the child field is blank
        private bool TryLoadSpriteFromParents(Prototype prototype)
        {
            if (prototype.Parent.Count == 0)
                return false;
            
            // Try to load sprite from each parent
            foreach (var parentId in prototype.Parent)
            {
                var parentPrototype = _prototypes.FirstOrDefault(p => 
                    p.ID.Equals(parentId, StringComparison.OrdinalIgnoreCase));
                
                if (parentPrototype != null && TryLoadSpriteFromPrototype(parentPrototype))
                {
                    // Add inheritance info to tooltip
                    var currentTooltip = GetTip(PrototypeSprite)?.ToString() ?? "";
                    SetTip(PrototypeSprite, $"{currentTooltip}\n🔗 Inherited from: {parentId}");
                    return true;
                }
            }
            
            return false;
        }

        // this fucking sucks tbh
        private Control CreateBooleanField(Prototype prototype, PrototypeFieldDefinition fieldDef)
        {
            var checkBox = new CheckBox();
            var currentValue = prototype.GetPropertyValue(fieldDef.Name);
            if (currentValue is bool boolValue)
            {
                checkBox.IsChecked = boolValue;
            }
            
            checkBox.IsCheckedChanged += (s, e) =>
            {
                prototype.SetPropertyValue(fieldDef.Name, checkBox.IsChecked ?? false);
            };
            
            return checkBox;
        }

        // i lobe integer
        private Control CreateIntegerField(Prototype prototype, PrototypeFieldDefinition fieldDef)
        {
            var textBox = new TextBox { Width = 200 };
            var currentValue = prototype.GetPropertyValue(fieldDef.Name);
            if (currentValue != null)
            {
                textBox.Text = currentValue.ToString();
            }
            
            textBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(textBox.Text, out int value))
                {
                    prototype.SetPropertyValue(fieldDef.Name, value);
                }
            };
            
            return textBox;
        }

        // i lobe string arrays
        private Control CreateStringArrayField(Prototype prototype, PrototypeFieldDefinition fieldDef)
        {
            var textBox = new TextBox { Width = 200 };
            var currentValue = prototype.GetPropertyValue(fieldDef.Name);
            if (currentValue is List<string> list)
            {
                textBox.Text = string.Join(", ", list);
            }
            
            textBox.TextChanged += (s, e) =>
            {
                var items = textBox.Text.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                prototype.SetPropertyValue(fieldDef.Name, items);
            };
            
            return textBox;
        }

        // theres a lot of these, this is for enums
        private Control CreateEnumField(Prototype prototype, PrototypeFieldDefinition fieldDef)
        {
            var comboBox = new ComboBox { Width = 200 };
            if (fieldDef.EnumValues != null)
            {
                comboBox.ItemsSource = fieldDef.EnumValues;
            }
            
            var currentValue = prototype.GetPropertyValue(fieldDef.Name);
            if (currentValue != null)
            {
                comboBox.SelectedItem = currentValue.ToString();
            }
            
            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    prototype.SetPropertyValue(fieldDef.Name, comboBox.SelectedItem.ToString());
                }
            };
            
            return comboBox;
        }
        
        private Control CreateStringField(Prototype prototype, PrototypeFieldDefinition fieldDef)
        {
            var textBox = new TextBox { Width = 200 };
            var currentValue = prototype.GetPropertyValue(fieldDef.Name);
            if (currentValue != null)
            {
                textBox.Text = currentValue.ToString();
            }
            
            textBox.TextChanged += (s, e) =>
            {
                prototype.SetPropertyValue(fieldDef.Name, textBox.Text);
            };
            
            return textBox;
        }
    }
}
