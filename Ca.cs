// 1. 核心配置模型
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;
using Visualizer.Core.Defaults.Visualizer.Core.Services;
using Visualizer.Core.Models;
using Visualizer.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Windows.Media.Effects;

namespace Visualizer.Core.Models
{
    // 2. VisualConfig 扩展方法
    public static class VisualConfigExtensions
    {
        public static DisplaySettings GetModeSettings(this VisualConfig config, DisplayMode mode)
        {
            if (!config.Properties.Any()) return new DisplaySettings();

            var firstProp = config.Properties.First().Value;
            if (firstProp.ModeSettings.TryGetValue(mode, out var settings))
            {
                return settings;
            }

            return new DisplaySettings
            {
                Visible = true,
                Order = 0,
                Width = "Auto"
            };
        }
    }
    public class VisualConfig
    {
        public string TypeFullName { get; set; }
        public string AssemblyName { get; set; }
        public DateTime LastModified { get; set; }
        public string Version { get; set; } = "1.0";
        public Dictionary<string, PropertyVisualConfig> Properties { get; set; }
        public List<DisplayMode> SupportedModes { get; set; }

        // 用于防止循环引用的最大深度
        public int MaxDepth { get; set; } = 3;
    }
    // 2. 显示设置类
    public class DisplaySettings : BindableBase
    {
        private bool _visible = true;
        private int _order;
        private string _width = "Auto";
        private string _height = "Auto";
        private string _alignment = "Left";
        private bool _isReadOnly = false;
        private string _format;
        private Style _style;
        private DataTemplate _template;

        public bool Visible
        {
            get => _visible;
            set => SetProperty(ref _visible, value);
        }

        public int Order
        {
            get => _order;
            set => SetProperty(ref _order, value);
        }

        public string Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public string Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public string Alignment
        {
            get => _alignment;
            set => SetProperty(ref _alignment, value);
        }

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => SetProperty(ref _isReadOnly, value);
        }

        public string Format
        {
            get => _format;
            set => SetProperty(ref _format, value);
        }

        public Style Style
        {
            get => _style;
            set => SetProperty(ref _style, value);
        }

        public DataTemplate Template
        {
            get => _template;
            set => SetProperty(ref _template, value);
        }
    }
    public class PropertyVisualConfig
    {
        public string PropertyPath { get; set; }
        public string DisplayName { get; set; }
        public Dictionary<DisplayMode, DisplaySettings> ModeSettings { get; set; }
        public TypeDisplayConfig TypeConfig { get; set; }
    }

    public class TypeDisplayConfig
    {
        public string TypeName { get; set; }
        public DisplayType DisplayType { get; set; }
        public Dictionary<string, object> Settings { get; set; }
    }

    public enum DisplayMode
    {
        List,       // 列表行显示
        Card,       // 预览卡片显示
        Detail      // 详细信息显示
    }

    public enum DisplayType
    {
        Text,
        String,
        Number,
        DateTime,
        Boolean,
        Enum,
        Collection,
        ComplexObject,
        Image,
        File,
        Link,
        Custom
    }
}

// 2. 默认显示策略管理
namespace Visualizer.Core.Defaults
{
    public interface ITypeDisplayStrategy
    {
        DisplayType DisplayType { get; }
        TypeDisplayConfig GetDefaultConfig();
        object FormatValue(object value, TypeDisplayConfig config);
    }
    public class StringDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.String;

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.String,
                Settings = new Dictionary<string, object>
            {
                { "MaxLength", null },
                { "TruncationSuffix", "..." },
                { "TextWrapping", TextWrapping.NoWrap },
                { "IsBold", false },
                { "FontSize", 12.0 },
                { "IsMultiline", false },
                { "MaxLines", null }
            }
            };
        }

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var text = value.ToString();
            var maxLength = config.Settings["MaxLength"] as int?;
            var truncationSuffix = (string)config.Settings["TruncationSuffix"];

            if (maxLength.HasValue && text.Length > maxLength.Value)
            {
                return text.Substring(0, maxLength.Value) + truncationSuffix;
            }

            return text;
        }
    }

    public class DateTimeDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.DateTime;

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.DateTime,
                Settings = new Dictionary<string, object>
            {
                { "Format", "yyyy-MM-dd HH:mm:ss" },
                { "ShowTime", true },
                { "ShowSeconds", true },
                { "UseLocalTime", true },
                { "TimeZone", TimeZoneInfo.Local.Id }
            }
            };
        }

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var dateTime = (DateTime)value;
            var format = (string)config.Settings["Format"];
            var useLocalTime = (bool)config.Settings["UseLocalTime"];
            var showTime = (bool)config.Settings["ShowTime"];
            var showSeconds = (bool)config.Settings["ShowSeconds"];

            // 调整时区
            if (useLocalTime && dateTime.Kind == DateTimeKind.Utc)
            {
                dateTime = dateTime.ToLocalTime();
            }
            else if (!useLocalTime && dateTime.Kind == DateTimeKind.Local)
            {
                dateTime = dateTime.ToUniversalTime();
            }

            // 根据设置构建格式字符串
            if (!showTime)
            {
                format = "yyyy-MM-dd";
            }
            else if (!showSeconds)
            {
                format = format.Replace(":ss", "");
            }

            return dateTime.ToString(format);
        }
    }

    public class BooleanDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.Boolean;

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.Boolean,
                Settings = new Dictionary<string, object>
            {
                { "TrueText", "是" },
                { "FalseText", "否" },
                { "UseCheckBox", true },
                { "TrueColor", "#28a745" },
                { "FalseColor", "#dc3545" }
            }
            };
        }

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var isTrue = (bool)value;
            var trueText = (string)config.Settings["TrueText"];
            var falseText = (string)config.Settings["FalseText"];

            return isTrue ? trueText : falseText;
        }
    }

    public class TextDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.Text;

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.Text,
                Settings = new Dictionary<string, object>
            {
                { "MaxLength", null },
                { "TruncationSuffix", "..." },
                { "TextWrapping", TextWrapping.NoWrap },
                { "FontSize", 12.0 }
            }
            };
        }

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var text = value.ToString();
            var maxLength = config.Settings["MaxLength"] as int?;
            var truncationSuffix = (string)config.Settings["TruncationSuffix"];

            if (maxLength.HasValue && text.Length > maxLength.Value)
            {
                return text.Substring(0, maxLength.Value) + truncationSuffix;
            }

            return text;
        }
    }

    // 添加一个辅助类来处理值的格式化和显示
    public class DisplayHelper
    {
        private readonly Dictionary<DisplayType, Style> _styleCache
            = new Dictionary<DisplayType, Style>();

        public Style GetStyleForType(TypeDisplayConfig config)
        {
            if (_styleCache.TryGetValue(config.DisplayType, out var cachedStyle))
            {
                return cachedStyle;
            }

            var style = new Style(typeof(FrameworkElement));

            switch (config.DisplayType)
            {
                case DisplayType.Boolean:
                    if ((bool)config.Settings["UseCheckBox"])
                    {
                        style = CreateCheckBoxStyle(config);
                    }
                    else
                    {
                        style = CreateBooleanTextStyle(config);
                    }
                    break;

                case DisplayType.DateTime:
                    style = CreateDateTimeStyle(config);
                    break;

                case DisplayType.String:
                case DisplayType.Text:
                    style = CreateTextStyle(config);
                    break;
            }

            _styleCache[config.DisplayType] = style;
            return style;
        }

        private Style CreateCheckBoxStyle(TypeDisplayConfig config)
        {
            var style = new Style(typeof(CheckBox));
            style.Setters.Add(new Setter(CheckBox.IsEnabledProperty, false));
            style.Setters.Add(new Setter(CheckBox.MarginProperty, new Thickness(4)));
            return style;
        }

        private Style CreateBooleanTextStyle(TypeDisplayConfig config)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));

            var trueColor = (string)config.Settings["TrueColor"];
            var falseColor = (string)config.Settings["FalseColor"];

            var trigger = new DataTrigger
            {
                Binding = new Binding(),
                Value = true
            };
            trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(trueColor))));

            style.Triggers.Add(trigger);
            return style;
        }

        private Style CreateDateTimeStyle(TypeDisplayConfig config)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty,
                new FontFamily("Consolas")));
            return style;
        }

        private Style CreateTextStyle(TypeDisplayConfig config)
        {
            var style = new Style(typeof(TextBlock));

            if (config.Settings.TryGetValue("TextWrapping", out var wrapping))
            {
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, wrapping));
            }

            if (config.Settings.TryGetValue("FontSize", out var fontSize))
            {
                style.Setters.Add(new Setter(TextBlock.FontSizeProperty, fontSize));
            }

            return style;
        }
    }
    public class DefaultDisplayStrategyProvider
    {
        private readonly Dictionary<Type, ITypeDisplayStrategy> _strategies;
        private readonly ITypeDisplayStrategy _fallbackStrategy;

        public DefaultDisplayStrategyProvider()
        {
            _strategies = new Dictionary<Type, ITypeDisplayStrategy>
            {
                { typeof(string), new StringDisplayStrategy() },
                { typeof(int), new NumberDisplayStrategy() },
                { typeof(double), new NumberDisplayStrategy() },
                { typeof(decimal), new NumberDisplayStrategy() },
                { typeof(DateTime), new DateTimeDisplayStrategy() },
                { typeof(bool), new BooleanDisplayStrategy() },
                // 添加更多类型的策略...
            };

            _fallbackStrategy = new TextDisplayStrategy();
        }

        public ITypeDisplayStrategy GetStrategy(Type type)
        {
            if (_strategies.TryGetValue(type, out var strategy))
                return strategy;

            if (type.IsEnum)
                return new EnumDisplayStrategy();

            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                return new CollectionDisplayStrategy();

            if (!type.IsPrimitive && type != typeof(string))
                return new ComplexObjectDisplayStrategy();

            return _fallbackStrategy;
        }
    }
    // 2. 显示策略实现
    public class EnumDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.Enum;

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            throw new NotImplementedException();
        }

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.Enum,
                Settings = new Dictionary<string, object>
            {
                { "UseDescription", true },
                { "ShowAsComboBox", false },
                { "AllowMultiple", false },
                { "SortByDescription", true }
            }
            };
        }
    }

    public class CollectionDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.Collection;

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            throw new NotImplementedException();
        }

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.Collection,
                Settings = new Dictionary<string, object>
            {
                { "MaxDisplayItems", 5 },
                { "Separator", ", " },
                { "ShowCount", true },
                { "AllowExpand", true },
                { "ItemTemplate", null }
            }
            };
        }
    }

    public class ComplexObjectDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.ComplexObject;

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            throw new NotImplementedException();
        }

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.ComplexObject,
                Settings = new Dictionary<string, object>
            {
                { "ExpandByDefault", false },
                { "MaxDepth", 2 },
                { "ShowType", true },
                { "CustomTemplate", null }
            }
            };
        }
    }
    // 示例策略实现
    public class NumberDisplayStrategy : ITypeDisplayStrategy
    {
        public DisplayType DisplayType => DisplayType.Number;

        public TypeDisplayConfig GetDefaultConfig()
        {
            return new TypeDisplayConfig
            {
                DisplayType = DisplayType.Number,
                Settings = new Dictionary<string, object>
                {
                    { "DecimalPlaces", 2 },
                    { "UseThousandsSeparator", true },
                    { "MinValue", null },
                    { "MaxValue", null },
                    { "Unit", "" }
                }
            };
        }

        public object FormatValue(object value, TypeDisplayConfig config)
        {
            if (value == null) return null;

            var decimalPlaces = (int)config.Settings["DecimalPlaces"];
            var useThousandsSeparator = (bool)config.Settings["UseThousandsSeparator"];
            var format = useThousandsSeparator ? $"N{decimalPlaces}" : $"F{decimalPlaces}";

            var formattedValue = Convert.ToDouble(value).ToString(format);
            var unit = config.Settings["Unit"]?.ToString();

            return string.IsNullOrEmpty(unit) ? formattedValue : $"{formattedValue} {unit}";
        }
    }

    // 3. 配置缓存管理
    public class ConfigurationCache
    {
        private static readonly ConcurrentDictionary<string, VisualConfig> _configCache
            = new ConcurrentDictionary<string, VisualConfig>();

        private static readonly ConcurrentDictionary<Type, TypeDisplayConfig> _typeConfigCache
            = new ConcurrentDictionary<Type, TypeDisplayConfig>();

        public static VisualConfig GetOrCreateConfig(Type modelType)
        {
            var key = $"{modelType.FullName}, {modelType.Assembly.GetName().Name}";
            return _configCache.GetOrAdd(key, _ => CreateDefaultConfig(modelType));
        }

        public static VisualConfig CreateDefaultConfig(Type modelType)
        {
            var provider = new DefaultDisplayStrategyProvider();
            var config = new VisualConfig
            {
                TypeFullName = modelType.FullName,
                AssemblyName = modelType.Assembly.GetName().Name,
                LastModified = DateTime.UtcNow,
                Properties = new Dictionary<string, PropertyVisualConfig>(),
                SupportedModes = new List<DisplayMode>
                {
                    DisplayMode.List,
                    DisplayMode.Card,
                    DisplayMode.Detail
                }
            };

            foreach (var prop in modelType.GetProperties())
            {
                var strategy = provider.GetStrategy(prop.PropertyType);
                config.Properties[prop.Name] = new PropertyVisualConfig
                {
                    PropertyPath = prop.Name,
                    DisplayName = prop.Name,
                    TypeConfig = strategy.GetDefaultConfig(),
                    ModeSettings = new Dictionary<DisplayMode, DisplaySettings>
                    {
                        [DisplayMode.List] = CreateDefaultListSettings(prop),
                        [DisplayMode.Card] = CreateDefaultCardSettings(prop),
                        [DisplayMode.Detail] = CreateDefaultDetailSettings(prop)
                    }
                };
            }

            return config;
        }
        // 1. 默认设置创建方法
        private static DisplaySettings CreateDefaultCardSettings(PropertyInfo prop)
        {
            return new DisplaySettings
            {
                Visible = true,
                Order = 100,
                Width = "200", // 卡片模式默认宽度
                Height = "Auto",
                Template = null,
                Style = null,
                Format = null,
                IsReadOnly = true
            };
        }

        private static DisplaySettings CreateDefaultDetailSettings(PropertyInfo prop)
        {
            return new DisplaySettings
            {
                Visible = true,
                Order = 100,
                Width = "*", // 详细模式使用剩余空间
                Height = "Auto",
                Template = null,
                Style = null,
                Format = null,
                IsReadOnly = false
            };
        }
        private static DisplaySettings CreateDefaultListSettings(PropertyInfo prop)
        {
            // 根据属性类型创建默认的列表显示设置
            return new DisplaySettings
            {
                Visible = true,
                Order = 100,
                Width = "Auto",
                // 其他默认设置...
            };
        }
        public static VisualConfig GetOrAdd(Type type, VisualConfig config)
        {
            var key = $"{type.FullName}, {type.Assembly.GetName().Name}";
            return _configCache.GetOrAdd(key, _ => config);
        }


        public static void Clear()
        {
            _configCache.Clear();
        }
        // 其他helper方法...
    }
    // 1. 基础视图模型
    namespace Visualizer.Core.ViewModels
    {
        public class VisualizerViewModel : BindableBase
        {
            private readonly IEventAggregator _eventAggregator;
            private readonly IConfigurationService _configService;
            private readonly IVisualRendererService _renderService;

            private DisplayMode _currentMode = DisplayMode.List;
            private object _selectedItem;
            private IEnumerable _items;
            private VisualConfig _currentConfig;

            public VisualizerViewModel(
                IEventAggregator eventAggregator,
                IConfigurationService configService,
                IVisualRendererService renderService)
            {
                _eventAggregator = eventAggregator;
                _configService = configService;
                _renderService = renderService;

                // 命令初始化
                SwitchModeCommand = new DelegateCommand<DisplayMode?>(ExecuteSwitchMode);
                NavigateCommand = new DelegateCommand<string>(ExecuteNavigate);

                // 订阅事件
                _eventAggregator.GetEvent<ConfigurationChangedEvent>().Subscribe(OnConfigurationChanged);
            }

            public DisplayMode CurrentMode
            {
                get => _currentMode;
                set => SetProperty(ref _currentMode, value);
            }

            public object SelectedItem
            {
                get => _selectedItem;
                set
                {
                    if (SetProperty(ref _selectedItem, value))
                    {
                        _eventAggregator.GetEvent<ItemSelectedEvent>().Publish(value);
                    }
                }
            }

            public IEnumerable Items
            {
                get => _items;
                set => SetProperty(ref _items, value);
            }

            public DelegateCommand<DisplayMode?> SwitchModeCommand { get; }
            public DelegateCommand<string> NavigateCommand { get; }

            public async Task InitializeAsync(IEnumerable items, Type itemType)
            {
                // 获取或创建配置
                _currentConfig = await _configService.GetOrCreateConfigAsync(itemType);
                Items = items;

                // 应用默认显示模式
                await ApplyDisplayModeAsync(CurrentMode);
            }

            private async Task ApplyDisplayModeAsync(DisplayMode mode)
            {
                // 根据当前模式生成显示模板
                var template = await _renderService.GenerateTemplateAsync(_currentConfig, mode);
                // 通知UI更新显示模板
                _eventAggregator.GetEvent<TemplateChangedEvent>().Publish(template);
            }

            private void ExecuteSwitchMode(DisplayMode? mode)
            {
                if (!mode.HasValue) return;
                CurrentMode = mode.Value;
                _ = ApplyDisplayModeAsync(mode.Value);
            }

            private void ExecuteNavigate(string direction)
            {
                if (!(Items is IList list) || list.Count == 0) return;

                var currentIndex = list.IndexOf(SelectedItem);
                var newIndex = direction == "Next"
                    ? (currentIndex + 1) % list.Count
                    : (currentIndex - 1 + list.Count) % list.Count;

                SelectedItem = list[newIndex];
            }

            private void OnConfigurationChanged(VisualConfig config)
            {
                _currentConfig = config;
                _ = ApplyDisplayModeAsync(CurrentMode);
            }
        }
    }

    // 2. 服务接口
    namespace Visualizer.Core.Services
    {
        public interface IConfigurationService
        {
            Task<VisualConfig> GetOrCreateConfigAsync(Type type);
            Task SaveConfigAsync(VisualConfig config);
            Task<VisualConfig> ImportConfigAsync(string path);
            Task ExportConfigAsync(VisualConfig config, string path);
        }

        public interface IVisualRendererService
        {
            Task<object> GenerateTemplateAsync(VisualConfig config, DisplayMode mode);
            object FormatValue(object value, PropertyVisualConfig propertyConfig);
        }
    }

    // 3. 实现配置服务
    namespace Visualizer.Core.Services.Impl
    {
        // 3. VisualizerOptions 类
        public class VisualizerOptions
        {
            public string ConfigurationFolder { get; set; } = "Configs";
            public int CacheTimeout { get; set; } = 300; // 秒
            public int MaxCacheItems { get; set; } = 1000;
            public bool AutoSave { get; set; } = true;
            public string DefaultDateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        }
        public class ConfigurationService : IConfigurationService
        {
            private readonly string _configFolder;
            private readonly ILogger<ConfigurationService> _logger;
            private readonly IConfigurationExportImport _exportImport;

            public ConfigurationService(
                IOptions<VisualizerOptions> options,
                ILogger<ConfigurationService> logger, IConfigurationExportImport exportImport)
            {
                _configFolder = options.Value.ConfigurationFolder;
                _logger = logger;
                _exportImport = exportImport;
                Directory.CreateDirectory(_configFolder);
            }

            public async Task<VisualConfig> GetOrCreateConfigAsync(Type type)
            {
                var configPath = GetConfigPath(type);

                if (File.Exists(configPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(configPath);
                        var config = JsonSerializer.Deserialize<VisualConfig>(json);
                        return ConfigurationCache.GetOrAdd(type, config);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load configuration for {Type}", type.FullName);
                    }
                }

                // 创建默认配置
                var defaultConfig = ConfigurationCache.GetOrCreateConfig(type);
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }

            public async Task SaveConfigAsync(VisualConfig config)
            {
                var configPath = GetConfigPath(config);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(configPath, json);

                // 更新缓存
                var type = Type.GetType($"{config.TypeFullName}, {config.AssemblyName}");
                if (type != null)
                {
                    ConfigurationCache.GetOrAdd(type, config);
                }
            }

            private string GetConfigPath(Type type)
            {
                return Path.Combine(_configFolder, $"{type.FullName}.json");
            }

            private string GetConfigPath(VisualConfig config)
            {
                return Path.Combine(_configFolder, $"{config.TypeFullName}.json");
            }
            public async Task ExportConfigAsync(VisualConfig config, string filePath)
            {
                await _exportImport.ExportAsync(config, filePath);
            }

            public async Task<VisualConfig> ImportConfigAsync(string filePath)
            {
                return await _exportImport.ImportAsync(filePath);
            }
        }
    }
    // 4. 模板提供者接口和实现
    public interface ITemplateProvider
    {
        Task<object> GetTemplateAsync(DisplayMode mode);
        Task<DataTemplate> CreatePropertyTemplateAsync(PropertyVisualConfig propertyConfig);
    }
    // 2. 添加 BooleanToTextConverter
    public class BooleanToTextConverter : IValueConverter
    {
        public string TrueText { get; set; } = "是";
        public string FalseText { get; set; } = "否";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueText : FalseText;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return string.Equals(stringValue, TrueText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
    public class TemplateProvider : ITemplateProvider
    {
        private readonly Dictionary<DisplayMode, object> _templateCache
            = new Dictionary<DisplayMode, object>();

        public async Task<object> GetTemplateAsync(DisplayMode mode)
        {
            if (_templateCache.TryGetValue(mode, out var cachedTemplate))
            {
                return cachedTemplate;
            }

            var template = await CreateTemplateForMode(mode);
            _templateCache[mode] = template;
            return template;
        }

        public async Task<DataTemplate> CreatePropertyTemplateAsync(PropertyVisualConfig propertyConfig)
        {
            return await Task.Run(() =>
            {
                var template = new DataTemplate();
                var factory = new FrameworkElementFactory(typeof(ContentPresenter));

                // 根据属性配置设置模板
                switch (propertyConfig.TypeConfig.DisplayType)
                {
                    case DisplayType.String:
                    case DisplayType.Text:
                        factory = CreateTextBlockFactory(propertyConfig);
                        break;
                    case DisplayType.Number:
                        factory = CreateNumberFactory(propertyConfig);
                        break;
                    case DisplayType.DateTime:
                        factory = CreateDateTimeFactory(propertyConfig);
                        break;
                    case DisplayType.Boolean:
                        factory = CreateBooleanFactory(propertyConfig);
                        break;
                        // 添加其他类型的处理...
                }

                template.VisualTree = factory;
                return template;
            });
        }

        private FrameworkElementFactory CreateTextBlockFactory(PropertyVisualConfig config)
        {
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new Binding(config.PropertyPath));

            // 应用配置的样式
            if (config.TypeConfig.Settings.TryGetValue("TextWrapping", out var wrapping))
            {
                factory.SetValue(TextBlock.TextWrappingProperty, wrapping);
            }

            return factory;
        }

        private FrameworkElementFactory CreateNumberFactory(PropertyVisualConfig config)
        {
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            var binding = new Binding(config.PropertyPath);

            // 设置数字格式化
            if (config.TypeConfig.Settings.TryGetValue("Format", out var format))
            {
                binding.StringFormat = format as string;
            }

            factory.SetBinding(TextBlock.TextProperty, binding);
            return factory;
        }

        private FrameworkElementFactory CreateDateTimeFactory(PropertyVisualConfig config)
        {
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            var binding = new Binding(config.PropertyPath);

            // 设置日期格式化
            if (config.TypeConfig.Settings.TryGetValue("Format", out var format))
            {
                binding.StringFormat = format as string;
            }

            factory.SetBinding(TextBlock.TextProperty, binding);
            return factory;
        }

        private FrameworkElementFactory CreateBooleanFactory(PropertyVisualConfig config)
        {
            if ((bool)config.TypeConfig.Settings["UseCheckBox"])
            {
                var factory = new FrameworkElementFactory(typeof(CheckBox));
                factory.SetBinding(CheckBox.IsCheckedProperty, new Binding(config.PropertyPath));
                factory.SetValue(CheckBox.IsEnabledProperty, false);
                return factory;
            }
            else
            {
                var factory = new FrameworkElementFactory(typeof(TextBlock));
                var binding = new Binding(config.PropertyPath)
                {
                    Converter = new BooleanToTextConverter
                    {
                        TrueText = (string)config.TypeConfig.Settings["TrueText"],
                        FalseText = (string)config.TypeConfig.Settings["FalseText"]
                    }
                };
                factory.SetBinding(TextBlock.TextProperty, binding);
                return factory;
            }
        }

        private async Task<object> CreateTemplateForMode(DisplayMode mode)
        {
            return await Task.Run(() =>
            {
                var template = new DataTemplate();
                switch (mode)
                {
                    case DisplayMode.List:
                        // 创建列表模式的基础模板
                        var listFactory = new FrameworkElementFactory(typeof(Grid));
                        template.VisualTree = listFactory;
                        break;

                    case DisplayMode.Card:
                        // 创建卡片模式的基础模板
                        var cardFactory = new FrameworkElementFactory(typeof(Border));
                        cardFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                        cardFactory.SetValue(Border.PaddingProperty, new Thickness(8));
                        template.VisualTree = cardFactory;
                        break;

                    case DisplayMode.Detail:
                        // 创建详细模式的基础模板
                        var detailFactory = new FrameworkElementFactory(typeof(ScrollViewer));
                        template.VisualTree = detailFactory;
                        break;

                    default:
                        throw new ArgumentException($"Unsupported display mode: {mode}");
                }
                return template;
            });
        }
    }
    // 4. 渲染服务实现
    namespace Visualizer.Core.Services.Impl
    {
        public class VisualRendererService : IVisualRendererService
        {
            private readonly ITemplateProvider _templateProvider;
            private readonly DefaultDisplayStrategyProvider _strategyProvider;
            private readonly ILogger<VisualRendererService> _logger;

            public VisualRendererService(
                ITemplateProvider templateProvider,
                ILogger<VisualRendererService> logger)
            {
                _templateProvider = templateProvider;
                _strategyProvider = new DefaultDisplayStrategyProvider();
                _logger = logger;
            }

            private async Task CustomizeListTemplate(FrameworkElementFactory rootFactory, VisualConfig config)
            {
                try
                {
                    // 创建Grid的列定义
                    var columnDefinitions = new List<ColumnDefinition>();

                    // 获取可见的属性配置
                    var visibleProperties = config.Properties
                        .Where(p => p.Value.ModeSettings[DisplayMode.List].Visible)
                        .OrderBy(p => p.Value.ModeSettings[DisplayMode.List].Order)
                        .ToList();

                    // 添加列定义
                    foreach (var prop in visibleProperties)
                    {
                        var settings = prop.Value.ModeSettings[DisplayMode.List];
                        var width = ParseColumnWidth(settings.Width);
                        columnDefinitions.Add(new ColumnDefinition { Width = width });
                    }

                    rootFactory.SetValue(Grid.ColumnProperty,
                        new GridLengthConverter().ConvertFromString(
                            string.Join(",", columnDefinitions.Select(c => c.Width))));

                    // 为每个属性创建单元格内容
                    for (int i = 0; i < visibleProperties.Count; i++)
                    {
                        var prop = visibleProperties[i];
                        var cellFactory = await CreateListCellFactory(prop.Value, i);
                        rootFactory.AppendChild(cellFactory);
                    }

                    // 添加行样式
                    var rowStyle = CreateListRowStyle();
                    rootFactory.SetValue(Grid.StyleProperty, rowStyle);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error customizing list template");
                    throw;
                }
            }

            private async Task CustomizeCardTemplate(FrameworkElementFactory rootFactory, VisualConfig config)
            {
                try
                {
                    // 设置卡片基本样式
                    rootFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                    rootFactory.SetValue(Border.PaddingProperty, new Thickness(12));
                    rootFactory.SetValue(Border.MarginProperty, new Thickness(8));
                    rootFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.White));
                    rootFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(230, 230, 230)));
                    rootFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                    // 添加阴影效果
                    var effect = new DropShadowEffect
                    {
                        BlurRadius = 8,
                        ShadowDepth = 2,
                        Opacity = 0.2
                    };
                    rootFactory.SetValue(Border.EffectProperty, effect);

                    // 创建内容面板
                    var contentPanel = new FrameworkElementFactory(typeof(StackPanel));
                    contentPanel.SetValue(StackPanel.MarginProperty, new Thickness(8));

                    // 添加属性显示
                    var visibleProperties = config.Properties
                        .Where(p => p.Value.ModeSettings[DisplayMode.Card].Visible)
                        .OrderBy(p => p.Value.ModeSettings[DisplayMode.Card].Order);

                    foreach (var prop in visibleProperties)
                    {
                        var propertyContainer = await CreateCardPropertyContainer(prop.Value);
                        contentPanel.AppendChild(propertyContainer);
                    }

                    rootFactory.AppendChild(contentPanel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error customizing card template");
                    throw;
                }
            }

            private async Task CustomizeDetailTemplate(FrameworkElementFactory rootFactory, VisualConfig config)
            {
                try
                {
                    // 设置ScrollViewer属性
                    rootFactory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,
                        ScrollBarVisibility.Disabled);
                    rootFactory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,
                        ScrollBarVisibility.Auto);
                    rootFactory.SetValue(ScrollViewer.PaddingProperty, new Thickness(16));

                    // 创建主容器
                    var mainPanel = new FrameworkElementFactory(typeof(StackPanel));
                    mainPanel.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 16, 0));

                    // 添加属性分组和显示
                    var propertyGroups = config.Properties
                        .Where(p => p.Value.ModeSettings[DisplayMode.Detail].Visible)
                        .GroupBy(p => p.Value.DisplayName ?? "General")
                        .OrderBy(g => g.Key == "General" ? 0 : 1)
                        .ThenBy(g => g.Key);

                    foreach (var group in propertyGroups)
                    {
                        var groupContainer = await CreateDetailGroupContainer(
                            group.Key,
                            group.OrderBy(p => p.Value.ModeSettings[DisplayMode.Detail].Order));
                        mainPanel.AppendChild(groupContainer);
                    }

                    rootFactory.AppendChild(mainPanel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error customizing detail template");
                    throw;
                }
            }

            private async Task<FrameworkElementFactory> CreateListCellFactory(
                PropertyVisualConfig propertyConfig,
                int columnIndex)
            {
                var cellFactory = new FrameworkElementFactory(typeof(Border));
                cellFactory.SetValue(Grid.ColumnProperty, columnIndex);
                cellFactory.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));

                // 创建并添加内容
                var contentFactory = await CreatePropertyElement(propertyConfig);
                cellFactory.AppendChild(contentFactory);

                return cellFactory;
            }

            private async Task<FrameworkElementFactory> CreateCardPropertyContainer(
                PropertyVisualConfig propertyConfig)
            {
                var containerFactory = new FrameworkElementFactory(typeof(DockPanel));
                containerFactory.SetValue(DockPanel.MarginProperty, new Thickness(0, 4, 0, 4));

                // 创建标签
                var labelFactory = new FrameworkElementFactory(typeof(TextBlock));
                labelFactory.SetValue(TextBlock.TextProperty, propertyConfig.DisplayName);
                labelFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
                labelFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
                labelFactory.SetValue(DockPanel.DockProperty, Dock.Left);

                // 创建值显示
                var valueFactory = await CreatePropertyElement(propertyConfig);
                valueFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

                containerFactory.AppendChild(labelFactory);
                containerFactory.AppendChild(valueFactory);

                return containerFactory;
            }

            private async Task<FrameworkElementFactory> CreateDetailGroupContainer(
                string groupName,
                IEnumerable<KeyValuePair<string, PropertyVisualConfig>> properties)
            {
                var groupFactory = new FrameworkElementFactory(typeof(StackPanel));
                groupFactory.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 0, 16));

                // 创建组标题
                if (groupName != "General")
                {
                    var titleFactory = new FrameworkElementFactory(typeof(TextBlock));
                    titleFactory.SetValue(TextBlock.TextProperty, groupName);
                    titleFactory.SetValue(TextBlock.FontSizeProperty, 16d);
                    titleFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
                    titleFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 8));
                    groupFactory.AppendChild(titleFactory);
                }

                // 添加属性
                foreach (var prop in properties)
                {
                    var propertyContainer = await CreateDetailPropertyContainer(prop.Value);
                    groupFactory.AppendChild(propertyContainer);
                }

                return groupFactory;
            }
            // 3. CreateDetailPropertyContainer 方法
            private FrameworkElementFactory CreateDetailPropertyContainer(PropertyVisualConfig propertyConfig)
            {
                var container = new FrameworkElementFactory(typeof(DockPanel));
                container.SetValue(DockPanel.MarginProperty, new Thickness(0, 0, 0, 8));

                // 创建属性标签
                var labelFactory = new FrameworkElementFactory(typeof(TextBlock));
                labelFactory.SetValue(TextBlock.TextProperty, propertyConfig.DisplayName);
                labelFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
                labelFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
                labelFactory.SetValue(TextBlock.MinWidthProperty, 120.0);
                labelFactory.SetValue(DockPanel.DockProperty, Dock.Left);
                container.AppendChild(labelFactory);

                // 创建属性值显示
                var valueContainer = CreatePropertyElement(propertyConfig);

                // 应用附加设置
                if (propertyConfig.ModeSettings.TryGetValue(DisplayMode.Detail, out var detailSettings))
                {
                    if (!string.IsNullOrEmpty(detailSettings.Width))
                    {
                        valueContainer.SetValue(FrameworkElement.WidthProperty,
                            new GridLengthConverter().ConvertFromString(detailSettings.Width));
                    }

                    if (!string.IsNullOrEmpty(detailSettings.Height))
                    {
                        valueContainer.SetValue(FrameworkElement.HeightProperty,
                            new GridLengthConverter().ConvertFromString(detailSettings.Height));
                    }

                    valueContainer.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

                    if (detailSettings.Style != null)
                    {
                        valueContainer.SetValue(FrameworkElement.StyleProperty, detailSettings.Style);
                    }
                }

                container.AppendChild(valueContainer);
                return container;
            }
            private Style CreateListRowStyle()
            {
                var style = new Style(typeof(Grid));

                // 添加触发器
                var trigger = new Trigger { Property = Grid.IsMouseOverProperty, Value = true };
                trigger.Setters.Add(new Setter(Grid.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(245, 245, 245))));
                style.Triggers.Add(trigger);

                return style;
            }

            private GridLength ParseColumnWidth(string width)
            {
                if (string.IsNullOrEmpty(width) || width.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                    return GridLength.Auto;

                if (width.EndsWith("*"))
                {
                    var factor = width.Length == 1 ? 1 :
                        double.Parse(width.TrimEnd('*'));
                    return new GridLength(factor, GridUnitType.Star);
                }

                return new GridLength(double.Parse(width));
            }

           

            private FrameworkElementFactory CreatePropertyElement(PropertyVisualConfig propertyConfig)
            {
                // 根据属性配置创建对应的UI元素
                var factory = new FrameworkElementFactory(typeof(ContentPresenter));
                factory.SetBinding(ContentPresenter.ContentTemplateProperty,
                    new Binding { Source = _templateProvider.CreatePropertyTemplateAsync(propertyConfig).Result });
                return factory;
            }

            public Task<object> GenerateTemplateAsync(VisualConfig config, DisplayMode mode)
            {
                throw new NotImplementedException();
            }

            public object FormatValue(object value, PropertyVisualConfig propertyConfig)
            {
                throw new NotImplementedException();
            }
        }
    }
}
