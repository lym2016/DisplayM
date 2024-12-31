using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using Visualizer.Core.Models;
using System.Collections.ObjectModel;
using Visualizer.Core.Defaults.Visualizer.Core.Services;
using System.Collections.Concurrent;
using System.Globalization;
using System.Collections;
using System.Text.Json.Serialization;
using System.Text.Json;
using Prism.Mvvm;
using System.IO;
using Visualizer.Core.Defaults;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Schema;
using Microsoft.Win32;

namespace DisplayM
{
    // ListViewTemplate.xaml.cs
    
}
namespace Visualizer.ViewModels
{
    // 1. 事件定义
    public class ConfigurationChangedEvent : PubSubEvent<VisualConfig>
    {
    }
    // 3. 事件定义
    public class ItemSelectedEvent : PubSubEvent<object> { }
    public class TemplateChangedEvent : PubSubEvent<object> { }
    // 2. PropertyTreeNode 定义
    public class PropertyTreeNode : BindableBase
    {
        private string _name;
        private string _displayName;
        private PropertyVisualConfig _propertyConfig;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public PropertyVisualConfig PropertyConfig
        {
            get => _propertyConfig;
            set => SetProperty(ref _propertyConfig, value);
        }

        public ObservableCollection<PropertyTreeNode> Children { get; } = new();
    }
    public interface ICustomDialogService
    {
        Task ShowMessageAsync(string title, string message);
        Task ShowErrorAsync(string title, string message);
        SaveFileDialog ShowSaveFileDialog(string filter = null, string defaultExt = null);
        OpenFileDialog ShowOpenFileDialog(string filter = null, string defaultExt = null);
        Task<bool> ShowConfirmAsync(string title, string message);
    }

    public class CustomDialogService : ICustomDialogService
    {
        private readonly IDialogService _prismDialogService;

        public CustomDialogService(IDialogService prismDialogService)
        {
            _prismDialogService = prismDialogService;
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            var parameters = new DialogParameters
        {
            { "Title", title },
            { "Message", message }
        };
              _prismDialogService.ShowDialog("MessageDialog", parameters);
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var parameters = new DialogParameters
        {
            { "Title", title },
            { "Message", message },
            { "IsError", true }
        };
              _prismDialogService.ShowDialog("MessageDialog", parameters);
        }

        public SaveFileDialog ShowSaveFileDialog(string filter = null, string defaultExt = null)
        {
            return new SaveFileDialog
            {
                Filter = filter ?? "所有文件 (*.*)|*.*",
                DefaultExt = defaultExt,
                AddExtension = true
            };
        }

        public OpenFileDialog ShowOpenFileDialog(string filter = null, string defaultExt = null)
        {
            return new OpenFileDialog
            {
                Filter = filter ?? "所有文件 (*.*)|*.*",
                DefaultExt = defaultExt
            };
        }

        public async Task<bool>  ShowConfirmAsync(string title, string message)
        {
            var parameters = new DialogParameters
        {
            { "Title", title },
            { "Message", message }
        };
             _prismDialogService.ShowDialog("ConfirmDialog", parameters);
            return true;
        }
    }
    public class ConfigEditorViewModel : BindableBase
    {
        private readonly IConfigurationService _configService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IConfigurationExportImport _configExportImport;
        private readonly ICustomDialogService _dialogService;
        private VisualConfig _currentConfig;
        private PropertyVisualConfig _selectedProperty;
        private bool _isDirty;

        public ConfigEditorViewModel(
            IConfigurationService configService,
           IEventAggregator eventAggregator,
           IConfigurationExportImport configExportImport,
           ICustomDialogService dialogService)
        {
            _configService = configService;
            _eventAggregator = eventAggregator;
            _configExportImport = configExportImport;
            _dialogService = dialogService;

            SaveCommand = new DelegateCommand(ExecuteSave, CanSave);
            ExportCommand = new DelegateCommand(ExecuteExport);
            ImportCommand = new DelegateCommand(ExecuteImport);
            ResetCommand = new DelegateCommand(ExecuteReset);
        }

        public ObservableCollection<PropertyNode> PropertyNodes { get; } = new();

        public PropertyVisualConfig SelectedProperty
        {
            get => _selectedProperty;
            set => SetProperty(ref _selectedProperty, value);
        }

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (SetProperty(ref _isDirty, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand ImportCommand { get; }
        public DelegateCommand ResetCommand { get; }

        public async Task InitializeAsync(Type modelType)
        {
            _currentConfig = await _configService.GetOrCreateConfigAsync(modelType);
            BuildPropertyTree();
        }

        private void BuildPropertyTree()
        {
            PropertyNodes.Clear();
            var properties = _currentConfig.Properties
                .OrderBy(p => p.Value.PropertyPath);

            foreach (var prop in properties)
            {
                AddPropertyNode(prop.Value);
            }
        }

        private void AddPropertyNode(PropertyVisualConfig propConfig)
        {
            var pathParts = propConfig.PropertyPath.Split('.');
            var currentCollection = PropertyNodes;
            PropertyNode currentNode = null;

            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                var isLast = i == pathParts.Length - 1;

                var node = currentCollection.FirstOrDefault(n => n.Name == part);
                if (node == null)
                {
                    node = new PropertyNode
                    {
                        Name = part,
                        FullPath = string.Join(".", pathParts.Take(i + 1)),
                        IsProperty = isLast,
                        PropertyConfig = isLast ? propConfig : null
                    };
                    currentCollection.Add(node);
                }

                if (!isLast)
                {
                    currentCollection = node.Children;
                }
                currentNode = node;
            }
        }

        private async void ExecuteSave()
        {
            try
            {
                await _configService.SaveConfigAsync(_currentConfig);
                _eventAggregator.GetEvent<ConfigurationChangedEvent>()
                    .Publish(_currentConfig);
                IsDirty = false;
                await _dialogService.ShowMessageAsync("保存成功", "配置已成功保存。");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("保存失败", ex.Message);
            }
        }
        
        private async void ExecuteExport()
        {
            try
            {
                var dialog = _dialogService.ShowSaveFileDialog(
                    filter: "JSON文件 (*.json)|*.json",
                    defaultExt: ".json");

                if (dialog.ShowDialog() == true)
                {
                    await _configExportImport.ExportAsync(_currentConfig, dialog.FileName);
                    await _dialogService.ShowMessageAsync("导出成功", "配置已成功导出。");
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("导出失败", ex.Message);
            }
        }

        private async void ExecuteImport()
        {
            try
            {
                var dialog = _dialogService.ShowOpenFileDialog(
                    filter: "JSON文件 (*.json)|*.json",
                    defaultExt: ".json");

                if (dialog.ShowDialog() == true)
                {
                    var importedConfig = await _configExportImport.ImportAsync(dialog.FileName);

                    // 确认是否替换当前配置
                    var result = await _dialogService.ShowConfirmAsync(
                        "确认导入",
                        "是否要替换当前配置？此操作无法撤销。");

                    if (result)
                    {
                        _currentConfig = importedConfig;
                        await InitializeAsync(_currentConfig);
                        IsDirty = true;
                    }
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("导入失败", ex.Message);
            }
        }

        private async void ExecuteReset()
        {
            try
            {
                var result = await _dialogService.ShowConfirmAsync(
                    "确认重置",
                    "是否要重置为默认配置？此操作无法撤销。");

                if (result)
                {
                    var type = Type.GetType($"{_currentConfig.TypeFullName}, {_currentConfig.AssemblyName}");
                    if (type != null)
                    {
                        _currentConfig = ConfigurationCache.CreateDefaultConfig(type);
                        await InitializeAsync(_currentConfig);
                        IsDirty = true;
                    }
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("重置失败", ex.Message);
            }
        }

        private bool CanSave() => IsDirty;
        public async Task InitializeAsync(VisualConfig config)
        {
            _currentConfig = config;
             BuildPropertyTree();
            await UpdateDisplayModes();
            IsDirty = false;
        }

        private async Task UpdateDisplayModes()
        {
            DisplayModes.Clear();
            foreach (var mode in _currentConfig.SupportedModes)
            {
                var modeViewModel = new DisplayModeViewModel
                {
                    Mode = mode,
                    Settings = _currentConfig.GetModeSettings(mode)
                };
                DisplayModes.Add(modeViewModel);
            }
        }

        public ObservableCollection<DisplayModeViewModel> DisplayModes { get; }
            = new ObservableCollection<DisplayModeViewModel>();
    }
    // 1. DisplayMode 相关
    public class DisplayModeViewModel : BindableBase
    {
        private DisplayMode _mode;
        private DisplaySettings _settings;
        private bool _isSelected;

        public DisplayMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public DisplaySettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
    public class PropertyNode : BindableBase
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsProperty { get; set; }
        public PropertyVisualConfig PropertyConfig { get; set; }
        public ObservableCollection<PropertyNode> Children { get; } = new();
    }
    // 2. 配置编辑器视图模型
    public class ConfigurationEditorViewModel : BindableBase
    {
        private readonly IConfigurationService _configService;
        private readonly IDialogService _dialogService;
        private VisualConfig _config;
        private PropertyTreeNode _selectedNode;

        public ConfigurationEditorViewModel(
            IConfigurationService configService,
            IDialogService dialogService)
        {
            _configService = configService;
            _dialogService = dialogService; 
        }

        public ObservableCollection<PropertyTreeNode> PropertyTree { get; private set; }

        public PropertyTreeNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    UpdateTypeSpecificEditor();
                }
            }
        }

        public object TypeSpecificEditor { get; private set; }

        public async Task InitializeAsync(VisualConfig config)
        {
            _config = config;
            PropertyTree = BuildPropertyTree(config);
            await UpdateDisplayModes();
        }
        private async Task UpdateDisplayModes()
        {
            DisplayModes.Clear();
            foreach (var mode in _config.SupportedModes)
            {
                var modeViewModel = new DisplayModeViewModel
                {
                    Mode = mode,
                    Settings = _config.GetModeSettings(mode)
                };
                DisplayModes.Add(modeViewModel);
            }
        }

        public ObservableCollection<DisplayModeViewModel> DisplayModes { get; }
            = new ObservableCollection<DisplayModeViewModel>();
        private ObservableCollection<PropertyTreeNode> BuildPropertyTree(VisualConfig config)
        {
            var tree = new ObservableCollection<PropertyTreeNode>();

            foreach (var prop in config.Properties)
            {
                var pathParts = prop.Value.PropertyPath.Split('.');
                var currentLevel = tree;
                PropertyTreeNode currentNode = null;

                foreach (var part in pathParts)
                {
                    currentNode = currentLevel.FirstOrDefault(n => n.Name == part);

                    if (currentNode == null)
                    {
                        currentNode = new PropertyTreeNode
                        {
                            Name = part,
                            DisplayName = part,
                            PropertyConfig = pathParts.Last() == part ? prop.Value : null
                        };
                        currentLevel.Add(currentNode);
                    }

                    currentLevel = currentNode.Children;
                }
            }

            return tree;
        }

        private void UpdateTypeSpecificEditor()
        {
            if (_selectedNode?.PropertyConfig == null)
            {
                TypeSpecificEditor = null;
                return;
            }

            TypeSpecificEditor = _selectedNode.PropertyConfig.TypeConfig.DisplayType switch
            {
                DisplayType.Number => new NumberTypeEditor(_selectedNode.PropertyConfig),
                DisplayType.DateTime => new DateTimeTypeEditor(_selectedNode.PropertyConfig),
                DisplayType.String => new StringTypeEditor(_selectedNode.PropertyConfig),
                DisplayType.Enum => new EnumTypeEditor(_selectedNode.PropertyConfig),
                DisplayType.Collection => new CollectionTypeEditor(_selectedNode.PropertyConfig),
                _ => null
            };

            RaisePropertyChanged(nameof(TypeSpecificEditor));
        }
    }

    // 3. 类型特定编辑器基类
    public abstract class TypeEditorBase : UserControl
    {
        protected PropertyVisualConfig PropertyConfig { get; }

        protected TypeEditorBase(PropertyVisualConfig config)
        {
            PropertyConfig = config;
        }
    }

  

    public class NumberTypeEditorViewModel : BindableBase
    {
        private readonly PropertyVisualConfig _config;

        public NumberTypeEditorViewModel(PropertyVisualConfig config)
        {
            _config = config;

            DecimalPlaces = (int)config.TypeConfig.Settings["DecimalPlaces"];
            UseThousandsSeparator = (bool)config.TypeConfig.Settings["UseThousandsSeparator"];
            Unit = (string)config.TypeConfig.Settings["Unit"];
        }

        public int DecimalPlaces
        {
            get => (int)_config.TypeConfig.Settings["DecimalPlaces"];
            set
            {
                _config.TypeConfig.Settings["DecimalPlaces"] = value;
                RaisePropertyChanged();
            }
        }

        public bool UseThousandsSeparator
        {
            get => (bool)_config.TypeConfig.Settings["UseThousandsSeparator"];
            set
            {
                _config.TypeConfig.Settings["UseThousandsSeparator"] = value;
                RaisePropertyChanged();
            }
        }

        public string Unit
        {
            get => (string)_config.TypeConfig.Settings["Unit"];
            set
            {
                _config.TypeConfig.Settings["Unit"] = value;
                RaisePropertyChanged();
            }
        }
    }
    // 1. 日期时间类型编辑器
    public class DateTimeTypeEditor : TypeEditorBase
    {
        public DateTimeTypeEditor(PropertyVisualConfig config) : base(config)
        {
            InitializeComponent();
            DataContext = new DateTimeTypeEditorViewModel(config);
        }
    }
    // 3. 类型编辑器
    public class NumberTypeEditor : TypeEditorBase
    {
        public NumberTypeEditor(PropertyVisualConfig config) : base(config)
        {
            DataContext = new NumberTypeEditorViewModel(config);
        }
    }

    public class EnumTypeEditor : TypeEditorBase
    {
        public EnumTypeEditor(PropertyVisualConfig config) : base(config)
        {
            DataContext = new EnumTypeEditorViewModel(config);
        }
    }
    // 1. EnumTypeEditorViewModel
    public class EnumTypeEditorViewModel : BindableBase
    {
        private readonly PropertyVisualConfig _config;
        private readonly Type _enumType;

        public EnumTypeEditorViewModel(PropertyVisualConfig config)
        {
            _config = config;
            _enumType = Type.GetType(_config.TypeConfig.Settings["EnumType"] as string);
            LoadEnumValues();
        }

        public ObservableCollection<EnumValueItem> EnumValues { get; } = new();

        public bool UseDescription
        {
            get => (bool)_config.TypeConfig.Settings["UseDescription"];
            set
            {
                _config.TypeConfig.Settings["UseDescription"] = value;
                RaisePropertyChanged();
                LoadEnumValues();
            }
        }

        public bool ShowAsComboBox
        {
            get => (bool)_config.TypeConfig.Settings["ShowAsComboBox"];
            set
            {
                _config.TypeConfig.Settings["ShowAsComboBox"] = value;
                RaisePropertyChanged();
            }
        }

        private void LoadEnumValues()
        {
            EnumValues.Clear();
            if (_enumType == null) return;

            var values = Enum.GetValues(_enumType);
            foreach (var value in values)
            {
                var name = value.ToString();
                var description = GetEnumDescription(_enumType, name);

                EnumValues.Add(new EnumValueItem
                {
                    Name = name,
                    Description = description ?? name,
                    Value = value
                });
            }
        }

        private string GetEnumDescription(Type enumType, string valueName)
        {
            if (!UseDescription) return valueName;

            var field = enumType.GetField(valueName);
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? valueName;
        }
    }

    public class EnumValueItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object Value { get; set; }
    }

    // 2. CollectionTypeEditorViewModel
    public class CollectionTypeEditorViewModel : BindableBase
    {
        private readonly PropertyVisualConfig _config;

        public CollectionTypeEditorViewModel(PropertyVisualConfig config)
        {
            _config = config;
        }

        public int MaxDisplayItems
        {
            get => (int)_config.TypeConfig.Settings["MaxDisplayItems"];
            set
            {
                _config.TypeConfig.Settings["MaxDisplayItems"] = value;
                RaisePropertyChanged();
            }
        }

        public string Separator
        {
            get => (string)_config.TypeConfig.Settings["Separator"];
            set
            {
                _config.TypeConfig.Settings["Separator"] = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowCount
        {
            get => (bool)_config.TypeConfig.Settings["ShowCount"];
            set
            {
                _config.TypeConfig.Settings["ShowCount"] = value;
                RaisePropertyChanged();
            }
        }

        public bool AllowExpand
        {
            get => (bool)_config.TypeConfig.Settings["AllowExpand"];
            set
            {
                _config.TypeConfig.Settings["AllowExpand"] = value;
                RaisePropertyChanged();
            }
        }

        public DataTemplate ItemTemplate
        {
            get => _config.TypeConfig.Settings["ItemTemplate"] as DataTemplate;
            set
            {
                _config.TypeConfig.Settings["ItemTemplate"] = value;
                RaisePropertyChanged();
            }
        }
    }
    public class CollectionTypeEditor : TypeEditorBase
    {
        public CollectionTypeEditor(PropertyVisualConfig config) : base(config)
        {
            DataContext = new CollectionTypeEditorViewModel(config);
        }
    }
    public class DateTimeTypeEditorViewModel : BindableBase
    {
        private readonly PropertyVisualConfig _config;
        private string _selectedPresetFormat;

        public DateTimeTypeEditorViewModel(PropertyVisualConfig config)
        {
            _config = config;
            LoadPresetFormats();
        }
        public string SelectedPresetFormat
        {
            get => _selectedPresetFormat;
            set
            {
                if (SetProperty(ref _selectedPresetFormat, value))
                {
                    CustomFormat = value;
                }
            }
        }
        private void LoadPresetFormats()
        {
            PresetFormats.Clear();
            PresetFormats.Add("yyyy-MM-dd");
            PresetFormats.Add("yyyy-MM-dd HH:mm");
            PresetFormats.Add("yyyy-MM-dd HH:mm:ss");
            PresetFormats.Add("MM/dd/yyyy");
            PresetFormats.Add("HH:mm:ss");
            PresetFormats.Add("yyyy年MM月dd日");

            // 设置默认选中的格式
            SelectedPresetFormat = CustomFormat;
        }
        public ObservableCollection<string> PresetFormats { get; } = new()
    {
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "MM/dd/yyyy",
        "HH:mm:ss",
        "yyyy年MM月dd日"
    };

        public string CustomFormat
        {
            get => (string)_config.TypeConfig.Settings["Format"];
            set
            {
                _config.TypeConfig.Settings["Format"] = value;
                RaisePropertyChanged();
                UpdateSampleOutput();
            }
        }

        public bool ShowTime
        {
            get => (bool)_config.TypeConfig.Settings["ShowTime"];
            set
            {
                _config.TypeConfig.Settings["ShowTime"] = value;
                RaisePropertyChanged();
                UpdatePresetFormat();
            }
        }
        private void UpdatePresetFormat()
        {
            if (ShowTime)
            {
                if (!CustomFormat.Contains("HH:mm"))
                {
                    CustomFormat = "yyyy-MM-dd HH:mm:ss";
                }
            }
            else
            {
                CustomFormat = "yyyy-MM-dd";
            }
        }
        public bool UseLocalTime
        {
            get => (bool)_config.TypeConfig.Settings["UseLocalTime"];
            set
            {
                _config.TypeConfig.Settings["UseLocalTime"] = value;
                RaisePropertyChanged();
                UpdateSampleOutput();
            }
        }

        public string SampleOutput { get; private set; }

        private void UpdateSampleOutput()
        {
            var sampleDate = DateTime.Now;
            if (!UseLocalTime)
                sampleDate = sampleDate.ToUniversalTime();

            try
            {
                SampleOutput = sampleDate.ToString(CustomFormat);
            }
            catch
            {
                SampleOutput = "格式无效";
            }
            RaisePropertyChanged(nameof(SampleOutput));
        }
    }

    // 2. 字符串类型编辑器
    public class StringTypeEditor : TypeEditorBase
    {
        public StringTypeEditor(PropertyVisualConfig config) : base(config)
        {
            InitializeComponent();
            DataContext = new StringTypeEditorViewModel(config);
        }
    }

    public class StringTypeEditorViewModel : BindableBase
    {
        private readonly PropertyVisualConfig _config;

        public StringTypeEditorViewModel(PropertyVisualConfig config)
        {
            _config = config;
        }

        public int? MaxLength
        {
            get => (int?)_config.TypeConfig.Settings["MaxLength"];
            set
            {
                _config.TypeConfig.Settings["MaxLength"] = value;
                RaisePropertyChanged();
            }
        }

        public string TruncationSuffix
        {
            get => (string)_config.TypeConfig.Settings["TruncationSuffix"];
            set
            {
                _config.TypeConfig.Settings["TruncationSuffix"] = value;
                RaisePropertyChanged();
            }
        }

        public TextWrapping TextWrapping
        {
            get => (TextWrapping)_config.TypeConfig.Settings["TextWrapping"];
            set
            {
                _config.TypeConfig.Settings["TextWrapping"] = value;
                RaisePropertyChanged();
            }
        }
    }

    // 3. 值格式化器实现
    public interface IValueFormatter
    {
        object Format(object value, TypeDisplayConfig config);
        object Parse(string text, TypeDisplayConfig config);
    }

    public class ValueFormatterFactory
    {
        private readonly Dictionary<DisplayType, IValueFormatter> _formatters;

        public ValueFormatterFactory()
        {
            _formatters = new Dictionary<DisplayType, IValueFormatter>
        {
            { DisplayType.Number, new NumberFormatter() },
            { DisplayType.DateTime, new DateTimeFormatter() },
            { DisplayType.Text, new StringFormatter() },
            { DisplayType.Boolean, new BooleanFormatter() },
            { DisplayType.Enum, new EnumFormatter() }
        };
        }

        public IValueFormatter GetFormatter(DisplayType type)
        {
            return _formatters.TryGetValue(type, out var formatter)
                ? formatter
                : new DefaultFormatter();
        }
    }
    // 4. 值格式化器实现
    public class StringFormatter : IValueFormatter
    {
        public object Format(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var text = value.ToString();
            var maxLength = config.Settings.GetValueOrDefault("MaxLength") as int?;
            var truncationSuffix = config.Settings.GetValueOrDefault("TruncationSuffix") as string ?? "...";

            if (maxLength.HasValue && text.Length > maxLength.Value)
            {
                return text.Substring(0, maxLength.Value) + truncationSuffix;
            }

            return text;
        }

        public object Parse(string text, TypeDisplayConfig config)
        {
            return text;
        }
    }

    public class BooleanFormatter : IValueFormatter
    {
        public object Format(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var boolValue = Convert.ToBoolean(value);
            var trueText = config.Settings.GetValueOrDefault("TrueText") as string ?? "是";
            var falseText = config.Settings.GetValueOrDefault("FalseText") as string ?? "否";

            return boolValue ? trueText : falseText;
        }

        public object Parse(string text, TypeDisplayConfig config)
        {
            var trueText = config.Settings.GetValueOrDefault("TrueText") as string ?? "是";
            return string.Equals(text, trueText, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class EnumFormatter : IValueFormatter
    {
        public object Format(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var enumType = value.GetType();
            if (!enumType.IsEnum) return value.ToString();

            var enumValue = (Enum)value;
            var useDescription = (bool)config.Settings.GetValueOrDefault("UseDescription", true);

            if (useDescription)
            {
                var field = enumType.GetField(enumValue.ToString());
                var descriptionAttribute = field?.GetCustomAttribute<DescriptionAttribute>();
                if (descriptionAttribute != null)
                {
                    return descriptionAttribute.Description;
                }
            }

            return enumValue.ToString();
        }

        public object Parse(string text, TypeDisplayConfig config)
        {
            var enumType = Type.GetType(config.Settings.GetValueOrDefault("EnumType") as string);
            if (enumType == null || !enumType.IsEnum)
                throw new ArgumentException("Invalid enum type");

            return Enum.Parse(enumType, text);
        }
    }

    public class DefaultFormatter : IValueFormatter
    {
        public object Format(object value, TypeDisplayConfig config)
        {
            return value?.ToString();
        }

        public object Parse(string text, TypeDisplayConfig config)
        {
            return text;
        }
    }
    public class NumberFormatter : IValueFormatter
    {
        public object Format(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var decimalPlaces = (int)config.Settings["DecimalPlaces"];
            var useThousandsSeparator = (bool)config.Settings["UseThousandsSeparator"];
            var unit = (string)config.Settings["Unit"];

            var number = Convert.ToDouble(value);
            var format = useThousandsSeparator ? $"N{decimalPlaces}" : $"F{decimalPlaces}";
            var formattedValue = number.ToString(format);

            return string.IsNullOrEmpty(unit) ? formattedValue : $"{formattedValue} {unit}";
        }

        public object Parse(string text, TypeDisplayConfig config)
        {
            var unit = (string)config.Settings["Unit"];
            if (!string.IsNullOrEmpty(unit))
            {
                text = text.Replace(unit, "").Trim();
            }

            return double.TryParse(text, out var result) ? result : null;
        }
    }

    public class DateTimeFormatter : IValueFormatter
    {
        public object Format(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var dateTime = (DateTime)value;
            var format = (string)config.Settings["Format"];
            var useLocalTime = (bool)config.Settings["UseLocalTime"];

            if (!useLocalTime && dateTime.Kind == DateTimeKind.Local)
                dateTime = dateTime.ToUniversalTime();
            else if (useLocalTime && dateTime.Kind == DateTimeKind.Utc)
                dateTime = dateTime.ToLocalTime();

            return dateTime.ToString(format);
        }

        public object Parse(string text, TypeDisplayConfig config)
        {
            var format = (string)config.Settings["Format"];
            return DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result) ? result : null;
        }
    }

    // 4. 性能优化 - 值缓存实现
    public class ValueCache
    {
        private readonly ConcurrentDictionary<string, WeakReference<object>> _cache
            = new();
        private readonly int _maxItems;
        private readonly TimeSpan _expiration;

        public ValueCache(int maxItems = 1000, TimeSpan? expiration = null)
        {
            _maxItems = maxItems;
            _expiration = expiration ?? TimeSpan.FromMinutes(10);
        }

        public object GetOrAdd(string key, object value)
        {
            CleanupIfNeeded();
            return _cache.GetOrAdd(key, k => new WeakReference<object>(value))
                        .TryGetTarget(out var cachedValue) ? cachedValue : value;
        }

        private void CleanupIfNeeded()
        {
            if (_cache.Count > _maxItems)
            {
                var keysToRemove = _cache
                    .Where(kvp => !kvp.Value.TryGetTarget(out _))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
    }
    // 1. 配置导入导出服务
    public interface IConfigurationExportImport
    {
        Task ExportAsync(VisualConfig config, string filePath);
        Task<VisualConfig> ImportAsync(string filePath);
       
    }

    public class ConfigurationExportImport : IConfigurationExportImport
    {
        private readonly ILogger<ConfigurationExportImport> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigurationExportImport(ILogger<ConfigurationExportImport> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task ExportAsync(VisualConfig config, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export configuration to {FilePath}", filePath);
                throw new ConfigurationExportException("配置导出失败", ex);
            }
        }

        public async Task<VisualConfig> ImportAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<VisualConfig>(json, _jsonOptions);

                // 验证配置
                await ValidateConfigurationAsync(config);

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import configuration from {FilePath}", filePath);
                throw new ConfigurationImportException("配置导入失败", ex);
            }
        }

       

        private async Task ValidateConfigurationAsync(VisualConfig config)
        {
            // 验证配置的完整性和正确性
            var type = Type.GetType($"{config.TypeFullName}, {config.AssemblyName}");
            if (type == null)
            {
                throw new ConfigurationValidationException(
                    $"找不到类型: {config.TypeFullName}");
            }

        }
    }
    // 3. 自定义异常类
    public class ConfigurationValidationException : Exception
    {
        public ConfigurationValidationException(string message) : base(message) { }
        public ConfigurationValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public class ConfigurationImportException : Exception
    {
        public ConfigurationImportException(string message) : base(message) { }
        public ConfigurationImportException(string message, Exception inner) : base(message, inner) { }
    }
    public class ConfigurationExportException : Exception
    {
        public ConfigurationExportException(string message, Exception inner) : base(message, inner) { }
    }
    // 2. 性能优化 - 虚拟化支持
    public class VirtualizingItemsControl : ItemsControl
    {
        private VirtualizingStackPanel _virtualingPanel;
        private VirtualizingPanel _virtualizingPanel;
        static VirtualizingItemsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(VirtualizingItemsControl),
                new FrameworkPropertyMetadata(typeof(VirtualizingItemsControl)));

            VirtualizingPanel.IsVirtualizingProperty.OverrideMetadata(
                typeof(VirtualizingItemsControl),
                new FrameworkPropertyMetadata(true));

            VirtualizingPanel.VirtualizationModeProperty.OverrideMetadata(
                typeof(VirtualizingItemsControl),
                new FrameworkPropertyMetadata(VirtualizationMode.Recycling));

            ScrollViewer.CanContentScrollProperty.OverrideMetadata(
                typeof(VirtualizingItemsControl),
                new FrameworkPropertyMetadata(true));
        }
         
    }

    // 3. 性能优化 - 延迟加载服务
    public interface ILazyLoadingService
    {
        Task<object> LoadPropertyValueAsync(object instance, string propertyPath);
        void RegisterLazyLoadHandler(Type type, Func<object, string, Task<object>> handler);
    }

    public class LazyLoadingService : ILazyLoadingService
    {
        private readonly Dictionary<Type, Func<object, string, Task<object>>> _handlers
            = new();
        private readonly ILogger<LazyLoadingService> _logger;

        public LazyLoadingService(ILogger<LazyLoadingService> logger)
        {
            _logger = logger;
        }

        public async Task<object> LoadPropertyValueAsync(object instance, string propertyPath)
        {
            if (instance == null) return null;

            var type = instance.GetType();
            if (_handlers.TryGetValue(type, out var handler))
            {
                try
                {
                    return await handler(instance, propertyPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error loading property {Property} for type {Type}",
                        propertyPath, type.Name);
                    return null;
                }
            }

            // 如果没有注册处理器，使用默认的反射方式
            return PropertyValueHelper.GetPropertyValueByPath(instance, propertyPath);
        }

        public void RegisterLazyLoadHandler(
            Type type,
            Func<object, string, Task<object>> handler)
        {
            _handlers[type] = handler;
        }
    }
    // 2. 属性值获取扩展方法
    public static class PropertyValueHelper
    {
        public static object GetPropertyValueByPath(object instance, string propertyPath)
        {
            if (instance == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var properties = propertyPath.Split('.');
            var current = instance;

            foreach (var property in properties)
            {
                if (current == null) return null;

                var propertyInfo = current.GetType().GetProperty(property);
                if (propertyInfo == null)
                    throw new ArgumentException($"Property {property} not found in type {current.GetType().Name}");

                current = propertyInfo.GetValue(current);
            }

            return current;
        }
    }
    // 1. 视图接口
    public interface IVisualizerView
    {
        void BeginUpdate();
        void EndUpdate();
        object ItemsSource { get; set; }
        object SelectedItem { get; set; }
    }

    // 4. 性能优化 - 批量更新支持
    public class BatchUpdateScope : IDisposable
    {
        private readonly IVisualizerView _view;
        private bool _disposed;

        public BatchUpdateScope(IVisualizerView view)
        {
            _view = view;
            _view.BeginUpdate();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _view.EndUpdate();
                _disposed = true;
            }
        }
    }
    // 1. 配置变更相关类
    public class ConfigurationChange
    {
        public string PropertyPath { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public DateTime ChangeTime { get; set; }
        public string ChangeType { get; set; }

        public ConfigurationChange(string propertyPath, object oldValue, object newValue)
        {
            PropertyPath = propertyPath;
            OldValue = oldValue;
            NewValue = newValue;
            ChangeTime = DateTime.UtcNow;
        }
    }
    // 5. 配置变更跟踪
    public class ConfigurationChangeTracker
    {
        private readonly Stack<ConfigurationChange> _undoStack = new();
        private readonly Stack<ConfigurationChange> _redoStack = new();
        private readonly int _maxChanges;

        public ConfigurationChangeTracker(int maxChanges = 100)
        {
            _maxChanges = maxChanges;
        }

        public void TrackChange(ConfigurationChange change)
        {
            _undoStack.Push(change);
            _redoStack.Clear();

            if (_undoStack.Count > _maxChanges)
            {
                _undoStack.Pop();
            }
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public ConfigurationChange Undo()
        {
            if (!CanUndo) return null;

            var change = _undoStack.Pop();
            _redoStack.Push(change);
            return change;
        }

        public ConfigurationChange Redo()
        {
            if (!CanRedo) return null;

            var change = _redoStack.Pop();
            _undoStack.Push(change);
            return change;
        }
    }
     
}

